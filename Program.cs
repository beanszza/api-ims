using Applications.Interfaces;
using Applications.Services;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
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

builder.Services.AddScoped<IItemService, ItemService>();
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<IRecipeService, RecipeService>();
builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
builder.Services.AddScoped<IStockTransferService, StockTransferService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IFinishedProductService, FinishedProductService>();
builder.Services.AddScoped<ILocationService, LocationService>();

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

            // Seed a test Item and FinishedProduct so you can test Recipes
            if (!db.FinishedProducts.Any())
            {
                Console.WriteLine("→ Seeding Test Finished Product...");
                var testItem = new Domains.Entities.Item
                {
                    ItemName = "Test Final Product",
                    UomId = 2, // pcs
                    CategoryId = 1, // Raw Materials (or Finished Goods if you had it)
                    MinStockLevel = 0,
                    MaxStockLevel = 100,
                    IsActive = true
                };
                db.Items.Add(testItem);
                db.SaveChanges(); // get ItemId

                db.FinishedProducts.Add(new Domains.Entities.FinishedProduct
                {
                    ItemId = testItem.ItemId,
                    SellingPrice = 15.50m,
                    Sku = "SKU-TEST-01"
                });
                db.SaveChanges();

                // Seed some inventory at Commissary Kitchen (Location 1) for testing
                db.Inventories.Add(new Domains.Entities.Inventory { ItemId = testItem.ItemId, LocationId = 1, CurrentStock = 1000 });
                if (db.Items.Any(i => i.ItemId == 9)) db.Inventories.Add(new Domains.Entities.Inventory { ItemId = 9, LocationId = 1, CurrentStock = 1000 }); // Ube Halaya
                if (db.Items.Any(i => i.ItemId == 10)) db.Inventories.Add(new Domains.Entities.Inventory { ItemId = 10, LocationId = 1, CurrentStock = 1000 }); // Ube Jam
                db.SaveChanges();

                migrateLogger.LogInformation("✓ Test Finished Product seeded successfully.");
                Console.WriteLine("✓ Test Finished Product seeded.");
            }

            // Unconditionally seed inventory for testing Stock Transfers
            if (!db.Inventories.Any(i => i.LocationId == 1 && i.ItemId == 9))
            {
                db.Inventories.Add(new Domains.Entities.Inventory { ItemId = 9, LocationId = 1, DriverId = 1, CurrentStock = 5000 }); // Ube Halaya
                db.Inventories.Add(new Domains.Entities.Inventory { ItemId = 10, LocationId = 1, DriverId = 1, CurrentStock = 5000 }); // Ube Jam
                db.Inventories.Add(new Domains.Entities.Inventory { ItemId = 2, LocationId = 1, DriverId = 1, CurrentStock = 5000 }); // Test Product
                db.SaveChanges();
            }

            if (!db.FinishedProducts.Any(fp => fp.Item != null && fp.Item.ItemName == "Ube Halaya"))
            {
                Console.WriteLine("→ Seeding Ube Halaya Finished Product...");
                var ubeHalayaItem = new Domains.Entities.Item
                {
                    ItemName = "Ube Halaya",
                    UomId = 2, // pcs
                    CategoryId = 1,
                    MinStockLevel = 0,
                    MaxStockLevel = 100,
                    IsActive = true
                };
                db.Items.Add(ubeHalayaItem);
                db.SaveChanges();

                db.FinishedProducts.Add(new Domains.Entities.FinishedProduct
                {
                    ItemId = ubeHalayaItem.ItemId,
                    SellingPrice = 150.00m,
                    Sku = "UBE-HALAYA-001"
                });
                db.SaveChanges();
                migrateLogger.LogInformation("✓ Ube Halaya Finished Product seeded successfully.");
                Console.WriteLine("✓ Ube Halaya Finished Product seeded.");
            }

            if (!db.FinishedProducts.Any(fp => fp.Item != null && fp.Item.ItemName == "Ube Jam"))
            {
                Console.WriteLine("→ Seeding Ube Jam Finished Product...");
                var ubeJamItem = new Domains.Entities.Item
                {
                    ItemName = "Ube Jam",
                    UomId = 2, // pcs
                    CategoryId = 1,
                    MinStockLevel = 0,
                    MaxStockLevel = 100,
                    IsActive = true
                };
                db.Items.Add(ubeJamItem);
                db.SaveChanges();

                db.FinishedProducts.Add(new Domains.Entities.FinishedProduct
                {
                    ItemId = ubeJamItem.ItemId,
                    SellingPrice = 120.00m,
                    Sku = "UBE-JAM-001"
                });
                db.SaveChanges();
                migrateLogger.LogInformation("✓ Ube Jam Finished Product seeded successfully.");
                Console.WriteLine("✓ Ube Jam Finished Product seeded.");
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
