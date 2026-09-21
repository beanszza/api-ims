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
    public DbSet<SupplierItem> SupplierItems { get; set; }
    public DbSet<SupplierDocument> SupplierDocuments { get; set; }
    public DbSet<ApprovalRequest> ApprovalRequests { get; set; }
    public DbSet<PurchaseRequisition> PurchaseRequisitions { get; set; }
    public DbSet<PurchaseRequisitionItem> PurchaseRequisitionItems { get; set; }
    public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
    public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }
    public DbSet<Delivery> Deliveries { get; set; }
    public DbSet<DeliveryItem> DeliveryItems { get; set; }
    public DbSet<GoodsReceipt> GoodsReceipts { get; set; }
    public DbSet<GoodsReceiptItem> GoodsReceiptItems { get; set; }
    public DbSet<QualityInspection> QualityInspections { get; set; }
    public DbSet<QualityInspectionItem> QualityInspectionItems { get; set; }
    public DbSet<NonConformanceReport> NonConformanceReports { get; set; }
    public DbSet<ReturnToVendor> ReturnToVendors { get; set; }
    public DbSet<Discrepancy> Discrepancies { get; set; }
    public DbSet<PutAwayTransaction> PutAwayTransactions { get; set; }
    public DbSet<LossReport> LossReports { get; set; }

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
    public DbSet<DisposalRecord> DisposalRecords { get; set; }
    public DbSet<DisposalRecordItem> DisposalRecordItems { get; set; }
    public DbSet<BranchRequest> BranchRequests { get; set; }
    public DbSet<BranchRequestItem> BranchRequestItems { get; set; }
    public DbSet<BranchReturn> BranchReturns { get; set; }
    public DbSet<BranchReturnItem> BranchReturnItems { get; set; }
    public DbSet<RecallRecord> RecallRecords { get; set; }
    public DbSet<CycleCount> CycleCounts { get; set; }
    public DbSet<CycleCountItem> CycleCountItems { get; set; }

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
        modelBuilder.Entity<Delivery>().HasKey(e => e.DeliveryId);
        modelBuilder.Entity<DeliveryItem>().HasKey(e => e.DeliveryItemId);
        modelBuilder.Entity<RecipeIngredient>().HasKey(e => e.IngredientId);
        modelBuilder.Entity<BatchConsumption>().HasKey(e => e.BatchConsumptionId);
        modelBuilder.Entity<StockTransfer>().HasKey(e => e.TransferId);

        modelBuilder.Entity<BatchConsumption>()
            .HasOne(b => b.Lot)
            .WithMany()
            .HasForeignKey(b => b.LotId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<BatchConsumption>()
            .HasOne(b => b.Uom)
            .WithMany()
            .HasForeignKey(b => b.UomId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProductionBatch>()
            .HasOne(b => b.FgLot)
            .WithMany()
            .HasForeignKey(b => b.FgLotId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Recipe>()
            .HasOne(r => r.YieldUom)
            .WithMany()
            .HasForeignKey(r => r.YieldUomId)
            .OnDelete(DeleteBehavior.Restrict);

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
        ConfigureSupplierItems(modelBuilder);
        ConfigureSupplierDocuments(modelBuilder);
        ConfigureApprovalRequests(modelBuilder);
        ConfigurePurchaseRequisitions(modelBuilder);
        ConfigureDeliveries(modelBuilder);
        ConfigureGoodsReceipts(modelBuilder);
        ConfigureQualityInspections(modelBuilder);
        ConfigureNonConformanceReports(modelBuilder);
        ConfigureReturnToVendors(modelBuilder);
        ConfigureDiscrepancies(modelBuilder);
        ConfigurePutAwayTransactions(modelBuilder);
        ConfigureLossReports(modelBuilder);
        ConfigureDisposalRecords(modelBuilder);
        ConfigureBranchDistribution(modelBuilder);
        ConfigureRecallRecords(modelBuilder);
        ConfigureCycleCounts(modelBuilder);
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

        // From Task 10, CurrentStock is a cached projection of SUM(InventoryLots.QuantityRemaining)
        // WHERE Status = 'Available', maintained only by IStockPostingService. This constraint is the
        // same defence the lot table already has on QuantityRemaining: the cache disagreeing with the
        // ledger it summarises should fail loudly, not store a negative number nobody asked for.
        modelBuilder.Entity<Inventory>().ToTable(t => t.HasCheckConstraint(
            "CK_Inventories_CurrentStock_NotNegative", "\"CurrentStock\" >= 0"));
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

        modelBuilder.Entity<Recipe>().Property(e => e.OutputQuantity).HasPrecision(precision, scale);

        modelBuilder.Entity<PurchaseOrderItem>().Property(e => e.PoItemQuantity).HasPrecision(precision, scale);
        modelBuilder.Entity<PurchaseOrderItem>().Property(e => e.ReceivedQuantity).HasPrecision(precision, scale);
        modelBuilder.Entity<PurchaseOrderItem>().Property(e => e.TotalPrice).HasPrecision(18, 4);
        modelBuilder.Entity<PurchaseOrderItem>().Ignore(e => e.UnitPrice);
        modelBuilder.Entity<PurchaseOrderItem>().Ignore(e => e.LineTotal);

        modelBuilder.Entity<PurchaseOrder>().Property(e => e.TotalAmount).HasPrecision(18, 4);

        modelBuilder.Entity<ApprovalRequest>().Property(e => e.Amount).HasPrecision(18, 4);

        modelBuilder.Entity<PurchaseRequisition>().Property(e => e.EstimatedTotalAmount).HasPrecision(18, 4);
        modelBuilder.Entity<PurchaseRequisitionItem>().Property(e => e.RequestedQuantity).HasPrecision(precision, scale);
        modelBuilder.Entity<PurchaseRequisitionItem>().Property(e => e.EstimatedUnitPrice).HasPrecision(18, 4);

        modelBuilder.Entity<GoodsReceiptItem>().Property(e => e.OrderedQuantity).HasPrecision(precision, scale);
        modelBuilder.Entity<GoodsReceiptItem>().Property(e => e.PreviouslyReceivedQuantity).HasPrecision(precision, scale);
        modelBuilder.Entity<GoodsReceiptItem>().Property(e => e.DeclaredQuantity).HasPrecision(precision, scale);
        modelBuilder.Entity<GoodsReceiptItem>().Property(e => e.DeliveredQuantity).HasPrecision(precision, scale);
        modelBuilder.Entity<GoodsReceiptItem>().Property(e => e.VarianceQuantity).HasPrecision(precision, scale);

        modelBuilder.Entity<QualityInspection>().Property(e => e.TotalReceivedQuantity).HasPrecision(precision, scale);
        modelBuilder.Entity<QualityInspection>().Property(e => e.TotalAcceptedQuantity).HasPrecision(precision, scale);
        modelBuilder.Entity<QualityInspection>().Property(e => e.TotalRejectedQuantity).HasPrecision(precision, scale);

        modelBuilder.Entity<QualityInspectionItem>().Property(e => e.DeliveredQuantity).HasPrecision(precision, scale);
        modelBuilder.Entity<QualityInspectionItem>().Property(e => e.AcceptedQuantity).HasPrecision(precision, scale);
        modelBuilder.Entity<QualityInspectionItem>().Property(e => e.RejectedQuantity).HasPrecision(precision, scale);
        modelBuilder.Entity<QualityInspectionItem>().Property(e => e.ConcessionQuantity).HasPrecision(precision, scale);

        modelBuilder.Entity<Discrepancy>().Property(e => e.OrderedQuantity).HasPrecision(precision, scale);
        modelBuilder.Entity<Discrepancy>().Property(e => e.PreviouslyReceivedQty).HasPrecision(precision, scale);
        modelBuilder.Entity<Discrepancy>().Property(e => e.CurrentReceivedQty).HasPrecision(precision, scale);
        modelBuilder.Entity<Discrepancy>().Property(e => e.DiscrepancyQuantity).HasPrecision(precision, scale);

        modelBuilder.Entity<PutAwayTransaction>().Property(e => e.AcceptedQuantity).HasPrecision(precision, scale);

        modelBuilder.Entity<LossReport>().Property(e => e.LostQuantity).HasPrecision(precision, scale);

        modelBuilder.Entity<NonConformanceReport>().Property(e => e.DefectiveQuantity).HasPrecision(precision, scale);

        modelBuilder.Entity<ReturnToVendor>().Property(e => e.ReturnedQuantity).HasPrecision(precision, scale);
        modelBuilder.Entity<DeliveryItem>().Property(e => e.DeclaredQuantity).HasPrecision(precision, scale);

        modelBuilder.Entity<DisposalRecord>().Property(e => e.TotalCost).HasPrecision(18, 4);
        modelBuilder.Entity<DisposalRecordItem>().Property(e => e.QuantityDisposed).HasPrecision(precision, scale);
        modelBuilder.Entity<DisposalRecordItem>().Property(e => e.UnitCost).HasPrecision(18, 4);

        modelBuilder.Entity<BranchRequestItem>().Property(e => e.RequestedQuantity).HasPrecision(precision, scale);
        modelBuilder.Entity<BranchRequestItem>().Property(e => e.ApprovedQuantity).HasPrecision(precision, scale);
        modelBuilder.Entity<BranchRequestItem>().Property(e => e.DispatchedQuantity).HasPrecision(precision, scale);
        modelBuilder.Entity<BranchRequestItem>().Property(e => e.ReceivedQuantity).HasPrecision(precision, scale);

        modelBuilder.Entity<BranchReturnItem>().Property(e => e.ReturnedQuantity).HasPrecision(precision, scale);

        modelBuilder.Entity<RecallRecord>().Property(e => e.TotalUnitsAffected).HasPrecision(precision, scale);

        modelBuilder.Entity<CycleCount>().Property(e => e.TotalSystemValue).HasPrecision(18, 4);
        modelBuilder.Entity<CycleCount>().Property(e => e.TotalCountedValue).HasPrecision(18, 4);
        modelBuilder.Entity<CycleCount>().Property(e => e.TotalVarianceValue).HasPrecision(18, 4);

        modelBuilder.Entity<CycleCountItem>().Property(e => e.SystemQuantity).HasPrecision(precision, scale);
        modelBuilder.Entity<CycleCountItem>().Property(e => e.CountedQuantity).HasPrecision(precision, scale);
        modelBuilder.Entity<CycleCountItem>().Property(e => e.UnitCost).HasPrecision(18, 4);

        modelBuilder.Entity<ProductionBatch>().Property(e => e.BatchMultiplier).HasPrecision(precision, scale);
        modelBuilder.Entity<ProductionBatch>().Property(e => e.EstimatedQuantity).HasPrecision(precision, scale);
        modelBuilder.Entity<ProductionBatch>().Property(e => e.ActualQuantity).HasPrecision(precision, scale);
        modelBuilder.Entity<ProductionBatch>().Property(e => e.ScrapQuantity).HasPrecision(precision, scale);
        modelBuilder.Entity<ProductionBatch>().Property(e => e.TotalMaterialCost).HasPrecision(18, 4);
        modelBuilder.Entity<ProductionBatch>().Property(e => e.UnitCost).HasPrecision(18, 4);
        modelBuilder.Entity<ProductionBatch>().Property(e => e.YieldPercentage).HasPrecision(18, 2);

        modelBuilder.Entity<BatchConsumption>().Property(e => e.RequiredQuantity).HasPrecision(precision, scale);
        modelBuilder.Entity<BatchConsumption>().Property(e => e.QuantityUsed).HasPrecision(precision, scale);
        modelBuilder.Entity<BatchConsumption>().Property(e => e.UnitCost).HasPrecision(18, 4);

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

        modelBuilder.Entity<SupplierDocument>()
            .Property(e => e.DocumentType)
            .HasConversion(EnumTextConverter<SupplierDocumentType>())
            .HasColumnType("text");

        modelBuilder.Entity<ApprovalRequest>()
            .Property(e => e.Status)
            .HasConversion(EnumTextConverter<ApprovalStatus>())
            .HasColumnType("text");

        modelBuilder.Entity<PurchaseRequisition>()
            .Property(e => e.Status)
            .HasConversion(EnumTextConverter<PurchaseRequisitionStatus>())
            .HasColumnType("text");

        modelBuilder.Entity<Delivery>()
            .Property(e => e.Status)
            .HasConversion(EnumTextConverter<DeliveryStatus>())
            .HasColumnType("text");

        modelBuilder.Entity<GoodsReceipt>()
            .Property(e => e.Status)
            .HasConversion(EnumTextConverter<GoodsReceiptStatus>())
            .HasColumnType("text");

        modelBuilder.Entity<Discrepancy>()
            .Property(e => e.DiscrepancyType)
            .HasConversion(EnumTextConverter<DiscrepancyType>())
            .HasColumnType("text");

        modelBuilder.Entity<Discrepancy>()
            .Property(e => e.Status)
            .HasConversion(EnumTextConverter<DiscrepancyStatus>())
            .HasColumnType("text");

        modelBuilder.Entity<PutAwayTransaction>()
            .Property(e => e.Status)
            .HasConversion(EnumTextConverter<PutAwayStatus>())
            .HasColumnType("text");

        modelBuilder.Entity<QualityInspection>()
            .Property(e => e.InspectionType)
            .HasConversion(EnumTextConverter<InspectionType>())
            .HasColumnType("text");

        modelBuilder.Entity<QualityInspection>()
            .Property(e => e.Status)
            .HasConversion(EnumTextConverter<QualityInspectionStatus>())
            .HasColumnType("text");

        modelBuilder.Entity<NonConformanceReport>()
            .Property(e => e.Status)
            .HasConversion(EnumTextConverter<NcrStatus>())
            .HasColumnType("text");

        modelBuilder.Entity<ReturnToVendor>().Property(e => e.Status)
            .HasConversion(EnumTextConverter<RtvStatus>())
            .HasColumnType("text");

        modelBuilder.Entity<BranchRequest>().Property(e => e.Status)
            .HasConversion(EnumTextConverter<BranchRequestStatus>())
            .HasColumnType("text");

        modelBuilder.Entity<CycleCount>().Property(e => e.Status)
            .HasConversion(EnumTextConverter<CycleCountStatus>())
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

    /// <summary>
    /// Configures the Supplier-Item catalog linking suppliers to items with pricing, packaging, and lead times.
    /// </summary>
    private static void ConfigureSupplierItems(ModelBuilder modelBuilder)
    {
        var si = modelBuilder.Entity<SupplierItem>();

        si.HasKey(e => new { e.SupplierId, e.ItemId });

        si.HasOne(e => e.Supplier)
            .WithMany(s => s.SupplierItems)
            .HasForeignKey(e => e.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        si.HasOne(e => e.Item)
            .WithMany(i => i.SupplierItems)
            .HasForeignKey(e => e.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        si.HasOne(e => e.PurchaseUom)
            .WithMany()
            .HasForeignKey(e => e.PurchaseUomId)
            .OnDelete(DeleteBehavior.Restrict);

        si.HasIndex(e => e.ItemId)
            .HasDatabaseName("IX_SupplierItems_ItemId");

        si.Property(e => e.UnitPrice).HasPrecision(18, 4);
        si.Property(e => e.PackSize).HasPrecision(18, 3);
        si.Property(e => e.MinOrderQuantity).HasPrecision(18, 3);
        si.Property(e => e.LastPurchasePrice).HasPrecision(18, 4);

        si.ToTable(t =>
        {
            t.HasCheckConstraint("CK_SupplierItems_UnitPrice_Positive", "\"UnitPrice\" >= 0");
            t.HasCheckConstraint("CK_SupplierItems_PackSize_Positive", "\"PackSize\" > 0");
            t.HasCheckConstraint("CK_SupplierItems_MinOrderQuantity_Positive", "\"MinOrderQuantity\" > 0");
        });

        modelBuilder.Entity<PurchaseOrderItem>()
            .HasOne(poi => poi.PurchaseUom)
            .WithMany()
            .HasForeignKey(poi => poi.PurchaseUomId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    /// <summary>
    /// Configures regulatory permits and compliance certificates for suppliers.
    /// </summary>
    private static void ConfigureSupplierDocuments(ModelBuilder modelBuilder)
    {
        var doc = modelBuilder.Entity<SupplierDocument>();

        doc.HasKey(d => d.DocumentId);

        doc.HasOne(d => d.Supplier)
            .WithMany(s => s.SupplierDocuments)
            .HasForeignKey(d => d.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        doc.HasIndex(d => new { d.SupplierId, d.DocumentType })
            .HasDatabaseName("IX_SupplierDocuments_Supplier_Type");

        doc.HasIndex(d => d.ExpiryDate)
            .HasDatabaseName("IX_SupplierDocuments_ExpiryDate");
    }

    /// <summary>
    /// Configures approval requests for financial and quality sign-offs.
    /// </summary>
    private static void ConfigureApprovalRequests(ModelBuilder modelBuilder)
    {
        var app = modelBuilder.Entity<ApprovalRequest>();

        app.HasKey(a => a.ApprovalRequestId);

        app.HasIndex(a => new { a.EntityType, a.EntityId })
            .HasDatabaseName("IX_ApprovalRequests_Entity");

        app.HasIndex(a => a.Status)
            .HasDatabaseName("IX_ApprovalRequests_Status");

        app.HasIndex(a => a.RequestedAt)
            .HasDatabaseName("IX_ApprovalRequests_RequestedAt");
    }

    /// <summary>
    /// Configures purchase requisitions and multi-vendor fan-out line items.
    /// </summary>
    private static void ConfigurePurchaseRequisitions(ModelBuilder modelBuilder)
    {
        var pr = modelBuilder.Entity<PurchaseRequisition>();

        pr.HasKey(p => p.PrId);

        pr.HasIndex(p => p.PrNumber)
            .IsUnique()
            .HasDatabaseName("IX_PurchaseRequisitions_PrNumber");

        pr.HasIndex(p => p.Status)
            .HasDatabaseName("IX_PurchaseRequisitions_Status");

        pr.HasMany(p => p.Items)
            .WithOne(i => i.PurchaseRequisition)
            .HasForeignKey(i => i.PrId)
            .OnDelete(DeleteBehavior.Cascade);

        var item = modelBuilder.Entity<PurchaseRequisitionItem>();

        item.HasKey(i => i.PrItemId);

        item.HasOne(i => i.Item)
            .WithMany()
            .HasForeignKey(i => i.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        item.HasOne(i => i.SuggestedSupplier)
            .WithMany()
            .HasForeignKey(i => i.SuggestedSupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        item.HasOne(i => i.PurchaseUom)
            .WithMany()
            .HasForeignKey(i => i.PurchaseUomId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    /// <summary>
    /// Configures Delivery shipment events.
    /// </summary>
    private static void ConfigureDeliveries(ModelBuilder modelBuilder)
    {
        var delivery = modelBuilder.Entity<Delivery>();

        delivery.HasKey(d => d.DeliveryId);

        delivery.HasIndex(d => d.DeliveryNumber)
            .IsUnique()
            .HasDatabaseName("IX_Deliveries_DeliveryNumber");

        delivery.HasIndex(d => d.PoId)
            .HasDatabaseName("IX_Deliveries_PoId");

        delivery.HasIndex(d => d.Status)
            .HasDatabaseName("IX_Deliveries_Status");

        delivery.HasOne(d => d.PurchaseOrder)
            .WithMany(p => p.Deliveries)
            .HasForeignKey(d => d.PoId)
            .OnDelete(DeleteBehavior.Restrict);

        delivery.HasOne(d => d.Supplier)
            .WithMany()
            .HasForeignKey(d => d.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        delivery.HasOne(d => d.ReceivingLocation)
            .WithMany()
            .HasForeignKey(d => d.ReceivingLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        delivery.HasMany(d => d.Items)
            .WithOne(i => i.Delivery)
            .HasForeignKey(i => i.DeliveryId)
            .OnDelete(DeleteBehavior.Cascade);

        var item = modelBuilder.Entity<DeliveryItem>();

        item.HasKey(i => i.DeliveryItemId);

        item.Property(i => i.DeclaredQuantity).HasPrecision(18, 3);

        item.HasOne(i => i.PurchaseOrderItem)
            .WithMany()
            .HasForeignKey(i => i.PoItemId)
            .OnDelete(DeleteBehavior.Restrict);

        item.HasOne(i => i.Item)
            .WithMany()
            .HasForeignKey(i => i.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        item.HasOne(i => i.PurchaseUom)
            .WithMany()
            .HasForeignKey(i => i.PurchaseUomId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    /// <summary>
    /// Configures Goods Receipt Notes (GRN).
    /// </summary>
    private static void ConfigureGoodsReceipts(ModelBuilder modelBuilder)
    {
        var grn = modelBuilder.Entity<GoodsReceipt>();

        grn.HasKey(g => g.GrnId);

        grn.HasIndex(g => g.GrnNumber)
            .IsUnique()
            .HasDatabaseName("IX_GoodsReceipts_GrnNumber");

        grn.HasIndex(g => g.PoId)
            .HasDatabaseName("IX_GoodsReceipts_PoId");

        grn.HasIndex(g => g.DeliveryId)
            .HasDatabaseName("IX_GoodsReceipts_DeliveryId");

        grn.HasOne(g => g.PurchaseOrder)
            .WithMany()
            .HasForeignKey(g => g.PoId)
            .OnDelete(DeleteBehavior.Restrict);

        grn.HasOne(g => g.Delivery)
            .WithMany(d => d.GoodsReceipts)
            .HasForeignKey(g => g.DeliveryId)
            .OnDelete(DeleteBehavior.SetNull);

        grn.HasOne(g => g.Supplier)
            .WithMany()
            .HasForeignKey(g => g.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        grn.HasOne(g => g.ReceivingLocation)
            .WithMany()
            .HasForeignKey(g => g.ReceivingLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        grn.HasMany(g => g.Items)
            .WithOne(i => i.GoodsReceipt)
            .HasForeignKey(i => i.GrnId)
            .OnDelete(DeleteBehavior.Cascade);

        var item = modelBuilder.Entity<GoodsReceiptItem>();

        item.HasKey(i => i.GrnItemId);

        item.HasOne(i => i.PurchaseOrderItem)
            .WithMany()
            .HasForeignKey(i => i.PoItemId)
            .OnDelete(DeleteBehavior.Restrict);

        item.HasOne(i => i.Item)
            .WithMany()
            .HasForeignKey(i => i.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        item.HasOne(i => i.PurchaseUom)
            .WithMany()
            .HasForeignKey(i => i.PurchaseUomId)
            .OnDelete(DeleteBehavior.Restrict);

        item.HasOne(i => i.Lot)
            .WithMany()
            .HasForeignKey(i => i.LotId)
            .OnDelete(DeleteBehavior.Restrict);

        item.HasOne(i => i.DeliveryItem)
            .WithMany()
            .HasForeignKey(i => i.DeliveryItemId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureDiscrepancies(ModelBuilder modelBuilder)
    {
        var dsc = modelBuilder.Entity<Discrepancy>();

        dsc.HasKey(d => d.DiscrepancyId);

        dsc.HasIndex(d => d.DiscrepancyNumber)
            .IsUnique()
            .HasDatabaseName("IX_Discrepancies_DiscrepancyNumber");

        dsc.HasIndex(d => d.GrnId).HasDatabaseName("IX_Discrepancies_GrnId");
        dsc.HasIndex(d => d.PoId).HasDatabaseName("IX_Discrepancies_PoId");
        dsc.HasIndex(d => d.DeliveryId).HasDatabaseName("IX_Discrepancies_DeliveryId");

        dsc.HasOne(d => d.GoodsReceipt)
            .WithMany(g => g.Discrepancies)
            .HasForeignKey(d => d.GrnId)
            .OnDelete(DeleteBehavior.Cascade);

        dsc.HasOne(d => d.PurchaseOrder)
            .WithMany()
            .HasForeignKey(d => d.PoId)
            .OnDelete(DeleteBehavior.Restrict);

        dsc.HasOne(d => d.Delivery)
            .WithMany()
            .HasForeignKey(d => d.DeliveryId)
            .OnDelete(DeleteBehavior.SetNull);

        dsc.HasOne(d => d.Item)
            .WithMany()
            .HasForeignKey(d => d.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        dsc.HasOne(d => d.NonConformanceReport)
            .WithMany()
            .HasForeignKey(d => d.NcrId)
            .OnDelete(DeleteBehavior.SetNull);

        dsc.HasOne(d => d.ReturnToVendor)
            .WithMany()
            .HasForeignKey(d => d.RtvId)
            .OnDelete(DeleteBehavior.SetNull);

        dsc.HasOne(d => d.LossReport)
            .WithOne(l => l.Discrepancy)
            .HasForeignKey<Discrepancy>(d => d.LossReportId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigurePutAwayTransactions(ModelBuilder modelBuilder)
    {
        var pa = modelBuilder.Entity<PutAwayTransaction>();

        pa.HasKey(p => p.PutAwayId);

        pa.HasIndex(p => p.PutAwayNumber)
            .IsUnique()
            .HasDatabaseName("IX_PutAwayTransactions_PutAwayNumber");

        pa.HasIndex(p => p.GrnId).HasDatabaseName("IX_PutAwayTransactions_GrnId");

        pa.HasOne(p => p.GoodsReceipt)
            .WithMany(g => g.PutAways)
            .HasForeignKey(p => p.GrnId)
            .OnDelete(DeleteBehavior.Cascade);

        pa.HasOne(p => p.GoodsReceiptItem)
            .WithMany()
            .HasForeignKey(p => p.GrnItemId)
            .OnDelete(DeleteBehavior.Restrict);

        pa.HasOne(p => p.QaInspection)
            .WithMany()
            .HasForeignKey(p => p.QaInspectionId)
            .OnDelete(DeleteBehavior.SetNull);

        pa.HasOne(p => p.Item)
            .WithMany()
            .HasForeignKey(p => p.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        pa.HasOne(p => p.Uom)
            .WithMany()
            .HasForeignKey(p => p.UomId)
            .OnDelete(DeleteBehavior.Restrict);

        pa.HasOne(p => p.DestinationLocation)
            .WithMany()
            .HasForeignKey(p => p.DestinationLocationId)
            .OnDelete(DeleteBehavior.SetNull);

        pa.HasOne(p => p.Lot)
            .WithMany()
            .HasForeignKey(p => p.LotId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureLossReports(ModelBuilder modelBuilder)
    {
        var lr = modelBuilder.Entity<LossReport>();

        lr.HasKey(l => l.LossReportId);

        lr.HasIndex(l => l.LossReportNumber)
            .IsUnique()
            .HasDatabaseName("IX_LossReports_LossReportNumber");

        lr.HasIndex(l => l.DiscrepancyId).HasDatabaseName("IX_LossReports_DiscrepancyId");

        lr.HasOne(l => l.GoodsReceipt)
            .WithMany()
            .HasForeignKey(l => l.GrnId)
            .OnDelete(DeleteBehavior.SetNull);

        lr.HasOne(l => l.PurchaseOrder)
            .WithMany()
            .HasForeignKey(l => l.PoId)
            .OnDelete(DeleteBehavior.SetNull);

        lr.HasOne(l => l.Lot)
            .WithMany()
            .HasForeignKey(l => l.LotId)
            .OnDelete(DeleteBehavior.SetNull);

        lr.HasOne(l => l.Item)
            .WithMany()
            .HasForeignKey(l => l.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        lr.HasOne(l => l.Uom)
            .WithMany()
            .HasForeignKey(l => l.UomId)
            .OnDelete(DeleteBehavior.Restrict);

        lr.HasOne(l => l.StockLedgerEntry)
            .WithMany()
            .HasForeignKey(l => l.StockLedgerEntryId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    /// <summary>
    /// Configures Quality Inspections (Incoming, In-Process, Finished Goods QA).
    /// </summary>
    private static void ConfigureQualityInspections(ModelBuilder modelBuilder)
    {
        var qc = modelBuilder.Entity<QualityInspection>();

        qc.HasKey(q => q.InspectionId);

        qc.HasIndex(q => q.InspectionNumber)
            .IsUnique()
            .HasDatabaseName("IX_QualityInspections_InspectionNumber");

        qc.HasIndex(q => new { q.ReferenceType, q.ReferenceId })
            .HasDatabaseName("IX_QualityInspections_Reference");

        qc.HasMany(q => q.Items)
            .WithOne(i => i.QualityInspection)
            .HasForeignKey(i => i.InspectionId)
            .OnDelete(DeleteBehavior.Cascade);

        var item = modelBuilder.Entity<QualityInspectionItem>();

        item.HasKey(i => i.InspectionItemId);

        item.HasOne(i => i.Item)
            .WithMany()
            .HasForeignKey(i => i.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        item.HasOne(i => i.Lot)
            .WithMany()
            .HasForeignKey(i => i.LotId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    /// <summary>
    /// Configures Non-Conformance Reports (NCR).
    /// </summary>
    private static void ConfigureNonConformanceReports(ModelBuilder modelBuilder)
    {
        var ncr = modelBuilder.Entity<NonConformanceReport>();

        ncr.HasKey(n => n.NcrId);

        ncr.HasIndex(n => n.NcrNumber)
            .IsUnique()
            .HasDatabaseName("IX_NonConformanceReports_NcrNumber");

        ncr.HasOne(n => n.Inspection)
            .WithMany()
            .HasForeignKey(n => n.InspectionId)
            .OnDelete(DeleteBehavior.SetNull);

        ncr.HasOne(n => n.Supplier)
            .WithMany()
            .HasForeignKey(n => n.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        ncr.HasOne(n => n.Item)
            .WithMany()
            .HasForeignKey(n => n.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        ncr.HasOne(n => n.Lot)
            .WithMany()
            .HasForeignKey(n => n.LotId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    /// <summary>
    /// Configures Return to Vendor (RTV) records.
    /// </summary>
    private static void ConfigureReturnToVendors(ModelBuilder modelBuilder)
    {
        var rtv = modelBuilder.Entity<ReturnToVendor>();

        rtv.HasKey(r => r.RtvId);

        rtv.HasIndex(r => r.RtvNumber)
            .IsUnique()
            .HasDatabaseName("IX_ReturnToVendors_RtvNumber");

        rtv.HasOne(r => r.NonConformanceReport)
            .WithMany()
            .HasForeignKey(r => r.NcrId)
            .OnDelete(DeleteBehavior.SetNull);

        rtv.HasOne(r => r.Supplier)
            .WithMany()
            .HasForeignKey(r => r.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        rtv.HasOne(r => r.Item)
            .WithMany()
            .HasForeignKey(r => r.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        rtv.HasOne(r => r.Lot)
            .WithMany()
            .HasForeignKey(r => r.LotId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    /// <summary>
    /// Configures stock disposal records and write-offs.
    /// </summary>
    private static void ConfigureDisposalRecords(ModelBuilder modelBuilder)
    {
        var disp = modelBuilder.Entity<DisposalRecord>();

        disp.HasKey(d => d.DisposalId);

        disp.HasIndex(d => d.DisposalNumber)
            .IsUnique()
            .HasDatabaseName("IX_DisposalRecords_DisposalNumber");

        disp.HasMany(d => d.Items)
            .WithOne(i => i.DisposalRecord)
            .HasForeignKey(i => i.DisposalId)
            .OnDelete(DeleteBehavior.Cascade);

        var item = modelBuilder.Entity<DisposalRecordItem>();

        item.HasKey(i => i.DisposalItemId);

        item.HasOne(i => i.Lot)
            .WithMany()
            .HasForeignKey(i => i.LotId)
            .OnDelete(DeleteBehavior.Restrict);

        item.HasOne(i => i.Item)
            .WithMany()
            .HasForeignKey(i => i.ItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    /// <summary>
    /// Configures branch replenishment requests and store returns.
    /// </summary>
    private static void ConfigureBranchDistribution(ModelBuilder modelBuilder)
    {
        var req = modelBuilder.Entity<BranchRequest>();

        req.HasKey(r => r.BranchRequestId);

        req.HasIndex(r => r.RequestNumber)
            .IsUnique()
            .HasDatabaseName("IX_BranchRequests_RequestNumber");

        req.HasOne(r => r.Branch)
            .WithMany()
            .HasForeignKey(r => r.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        req.HasMany(r => r.Items)
            .WithOne(i => i.BranchRequest)
            .HasForeignKey(i => i.BranchRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        var reqItem = modelBuilder.Entity<BranchRequestItem>();

        reqItem.HasKey(i => i.BranchRequestItemId);

        reqItem.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        var ret = modelBuilder.Entity<BranchReturn>();

        ret.HasKey(r => r.BranchReturnId);

        ret.HasIndex(r => r.ReturnNumber)
            .IsUnique()
            .HasDatabaseName("IX_BranchReturns_ReturnNumber");

        ret.HasOne(r => r.Branch)
            .WithMany()
            .HasForeignKey(r => r.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        ret.HasMany(r => r.Items)
            .WithOne(i => i.BranchReturn)
            .HasForeignKey(i => i.BranchReturnId)
            .OnDelete(DeleteBehavior.Cascade);

        var retItem = modelBuilder.Entity<BranchReturnItem>();

        retItem.HasKey(i => i.BranchReturnItemId);

        retItem.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        retItem.HasOne(i => i.Lot)
            .WithMany()
            .HasForeignKey(i => i.LotId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    /// <summary>
    /// Configures recall simulation and hold cascade records.
    /// </summary>
    private static void ConfigureRecallRecords(ModelBuilder modelBuilder)
    {
        var rec = modelBuilder.Entity<RecallRecord>();

        rec.HasKey(r => r.RecallId);

        rec.HasIndex(r => r.RecallNumber)
            .IsUnique()
            .HasDatabaseName("IX_RecallRecords_RecallNumber");
    }

    /// <summary>
    /// Configures physical cycle count audits and inventory reconciliations.
    /// </summary>
    private static void ConfigureCycleCounts(ModelBuilder modelBuilder)
    {
        var cc = modelBuilder.Entity<CycleCount>();

        cc.HasKey(c => c.CycleCountId);

        cc.HasIndex(c => c.CountNumber)
            .IsUnique()
            .HasDatabaseName("IX_CycleCounts_CountNumber");

        cc.HasOne(c => c.Location)
            .WithMany()
            .HasForeignKey(c => c.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        cc.HasMany(c => c.Items)
            .WithOne(i => i.CycleCount)
            .HasForeignKey(i => i.CycleCountId)
            .OnDelete(DeleteBehavior.Cascade);

        var item = modelBuilder.Entity<CycleCountItem>();

        item.HasKey(i => i.CycleCountItemId);

        item.HasOne(i => i.Item)
            .WithMany()
            .HasForeignKey(i => i.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        item.HasOne(i => i.Lot)
            .WithMany()
            .HasForeignKey(i => i.LotId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static ValueConverter<TEnum, string> EnumTextConverter<TEnum>() where TEnum : struct, Enum
        => new(
            value => EnumDbValue.ToDbValue(value),
            stored => EnumDbValue.Parse<TEnum>(stored));
}