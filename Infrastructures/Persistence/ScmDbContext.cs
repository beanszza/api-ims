using Microsoft.EntityFrameworkCore;
using Domains.Entities;

namespace Infrastructures.Persistence;

public class ScmDbContext : DbContext
{
    public ScmDbContext(DbContextOptions<ScmDbContext> options) : base(options)
    {
    }

    // --- MASTER DATA ---
    public DbSet<UnitOfMeasure> UnitOfMeasures { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Item> Items { get; set; } 
    public DbSet<FinishedProduct> FinishedProducts { get; set; }

    // --- PURCHASING & SUPPLIERS ---
    public DbSet<Supplier> Suppliers { get; set; }
    public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
    public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }

    // --- PRODUCTION & MANUFACTURING ---
    public DbSet<Recipe> Recipes { get; set; }
    public DbSet<RecipeIngredient> RecipeIngredients { get; set; }
    public DbSet<ProductionBatch> ProductionBatches { get; set; }
    public DbSet<BatchConsumption> BatchConsumptions { get; set; }

    // --- INVENTORY & LOGISTICS ---
    public DbSet<Location> Locations { get; set; }
    public DbSet<Driver> Drivers { get; set; }
    public DbSet<Inventory> Inventories { get; set; }
    public DbSet<StockTransfer> StockTransfers { get; set; }
    public DbSet<InventoryMovementLog> InventoryMovementLogs { get; set; }

    // --- LOGS & OTHERS ---
    public DbSet<AuditLog> AuditLogs { get; set; }


    // --- CONFIGURATION ---
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Primary keys do not match the default "{EntityName}Id" convention for these types.
        modelBuilder.Entity<AuditLog>().HasKey(e => e.LogId);
        modelBuilder.Entity<FinishedProduct>().HasKey(e => e.ProductId);
        modelBuilder.Entity<UnitOfMeasure>().HasKey(e => e.UomId);
        modelBuilder.Entity<ProductionBatch>().HasKey(e => e.BatchId);
        modelBuilder.Entity<InventoryMovementLog>().HasKey(e => e.MovementId);
        modelBuilder.Entity<PurchaseOrder>().HasKey(e => e.PoId);
        modelBuilder.Entity<PurchaseOrderItem>().HasKey(e => e.PoItemId);
        modelBuilder.Entity<RecipeIngredient>().HasKey(e => e.IngredientId);
        modelBuilder.Entity<StockTransfer>().HasKey(e => e.TransferId);

        // Tell EF Core how to handle the TWO Location foreign keys in StockTransfer
        // We use DeleteBehavior.Restrict so deleting a location doesn't accidentally wipe out transfer history.
        modelBuilder.Entity<StockTransfer>()
            .HasOne(st => st.SourceLocation)
            .WithMany()
            .HasForeignKey(st => st.SourceLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockTransfer>()
            .HasOne(st => st.DestLocation)
            .WithMany()
            .HasForeignKey(st => st.DestLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        // SourceSupplier navigation name is not "Supplier"; wire inverse collection explicitly.
        modelBuilder.Entity<BatchConsumption>()
            .HasOne(b => b.SourceSupplier)
            .WithMany(s => s.SourcedBatchConsumptions)
            .HasForeignKey(b => b.SourceSupplierId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}