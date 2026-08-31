using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Domains.Entities;
using Domains.Enums;

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
    public DbSet<DocumentSequence> DocumentSequences { get; set; }


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

        ConfigureUnitsOfMeasure(modelBuilder);
        ConfigureEnumConversions(modelBuilder);
        ConfigureQuantityPrecision(modelBuilder);
        ConfigureDocumentNumbering(modelBuilder);
        ConfigureStockIntegrity(modelBuilder);
    }

    /// <summary>
    /// One sequence row per document type and year, and a unique document number per purchase order.
    /// </summary>
    private static void ConfigureDocumentNumbering(ModelBuilder modelBuilder)
    {
        // The unique index is what makes the allocating upsert safe: it is the conflict target.
        modelBuilder.Entity<DocumentSequence>()
            .HasIndex(s => new { s.DocType, s.Year })
            .IsUnique();

        modelBuilder.Entity<PurchaseOrder>()
            .HasIndex(o => o.PoNumber)
            .IsUnique();
    }

    /// <summary>
    /// Structural guarantees for stock balances.
    /// </summary>
    /// <remarks>
    /// Two protections that the code alone could not provide:
    /// <list type="bullet">
    ///   <item>A unique index on (ItemId, LocationId), so the same item cannot hold two balance rows at
    ///   one location. Duplicates were previously possible, and the application startup routine had a
    ///   recurring "quick fix" that merged them on every boot - a workaround for a missing constraint.</item>
    ///   <item>An optimistic concurrency token, so two simultaneous deductions cannot both read the same
    ///   balance and each write their own result, losing one of the movements.</item>
    /// </list>
    /// PostgreSQL's system column <c>xmin</c> is used as the token rather than adding a physical
    /// RowVersion column: it already changes on every update, so it needs no schema change and cannot
    /// be forgotten by code that writes the row.
    /// </remarks>
    private static void ConfigureStockIntegrity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Inventory>()
            .HasIndex(i => new { i.ItemId, i.LocationId })
            .IsUnique();

        // xmin is a PostgreSQL system column present on every table, so it is mapped as a concurrency
        // token but deliberately excluded from migrations: there is no column to create.
        modelBuilder.Entity<Inventory>()
            .Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }

    /// <summary>
    /// Wires up unit conversion: a unique code, the dimension, and the link to each dimension's base unit.
    /// </summary>
    private static void ConfigureUnitsOfMeasure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UnitOfMeasure>()
            .HasIndex(u => u.Code)
            .IsUnique();

        modelBuilder.Entity<UnitOfMeasure>()
            .Property(u => u.UomType)
            .HasConversion(EnumTextConverter<UomType>())
            .HasColumnType("text");

        // Conversion factors need more headroom than stock quantities: a milligram expressed in
        // kilograms is 0.000001.
        modelBuilder.Entity<UnitOfMeasure>()
            .Property(u => u.ConversionFactor)
            .HasPrecision(18, 9);

        // Self reference to the dimension's base unit. Restrict so removing a base unit cannot
        // silently orphan the units that depend on it for conversion.
        modelBuilder.Entity<UnitOfMeasure>()
            .HasOne(u => u.BaseUom)
            .WithMany()
            .HasForeignKey(u => u.BaseUomId)
            .OnDelete(DeleteBehavior.Restrict);

        // Item.UomId (display) and Item.StockUomId (authoritative) both point at UnitOfMeasure, so
        // the two relationships must be declared explicitly.
        modelBuilder.Entity<Item>()
            .HasOne(i => i.Uom)
            .WithMany(u => u.Items)
            .HasForeignKey(i => i.UomId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Item>()
            .HasOne(i => i.StockUom)
            .WithMany()
            .HasForeignKey(i => i.StockUomId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    /// <summary>
    /// Pins every stock quantity to <c>numeric(18,3)</c>.
    /// </summary>
    /// <remarks>
    /// Declared in one place so precision cannot drift between the balance, the ledger that explains
    /// it, and the documents that move it. Three decimals is chosen to express grams as kilograms and
    /// millilitres as litres exactly, which is the finest granularity the recipes need.
    /// <para>
    /// Money is deliberately not included: <c>TotalAmount</c> and <c>SellingPrice</c> are currency and
    /// get their own scale when costing arrives in Task 43.
    /// </para>
    /// </remarks>
    private static void ConfigureQuantityPrecision(ModelBuilder modelBuilder)
    {
        const int precision = 18;
        const int scale = 3;

        modelBuilder.Entity<Inventory>().Property(e => e.CurrentStock).HasPrecision(precision, scale);

        modelBuilder.Entity<Item>().Property(e => e.MinStockLevel).HasPrecision(precision, scale);
        modelBuilder.Entity<Item>().Property(e => e.MaxStockLevel).HasPrecision(precision, scale);

        modelBuilder.Entity<RecipeIngredient>().Property(e => e.StandardQuantity).HasPrecision(precision, scale);

        modelBuilder.Entity<PurchaseOrderItem>().Property(e => e.PoItemQuantity).HasPrecision(precision, scale);
        modelBuilder.Entity<PurchaseOrderItem>().Property(e => e.ReceivedQuantity).HasPrecision(precision, scale);

        modelBuilder.Entity<ProductionBatch>().Property(e => e.BatchMultiplier).HasPrecision(precision, scale);
        modelBuilder.Entity<ProductionBatch>().Property(e => e.EstimatedQuantity).HasPrecision(precision, scale);
        modelBuilder.Entity<ProductionBatch>().Property(e => e.ActualQuantity).HasPrecision(precision, scale);

        modelBuilder.Entity<BatchConsumption>().Property(e => e.RequiredQuantity).HasPrecision(precision, scale);

        modelBuilder.Entity<StockTransfer>().Property(e => e.TransferQuantity).HasPrecision(precision, scale);

        modelBuilder.Entity<InventoryMovementLog>().Property(e => e.ChangeQuantity).HasPrecision(precision, scale);
    }

    /// <summary>
    /// Stores status enums as their legacy text values.
    /// </summary>
    /// <remarks>
    /// The converters reproduce the exact strings the columns already hold, so introducing enums
    /// needs no data migration and no frontend change. Read is tolerant of historical spellings via
    /// <see cref="DbValueAttribute"/> aliases, and unknown values throw rather than being coerced,
    /// because a status the system cannot interpret is a data fault worth surfacing.
    /// </remarks>
    private static void ConfigureEnumConversions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PurchaseOrder>()
            .Property(e => e.Status)
            .HasConversion(EnumTextConverter<PurchaseOrderStatus>())
            .HasColumnType("text");

        modelBuilder.Entity<ProductionBatch>()
            .Property(e => e.Status)
            .HasConversion(EnumTextConverter<BatchStatus>())
            .HasColumnType("text");

        modelBuilder.Entity<ProductionBatch>()
            .Property(e => e.Stage)
            .HasConversion(EnumTextConverter<ProductionStage>())
            .HasColumnType("text");

        modelBuilder.Entity<ProductionBatch>()
            .Property(e => e.QualityStatus)
            .HasConversion(EnumTextConverter<QcStatus>())
            .HasColumnType("text");

        modelBuilder.Entity<StockTransfer>()
            .Property(e => e.Status)
            .HasConversion(EnumTextConverter<ShipmentStatus>())
            .HasColumnType("text");

        // Location.LocationType and InventoryMovementLog.ActionType stay as strings for now: their
        // stored values are ad hoc and need a normalising data migration first, which happens in
        // Task 7 and Task 9 respectively.
    }

    private static ValueConverter<TEnum, string> EnumTextConverter<TEnum>() where TEnum : struct, Enum
        => new(
            value => EnumDbValue.ToDbValue(value),
            stored => EnumDbValue.Parse<TEnum>(stored));
}