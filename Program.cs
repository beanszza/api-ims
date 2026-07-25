using Applications.Interfaces;
using Applications.Services;
using Infrastructures.Persistence;
using ms_analytics.Controllers;
using ms_analytics.Infrastructure;
using ms_analytics.Models;
using ms_analytics.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.ML;
using Microsoft.OpenApi;
using MongoDB.Driver;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Helper functions (moved to top so they can be used early)
static string ResolveScmConnectionString(IConfiguration configuration)
{
    var host = Environment.GetEnvironmentVariable("POSTGRES_DB_HOST");
    if (!string.IsNullOrWhiteSpace(host))
    {
        var port = Environment.GetEnvironmentVariable("POSTGRES_DB_PORT");
        if (string.IsNullOrWhiteSpace(port))
            port = "5432";

        var username = Environment.GetEnvironmentVariable("POSTGRES_USERNAME");
        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? string.Empty;

        var cs = $"Host={host};Port={port};Database=scm_db;Username={username};Password={password}";
        return AppendNpgsqlSslModeForDevContainers(cs);
    }

    var configured = configuration.GetConnectionString("ScmDbConnection");
    if (!string.IsNullOrWhiteSpace(configured))
        return AppendNpgsqlSslModeForDevContainers(configured);

    if (string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true",
            StringComparison.OrdinalIgnoreCase))
    {
        host = "db";
    }

    var fallbackPort = Environment.GetEnvironmentVariable("POSTGRES_DB_PORT") ?? "5432";
    var fallbackUsername = Environment.GetEnvironmentVariable("POSTGRES_USERNAME");
    var fallbackPassword = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? string.Empty;

    if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fallbackUsername))
    {
        throw new InvalidOperationException(
            "ScmDbConnection is not configured. Set ConnectionStrings:ScmDbConnection, " +
            "or POSTGRES_DB_HOST and POSTGRES_USERNAME (and POSTGRES_PASSWORD). " +
            "For Docker Compose, set host to your Postgres service name (often \"db\") or set ConnectionStrings__ScmDbConnection on the service.");
    }

    var csFallback = $"Host={host};Port={fallbackPort};Database=scm_db;Username={fallbackUsername};Password={fallbackPassword}";
    return AppendNpgsqlSslModeForDevContainers(csFallback);
}

static string AppendNpgsqlSslModeForDevContainers(string connectionString)
{
    if (string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true",
            StringComparison.OrdinalIgnoreCase)
        && connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase)
        && !connectionString.Contains("SSL Mode", StringComparison.OrdinalIgnoreCase)
        && !connectionString.Contains("Ssl Mode", StringComparison.OrdinalIgnoreCase))
    {
        return connectionString.TrimEnd(';') + ";SSL Mode=Disable";
    }

    return connectionString;
}

// Ensure database exists before running migrations
try
{
    var connectionString = ResolveScmConnectionString(builder.Configuration);
    var connBuilder = new NpgsqlConnectionStringBuilder(connectionString);
    var dbName = connBuilder.Database;
    connBuilder.Database = "postgres";
    
    using (var connection = new NpgsqlConnection(connBuilder.ConnectionString))
    {
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{dbName}\" TEMPLATE template0;";
        try 
        { 
            command.ExecuteNonQuery();
            Console.WriteLine($"✓ Database '{dbName}' created successfully.");
        } 
        catch (Exception ex)
        {
            Console.WriteLine($"ℹ Database '{dbName}' may already exist or error: {ex.Message}");
        }
    }
    
    // Give the database a moment to be ready
    System.Threading.Thread.Sleep(1000);
}
catch (Exception ex)
{
    Console.WriteLine($"⚠ Warning: Could not auto-create database: {ex.Message}");
}

builder.Services.AddControllers();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "R3B2P SCM API",
            Version = "v1",
            Description =
                "Supply chain management API (items, suppliers, inventory). " +
                "Use the same host/port you use to open Swagger when calling endpoints from \"Try it out\"."
        };
        return Task.CompletedTask;
    });
});

