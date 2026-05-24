using Applications.Interfaces;
using Applications.Services;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

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
            db.Database.Migrate();
            migrateLogger.LogInformation("SCM database migrations applied.");
            isDbReady = true;
        }
        catch (Exception ex)
        {
            migrateLogger.LogError(ex,
                "SCM database Migrate() failed. API will start; fix the connection and restart, or run migrations manually.");
        }
    }

    if (isDbReady)
    {
        try
        {
            // Seed Master Data if empty
            if (!db.Categories.Any())
            {
                db.Categories.AddRange(
                    new Domains.Entities.Category { CategoryName = "Raw Materials", Description = "Raw materials for production" },
                    new Domains.Entities.Category { CategoryName = "Tools and Supplies", Description = "Tools and supplies used in operations" }
                );
                db.SaveChanges();
                migrateLogger.LogInformation("Categories seeded successfully.");
            }

            if (!db.UnitOfMeasures.Any())
            {
                db.UnitOfMeasures.AddRange(
                    new Domains.Entities.UnitOfMeasure { Name = "Kilogram", Abbreviation = "kg" },
                    new Domains.Entities.UnitOfMeasure { Name = "Piece", Abbreviation = "pcs" },
                    new Domains.Entities.UnitOfMeasure { Name = "Litre", Abbreviation = "L" },
                    new Domains.Entities.UnitOfMeasure { Name = "Meter", Abbreviation = "m" }
                );
                db.SaveChanges();
                migrateLogger.LogInformation("Unit of Measures seeded successfully.");
            }

            if (!db.Locations.Any())
            {
                db.Locations.Add(new Domains.Entities.Location { LocationName = "Main Warehouse", LocationType = "Storage" });
                db.SaveChanges();
                migrateLogger.LogInformation("Locations seeded successfully.");
            }

            if (!db.Drivers.Any())
            {
                db.Drivers.Add(new Domains.Entities.Driver { DriverName = "Default Driver", Number = "DRV-001" });
                db.SaveChanges();
                migrateLogger.LogInformation("Drivers seeded successfully.");
            }
        }
        catch (Exception ex)
        {
            migrateLogger.LogError(ex, "Failed to seed SCM master data.");
        }
    }
}

// app.UseHttpsRedirection();
app.UseCors("AllowAll");
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
