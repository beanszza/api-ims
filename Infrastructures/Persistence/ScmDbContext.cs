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
    public DbSet<InventoryLot> InventoryLots { get; set; }
    public DbSet<StockLedger> StockLedgers { get; set; }
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
        ConfigureInventoryLots(modelBuilder);
        ConfigureStockLedger(modelBuilder);
    }

    /// <summary>
    /// Configures the append-only stock ledger.
    /// </summary>
    private static void ConfigureStockLedger(ModelBuilder modelBuilder)
    {
        var ledger = modelBuilder.Entity<StockLedger>();

        ledger.HasKey(l => l.LedgerId);

        // The reconciliation query: every row for one lot.
        ledger.HasIndex(l => l.LotId).HasDatabaseName("IX_StockLedgers_LotId");

        // Movement history for an item at a location, newest first.
        ledger.HasIndex(l => new { l.ItemId, l.LocationId, l.PostedAt })
            .HasDatabaseName("IX_StockLedgers_ItemLocationPostedAt");

        // "Show me everything this document did", used by the trace report.
        ledger.HasIndex(l => new { l.ReferenceType, l.ReferenceId })
            .HasDatabaseName("IX_StockLedgers_Reference");

        ledger.Property(l => l.MovementType)
            .HasConversion(EnumTextConverter<MovementType>())
            .HasColumnType("text");

        ledger.Property(l => l.Quantity).HasPrecision(18, 3);
        ledger.Property(l => l.UnitCost).HasPrecision(18, 6);

        // A movement of zero explains nothing and would let a caller pretend to post something. The old
        // code wrote exactly such a row on transfer completion ("Transfer Completed (No Addition)").
        ledger.ToTable(t => t.HasCheckConstraint(
            "CK_StockLedgers_Quantity_NonZero", "\"Quantity\" <> 0"));

        // Restrict everywhere: the ledger is evidence, and deleting a lot must not delete the record of
        // what happened to it.
        ledger.HasOne(l => l.Lot).WithMany().HasForeignKey(l => l.LotId).OnDelete(DeleteBehavior.Restrict);
        ledger.HasOne(l => l.Item).WithMany().HasForeignKey(l => l.ItemId).OnDelete(DeleteBehavior.Restrict);
        ledger.HasOne(l => l.Location).WithMany().HasForeignKey(l => l.LocationId).OnDelete(DeleteBehavior.Restrict);

        ledger.HasOne(l => l.ReversalOf)
            .WithMany()
            .HasForeignKey(l => l.ReversalOfLedgerId)
            .OnDelete(DeleteBehavior.Restrict);

        // One reversal per row: reversing the same movement twice would double the correction.
        ledger.HasIndex(l => l.ReversalOfLedgerId)
            .IsUnique()
            .HasFilter("\"ReversalOfLedgerId\" IS NOT NULL")
            .HasDatabaseName("IX_StockLedgers_ReversalOf");
    }

    /// <summary>
    /// Configures the lot table: identity, traceability links, and the invariants stock must obey.
    /// </summary>
    /// <remarks>
    /// The check constraints matter more than they look. They make it impossible for any code path -
    /// including one written years from now by someone who has not read the posting service - to drive a
    /// lot negative or hand out more than was received. Enforcing that in C# alone would mean trusting
    /// every future caller.
    /// </remarks>
    private static void ConfigureInventoryLots(ModelBuilder modelBuilder)
    {
        var lot = modelBuilder.Entity<InventoryLot>();

        lot.HasKey(l => l.LotId);

        // A lot code is a business identifier people quote, so it must be unique per location.
        // It is NOT globally unique: a transfer deliberately creates a second InventoryLot row carrying
        // the same code at the destination, because it is the same physical batch, just now split across
        // two places. Uniqueness per (LotCode, LocationId) still catches an accidental duplicate receipt
        // while allowing the one-batch-many-locations shape a transfer produces.
        lot.HasIndex(l => new { l.LotCode, l.LocationId })
            .IsUnique()
            .HasDatabaseName("IX_InventoryLots_LotCode_LocationId");

        // The query the allocation engine runs constantly: available stock of an item at a location.
        lot.HasIndex(l => new { l.ItemId, l.LocationId, l.Status })
            .HasDatabaseName("IX_InventoryLots_ItemLocationStatus");

        // Drives the expiry sweep and the FEFO ordering.
        lot.HasIndex(l => l.ExpiryDate).HasDatabaseName("IX_InventoryLots_ExpiryDate");

        // Backward tracing: every lot a supplier ever sent us.
        lot.HasIndex(l => l.SupplierId).HasDatabaseName("IX_InventoryLots_SupplierId");

        lot.Property(l => l.Status)
            .HasConversion(EnumTextConverter<LotStatus>())
            .HasColumnType("text");

        lot.Property(l => l.SourceType)
            .HasConversion(EnumTextConverter<LotSourceType>())
            .HasColumnType("text");

        lot.Property(l => l.QuantityReceived).HasPrecision(18, 3);
        lot.Property(l => l.QuantityRemaining).HasPrecision(18, 3);

        // Cost carries more scale than quantity: a per-gram cost of a bulk purchase is a small number.
        lot.Property(l => l.UnitCost).HasPrecision(18, 6);

        lot.HasOne(l => l.Item).WithMany().HasForeignKey(l => l.ItemId).OnDelete(DeleteBehavior.Restrict);
        lot.HasOne(l => l.Location).WithMany().HasForeignKey(l => l.LocationId).OnDelete(DeleteBehavior.Restrict);
        lot.HasOne(l => l.Uom).WithMany().HasForeignKey(l => l.UomId).OnDelete(DeleteBehavior.Restrict);

        // Restrict, not Cascade: deleting a supplier must never delete the evidence of what they sent.
        lot.HasOne(l => l.Supplier).WithMany().HasForeignKey(l => l.SupplierId).OnDelete(DeleteBehavior.Restrict);

        lot.ToTable(t =>
        {
            t.HasCheckConstraint("CK_InventoryLots_QuantityReceived_Positive",
                "\"QuantityReceived\" > 0");

            t.HasCheckConstraint("CK_InventoryLots_QuantityRemaining_NotNegative",
                "\"QuantityRemaining\" >= 0");

            t.HasCheckConstraint("CK_InventoryLots_QuantityRemaining_WithinReceived",
                "\"QuantityRemaining\" <= \"QuantityReceived\"");

            // A purchased lot without a supplier cannot be traced, which defeats the purpose.
            t.HasCheckConstraint("CK_InventoryLots_PurchasedHasSupplier",
                "\"SourceType\" <> 'Purchased' OR \"SupplierId\" IS NOT NULL");
        });

        // Same optimistic concurrency approach as Inventory: two simultaneous draws on one lot must not
        // both succeed against the same starting quantity.
        lot.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
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
        // A location's parent, for a bay inside a warehouse or a branch's in-transit lane. Restrict so a
        // parent cannot be removed while children still reference it.
        modelBuilder.Entity<Location>()
            .HasOne(l => l.ParentLocation)
            .WithMany()
            .HasForeignKey(l => l.ParentLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        // Only one system location per singleton role, so resolving a role always has a single answer.
        // Two exclusions:
        //  - ordinary (non-system) locations are unconstrained, so there can be many branches;
        //  - In Transit is excluded because it is inherently per-destination: every branch needs its own
        //    lane, so "one per role" is the wrong shape for it.
        modelBuilder.Entity<Location>()
            .HasIndex(l => l.LocationType)
            .IsUnique()
            .HasFilter("\"IsSystemLocation\" = true AND \"LocationType\" <> 'In Transit'")
            .HasDatabaseName("IX_Locations_SystemRole");

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

        modelBuilder.Entity<Location>()
            .Property(e => e.LocationType)
            .HasConversion(EnumTextConverter<LocationType>())
            .HasColumnType("text");

        // InventoryMovementLog.ActionType stays a string for now: its stored values are ad hoc and need
        // a normalising data migration first, which happens in Task 9.
    }

    private static ValueConverter<TEnum, string> EnumTextConverter<TEnum>() where TEnum : struct, Enum
        => new(
            value => EnumDbValue.ToDbValue(value),
            stored => EnumDbValue.Parse<TEnum>(stored));
}