var useInMemory = string.Equals(Environment.GetEnvironmentVariable("USE_IN_MEMORY_DB"), "true", StringComparison.OrdinalIgnoreCase);

builder.Services.AddDbContext<ScmDbContext>(options =>
{
    if (useInMemory)
    {
        options.UseInMemoryDatabase("scm_db");
    }
    else
    {
        var connectionString = ResolveScmConnectionString(builder.Configuration);
        options.UseNpgsql(
            connectionString,
            npg => npg.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null));
    }
});

// Configure MongoDB
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));

builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MongoDbSettings>>().Value;
    var mongoUrl = new MongoUrl(settings.ConnectionString ?? "mongodb://localhost:27017");
    var clientSettings = MongoClientSettings.FromUrl(mongoUrl);
    clientSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(2);
    clientSettings.ConnectTimeout = TimeSpan.FromSeconds(2);
    return new MongoClient(clientSettings);
});

builder.Services.AddScoped<IMongoDatabase>(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MongoDbSettings>>().Value;
    return client.GetDatabase(settings.DatabaseName ?? "scms_analytics");
});

// Configure Redis (IDistributedCache)
var redisConnectionString = builder.Configuration.GetSection("RedisSettings:ConnectionString").Value ?? "localhost:6379,abortConnect=false";
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString + ",connectTimeout=1000,syncTimeout=1000";
    options.InstanceName = "scms_";
});

builder.Services.AddScoped<RedisCacheService>();
builder.Services.AddScoped<AnalyticsCompilerService>();

// Configure ML.NET
builder.Services.Configure<MLSettings>(
    builder.Configuration.GetSection("MLSettings"));

var mlModelPath = builder.Configuration.GetSection("MLSettings:ModelPath").Value;
if (!string.IsNullOrEmpty(mlModelPath) && File.Exists(mlModelPath))
{
    builder.Services.AddPredictionEnginePool<ModelInput, ModelOutput>()
        .FromFile(modelName: "RecommendationModel", filePath: mlModelPath, watchForChanges: true);
    builder.Services.AddScoped<PredictionService>();
}

builder.Services.AddScoped<ms_analytics.Infrastructure.ModelBuilder>();

builder.Services.AddScoped<IItemService, ItemService>();
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<IRecipeService, RecipeService>();
builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
builder.Services.AddScoped<IStockTransferService, StockTransferService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IFinishedProductService, FinishedProductService>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<IProductionService, ProductionService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddHostedService<ImageCleanupService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ScmDbContext>();
    var migrateLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Database");

    var useInMemoryDb = string.Equals(Environment.GetEnvironmentVariable("USE_IN_MEMORY_DB"), "true", StringComparison.OrdinalIgnoreCase);
    bool isDbReady = false;

    if (useInMemoryDb)
    {
        try
        {
            db.Database.EnsureCreated();
            migrateLogger.LogInformation("SCM InMemory database created.");
            isDbReady = true;
        }
        catch (Exception ex)
        {
            migrateLogger.LogError(ex, "Failed to initialize SCM InMemory database.");
        }
    }
    else
    {
        try
        {
            Console.WriteLine("→ Attempting to run database migrations...");
            db.Database.Migrate();
            migrateLogger.LogInformation("✓ SCM database migrations applied.");
            isDbReady = true;
        }
        catch (Exception ex)
        {
            migrateLogger.LogError(ex, "✗ Migrations failed, trying EnsureCreated()...");
            Console.WriteLine($"✗ Migrations failed: {ex.Message}");
            
            try
            {
                Console.WriteLine("→ Fallback: Running EnsureCreated()...");
                db.Database.EnsureCreated();
                migrateLogger.LogInformation("✓ Database schema created via EnsureCreated().");
                isDbReady = true;
            }
            catch (Exception ex2)
            {
                migrateLogger.LogError(ex2, "✗ EnsureCreated() also failed.");
                Console.WriteLine($"✗ EnsureCreated() failed: {ex2.Message}");
            }
        }
    }

    if (isDbReady)
    {
        try
        {
            db.Database.ExecuteSqlRaw(@"
                ALTER TABLE ""PurchaseOrders"" ADD COLUMN IF NOT EXISTS ""InspectedBy"" text;
                ALTER TABLE ""PurchaseOrders"" ADD COLUMN IF NOT EXISTS ""QaInspectedDate"" timestamp with time zone;
                ALTER TABLE ""PurchaseOrders"" ADD COLUMN IF NOT EXISTS ""QaNotes"" text;
                ALTER TABLE ""PurchaseOrders"" ADD COLUMN IF NOT EXISTS ""QaStatus"" text;
            ");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not alter PurchaseOrders table: {ex.Message}");
        }

        try
        {
            // Seed Master Data if empty
            if (!db.Categories.Any())
            {
                Console.WriteLine("→ Seeding Categories...");
                db.Categories.AddRange(
                    new Domains.Entities.Category { CategoryName = "Raw Materials", Description = "Raw materials for production" },
                    new Domains.Entities.Category { CategoryName = "Tools and Supplies", Description = "Tools and supplies used in operations" }
                );
                db.SaveChanges();
                migrateLogger.LogInformation("✓ Categories seeded successfully.");
                Console.WriteLine("✓ Categories seeded.");
            }

            // Ensure all target Unit of Measures exist in the database
            var existingUoms = db.UnitOfMeasures.ToList();
            var targetUoms = new List<Domains.Entities.UnitOfMeasure>
            {
                new() { Name = "Kilogram", Abbreviation = "kg" },
                new() { Name = "Piece", Abbreviation = "pcs" },
                new() { Name = "Litre", Abbreviation = "L" },
                new() { Name = "Meter", Abbreviation = "m" },
                new() { Name = "Gram", Abbreviation = "g" },
                new() { Name = "Box", Abbreviation = "box" },
                new() { Name = "Pack", Abbreviation = "pack" },
                new() { Name = "Roll", Abbreviation = "roll" },
                new() { Name = "Bottle", Abbreviation = "bottle" }
            };

            bool uomAdded = false;
            foreach (var uom in targetUoms)
            {
                if (!existingUoms.Any(u => u.Abbreviation.Equals(uom.Abbreviation, StringComparison.OrdinalIgnoreCase)))
                {
                    db.UnitOfMeasures.Add(uom);
                    uomAdded = true;
                }
            }

            if (uomAdded)
            {
                db.SaveChanges();
                migrateLogger.LogInformation("✓ Unit of Measures seeded successfully.");
                Console.WriteLine("✓ Unit of Measures seeded.");
            }

            if (!db.Locations.Any(l => l.LocationName == "Branch 1 - Quezon City"))
            {
                Console.WriteLine("→ Seeding Testing Locations...");
                db.Locations.Add(new Domains.Entities.Location { LocationName = "Branch 1 - Quezon City", LocationType = "Branch", Status = "Active" });
                db.Locations.Add(new Domains.Entities.Location { LocationName = "Branch 2 - Makati", LocationType = "Branch", Status = "Active" });
                db.Locations.Add(new Domains.Entities.Location { LocationName = "Bazaar Booth - SM North", LocationType = "Bazaar", Status = "Active" });
                db.SaveChanges();
                migrateLogger.LogInformation("✓ Testing Locations seeded successfully.");
                Console.WriteLine("✓ Testing Locations seeded.");
            }

            if (!db.Drivers.Any())
            {
                Console.WriteLine("→ Seeding Drivers...");
                db.Drivers.Add(new Domains.Entities.Driver { DriverName = "Default Driver", Number = "DRV-001" });
                db.SaveChanges();
                migrateLogger.LogInformation("✓ Drivers seeded successfully.");
                Console.WriteLine("✓ Drivers seeded.");
            }

            // Quick fix for existing Finished Products
            var finishedGoodCategory = db.Categories.FirstOrDefault(c => c.CategoryName.ToLower().Contains("finished good"));
            if (finishedGoodCategory == null)
            {
                finishedGoodCategory = new Domains.Entities.Category { CategoryName = "Finished Good", Description = "Finished Goods" };
                db.Categories.Add(finishedGoodCategory);
                db.SaveChanges();
            }
            var finishedProductsItems = db.FinishedProducts.Include(fp => fp.Item).ToList();
            foreach (var fp in finishedProductsItems)
            {
                if (fp.Item != null && fp.Item.CategoryId != finishedGoodCategory.CategoryId)
                {
                    fp.Item.CategoryId = finishedGoodCategory.CategoryId;
                }
            }
            db.SaveChanges();
            
            // Quick fix: Remove any inventory from non-Commissary locations (Branches)
            var branchInventories = db.Inventories
                .Include(i => i.Location)
                .Where(i => i.Location != null && !i.Location.LocationName.ToLower().Contains("commissary"))
                .ToList();
            if (branchInventories.Any())
            {
                db.Inventories.RemoveRange(branchInventories);
                db.SaveChanges();
            }

            // Quick fix to merge duplicate inventories in Commissary
            var duplicates = db.Inventories
                .AsEnumerable()
                .GroupBy(i => new { i.ItemId, i.LocationId })
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var group in duplicates)
            {
                var keep = group.First();
                var toRemove = group.Skip(1).ToList();
                foreach (var dup in toRemove)
                {
                    keep.CurrentStock += dup.CurrentStock;
                    db.Inventories.Remove(dup);
                }
            }
            db.SaveChanges();

            if (!db.AuditLogs.Any())
            {
                Console.WriteLine("→ Seeding PostgreSQL AuditLogs table...");
                db.AuditLogs.AddRange(
                    new Domains.Entities.AuditLog
                    {
                        EntityName = "Supply",
                        EntityId = "Ube Yam 50kg",
                        Action = "Initial Raw Material Stock Onboarded",
                        FieldName = "Inventory Specialist",
                        OldValue = "0",
                        NewValue = "50",
                        Timestamp = DateTime.UtcNow.AddHours(-18),
                        UserId = 1
                    },
                    new Domains.Entities.AuditLog
                    {
                        EntityName = "Supply",
                        EntityId = "White Sugar 100kg",
                        Action = "Restock Purchase Received & Verified",
                        FieldName = "Warehouse Admin",
                        OldValue = "20",
                        NewValue = "120",
                        Timestamp = DateTime.UtcNow.AddHours(-6),
                        UserId = 1
                    },
                    new Domains.Entities.AuditLog
                    {
                        EntityName = "Recipe",
                        EntityId = "Ube Jam 500g Standard Batch",
                        Action = "Production Recipe Version 1.0 Approved",
                        FieldName = "Head Pastry Chef",
                        OldValue = "Draft",
                        NewValue = "Active",
                        Timestamp = DateTime.UtcNow.AddDays(-1),
                        UserId = 1
                    },
                    new Domains.Entities.AuditLog
                    {
                        EntityName = "Recipe",
                        EntityId = "Pan de Sal 20pc Pack",
                        Action = "Ingredient BOM Ratio Calibrated",
                        FieldName = "Production Supervisor",
                        OldValue = "1.8kg flour",
                        NewValue = "2.0kg flour",
                        Timestamp = DateTime.UtcNow.AddHours(-10),
                        UserId = 1
                    },
                    new Domains.Entities.AuditLog
                    {
                        EntityName = "Supplier",
                        EntityId = "Batangas Flour Corporation",
                        Action = "Vendor Quality Verification Passed",
                        FieldName = "Quality Lead",
                        OldValue = "Pending Inspection",
                        NewValue = "Grade A Approved",
                        Timestamp = DateTime.UtcNow.AddDays(-2),
                        UserId = 1
                    }
                );
                db.SaveChanges();
                Console.WriteLine("✓ PostgreSQL AuditLogs seeded.");
            }

            Console.WriteLine("✓ All database initialization completed successfully!");
        }
        catch (Exception ex)
        {
            migrateLogger.LogError(ex, "✗ Failed to seed SCM master data.");
            Console.WriteLine($"✗ Seeding failed: {ex.Message}");
        }
    }
}

// app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseStaticFiles();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUi(options =>
    {
        options.DocumentPath = "/openapi/v1.json";
    });
}

app.MapControllers();

app.Run();
