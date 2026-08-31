using api_scm.Tests.Infrastructure;
using Applications.Interfaces;
using Applications.Services;
using Domains.Enums;
using Domains.Exceptions;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;

namespace api_scm.Tests.Lots;

[Collection(ScmDatabaseCollection.Name)]
public sealed class StockPostingServiceTests(ScmDatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static StockPostingService NewService(ScmDbContext context, ICurrentUserService? user = null)
    {
        var actor = user ?? ServiceFactory.DefaultUser;
        return new StockPostingService(
            context,
            actor,
            new LotCodeGenerator(context, new DocumentNumberService(context)),
            new LocationResolver(context),
            new StatusTransitionGuard());
    }

    private static ReceiveLotRequest PurchasedUbe(SeededWorld world, decimal quantity, int supplierId) =>
        new(world.UbeItemId, world.MainWarehouseId, quantity, LotSourceType.Purchased, LotStatus.Available)
        {
            SupplierId = supplierId,
            UnitCost = 120m,
            ReferenceType = "PurchaseOrder",
            ReferenceId = "PO-2026-0042"
        };

    [Fact]
    public async Task Receiving_creates_a_lot_and_a_matching_ledger_entry()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        var service = NewService(context);

        var lot = await service.ReceiveAsync(PurchasedUbe(world, 90m, world.SupplierAId));
        await context.SaveChangesAsync();

        await using var verify = CreateContext();
        var stored = await verify.InventoryLots.SingleAsync();
        stored.LotCode.Should().StartWith("L-").And.Contain("UBE");
        stored.QuantityReceived.Should().Be(90m);
        stored.QuantityRemaining.Should().Be(90m);
        stored.SupplierId.Should().Be(world.SupplierAId);

        var entry = await verify.StockLedgers.SingleAsync();
        entry.LotId.Should().Be(lot.LotId, "the ledger row points at the lot it created");
        entry.MovementType.Should().Be(MovementType.PurchaseReceipt);
        entry.Quantity.Should().Be(90m, "receipts are positive");
        entry.ReferenceId.Should().Be("PO-2026-0042");
        entry.UserName.Should().Be("Maria Santos", "every movement is attributed");
    }

    [Fact]
    public async Task A_lots_remaining_quantity_always_equals_the_sum_of_its_ledger_rows()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        var service = NewService(context);

        var lot = await service.ReceiveAsync(PurchasedUbe(world, 100m, world.SupplierAId));
        await context.SaveChangesAsync();

        await service.ConsumeAsync(
            [new LotDraw(lot.LotId, 30m)], MovementType.ProductionConsumption, "ProductionOrder", "MB-2026-0001");
        await service.ConsumeAsync(
            [new LotDraw(lot.LotId, 25m)], MovementType.ProductionConsumption, "ProductionOrder", "MB-2026-0002");
        await context.SaveChangesAsync();

        await using var verify = CreateContext();
        var stored = await verify.InventoryLots.SingleAsync();
        stored.QuantityRemaining.Should().Be(45m);

        var ledgerTotal = await verify.StockLedgers.SumAsync(l => l.Quantity);
        ledgerTotal.Should().Be(45m, "the ledger is the explanation of the balance, so they must agree");

        // The reconciliation check the system can run on demand.
        (await NewService(verify).ReconcileLotAsync(stored.LotId)).Should().Be(0m);
    }

    [Fact]
    public async Task Consuming_more_than_a_lot_holds_is_refused()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        var service = NewService(context);

        var lot = await service.ReceiveAsync(PurchasedUbe(world, 10m, world.SupplierAId));
        await context.SaveChangesAsync();

        var act = async () => await service.ConsumeAsync(
            [new LotDraw(lot.LotId, 11m)], MovementType.ProductionConsumption, "ProductionOrder", "MB-1");

        (await act.Should().ThrowAsync<InsufficientStockException>())
            .WithMessage("*holds 10 but 11 was requested*");
    }

    [Fact]
    public async Task Quarantined_stock_cannot_be_consumed()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        var service = NewService(context);

        var request = PurchasedUbe(world, 50m, world.SupplierAId) with { Status = LotStatus.Quarantine };
        var lot = await service.ReceiveAsync(request);
        await context.SaveChangesAsync();

        var act = async () => await service.ConsumeAsync(
            [new LotDraw(lot.LotId, 5m)], MovementType.ProductionConsumption, "ProductionOrder", "MB-1");

        (await act.Should().ThrowAsync<InsufficientStockException>())
            .WithMessage("*Quarantine*cannot be consumed*");
    }

    [Fact]
    public async Task A_fully_drawn_lot_becomes_Consumed()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        var service = NewService(context);

        var lot = await service.ReceiveAsync(PurchasedUbe(world, 10m, world.SupplierAId));
        await context.SaveChangesAsync();

        await service.ConsumeAsync(
            [new LotDraw(lot.LotId, 10m)], MovementType.ProductionConsumption, "ProductionOrder", "MB-1");
        await context.SaveChangesAsync();

        await using var verify = CreateContext();
        var stored = await verify.InventoryLots.SingleAsync();
        stored.QuantityRemaining.Should().Be(0m);
        stored.Status.Should().Be(LotStatus.Consumed,
            "an empty lot drops out of availability queries without being deleted");
    }

    [Fact]
    public async Task The_9_plus_1_draw_across_two_suppliers_is_recorded_lot_by_lot()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        var service = NewService(context);

        var fromA = await service.ReceiveAsync(PurchasedUbe(world, 9m, world.SupplierAId));
        var fromB = await service.ReceiveAsync(PurchasedUbe(world, 100m, world.SupplierBId));
        await context.SaveChangesAsync();

        // A 10 kg requirement met as 9 from Supplier A and 1 from Supplier B.
        await service.ConsumeAsync(
            [new LotDraw(fromA.LotId, 9m), new LotDraw(fromB.LotId, 1m)],
            MovementType.ProductionConsumption, "ProductionOrder", "MB-2026-0001");
        await context.SaveChangesAsync();

        await using var verify = CreateContext();

        var consumption = await verify.StockLedgers
            .Include(l => l.Lot)
            .Where(l => l.MovementType == MovementType.ProductionConsumption)
            .ToListAsync();

        consumption.Should().HaveCount(2, "one row per source lot, which is what makes tracing possible");

        // The question the panel asked: which supplier's material went into this batch, and how much.
        var bySupplier = consumption.ToDictionary(l => l.Lot!.SupplierId!.Value, l => -l.Quantity);
        bySupplier[world.SupplierAId].Should().Be(9m);
        bySupplier[world.SupplierBId].Should().Be(1m);

        // Supplier A's lot is exhausted; Supplier B's still holds the rest.
        (await verify.InventoryLots.SingleAsync(l => l.LotId == fromA.LotId))
            .QuantityRemaining.Should().Be(0m);
        (await verify.InventoryLots.SingleAsync(l => l.LotId == fromB.LotId))
            .QuantityRemaining.Should().Be(99m);
    }

    [Fact]
    public async Task A_transfer_preserves_lot_identity_and_expiry_at_the_destination()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        var service = NewService(context);

        var expiry = new DateOnly(2026, 9, 30);
        var source = await service.ReceiveAsync(
            PurchasedUbe(world, 50m, world.SupplierAId) with { ExpiryDate = expiry });
        await context.SaveChangesAsync();

        await service.TransferOutAsync([new LotDraw(source.LotId, 20m)], "Shipment", "SH-2026-0001");
        var arriving = await service.TransferInAsync(
            source.LotId, world.BranchManilaId, 20m, "Shipment", "SH-2026-0001");
        await context.SaveChangesAsync();

        await using var verify = CreateContext();

        var atBranch = await verify.InventoryLots.SingleAsync(l => l.LotId == arriving.LotId);
        atBranch.LocationId.Should().Be(world.BranchManilaId);
        atBranch.QuantityRemaining.Should().Be(20m);

        // The chain that makes a last-mile recall possible.
        atBranch.LotCode.Should().Be(source.LotCode, "the same lot code travels with the goods");
        atBranch.ExpiryDate.Should().Be(expiry, "and so does the expiry date");
        atBranch.SupplierId.Should().Be(world.SupplierAId, "and the original supplier");

        (await verify.InventoryLots.SingleAsync(l => l.LotId == source.LotId))
            .QuantityRemaining.Should().Be(30m, "the source is debited by exactly what left");
    }

    [Fact]
    public async Task An_adjustment_requires_a_reason()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        var service = NewService(context);

        var lot = await service.ReceiveAsync(PurchasedUbe(world, 10m, world.SupplierAId));
        await context.SaveChangesAsync();

        var act = async () => await service.AdjustAsync(lot.LotId, 8m, "  ", "CC-2026-0001");

        (await act.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*needs a reason*");
    }

    [Fact]
    public async Task An_adjustment_records_the_difference_and_the_reason()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        var service = NewService(context);

        var lot = await service.ReceiveAsync(PurchasedUbe(world, 10m, world.SupplierAId));
        await context.SaveChangesAsync();

        await service.AdjustAsync(lot.LotId, 8.5m, "Counted short at cycle count", "CC-2026-0001");
        await context.SaveChangesAsync();

        await using var verify = CreateContext();
        (await verify.InventoryLots.SingleAsync()).QuantityRemaining.Should().Be(8.5m);

        var adjustment = await verify.StockLedgers
            .SingleAsync(l => l.MovementType == MovementType.Adjustment);
        adjustment.Quantity.Should().Be(-1.5m, "the ledger records the delta, not the new total");
        adjustment.Notes.Should().Be("Counted short at cycle count");

        (await NewService(verify).ReconcileLotAsync(lot.LotId)).Should().Be(0m);
    }

    [Fact]
    public async Task Counting_more_than_was_received_raises_the_received_figure_too()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        var service = NewService(context);

        var lot = await service.ReceiveAsync(PurchasedUbe(world, 10m, world.SupplierAId));
        await context.SaveChangesAsync();

        // Finding more than was booked in means the booked-in figure was wrong.
        await service.AdjustAsync(lot.LotId, 12m, "Found extra sack", "CC-2026-0002");
        await context.SaveChangesAsync();

        await using var verify = CreateContext();
        var stored = await verify.InventoryLots.SingleAsync();
        stored.QuantityRemaining.Should().Be(12m);
        stored.QuantityReceived.Should().Be(12m,
            "otherwise the remaining-within-received invariant would be violated");
    }

    [Fact]
    public async Task Disposal_depletes_the_lot_and_records_the_reason()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        var service = NewService(context);

        var lot = await service.ReceiveAsync(PurchasedUbe(world, 10m, world.SupplierAId));
        await context.SaveChangesAsync();

        await service.DisposeAsync(
            [new LotDraw(lot.LotId, 10m)], "Spoiled", "DisposalDocument", "WD-2026-0001");
        await context.SaveChangesAsync();

        await using var verify = CreateContext();
        var stored = await verify.InventoryLots.SingleAsync();
        stored.QuantityRemaining.Should().Be(0m);
        stored.Status.Should().Be(LotStatus.Disposed);

        var entry = await verify.StockLedgers.SingleAsync(l => l.MovementType == MovementType.Disposal);
        entry.Quantity.Should().Be(-10m);
        entry.Notes.Should().Be("Spoiled");

        // Cost of the loss is knowable, which is what makes waste reportable in pesos.
        (entry.Quantity * entry.UnitCost).Should().Be(-1200m);
    }

    [Fact]
    public async Task A_reversal_restores_the_prior_state_and_links_to_what_it_corrects()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        var service = NewService(context);

        var lot = await service.ReceiveAsync(PurchasedUbe(world, 100m, world.SupplierAId));
        await context.SaveChangesAsync();

        await service.ConsumeAsync(
            [new LotDraw(lot.LotId, 40m)], MovementType.ProductionConsumption, "ProductionOrder", "MB-1");
        await context.SaveChangesAsync();

        var consumption = await context.StockLedgers
            .SingleAsync(l => l.MovementType == MovementType.ProductionConsumption);

        await service.ReverseAsync(consumption.LedgerId, "Batch cancelled before cooking");
        await context.SaveChangesAsync();

        await using var verify = CreateContext();
        (await verify.InventoryLots.SingleAsync()).QuantityRemaining.Should().Be(100m,
            "the stock is back");

        var reversal = await verify.StockLedgers.SingleAsync(l => l.MovementType == MovementType.Reversal);
        reversal.Quantity.Should().Be(40m, "the mirror image of the -40 it reverses");
        reversal.ReversalOfLedgerId.Should().Be(consumption.LedgerId);
        reversal.Notes.Should().Be("Batch cancelled before cooking");

        // The original row still exists: history shows both what was believed and what was corrected.
        (await verify.StockLedgers.CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task The_same_movement_cannot_be_reversed_twice()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        var service = NewService(context);

        var lot = await service.ReceiveAsync(PurchasedUbe(world, 100m, world.SupplierAId));
        await context.SaveChangesAsync();
        await service.ConsumeAsync(
            [new LotDraw(lot.LotId, 10m)], MovementType.ProductionConsumption, "ProductionOrder", "MB-1");
        await context.SaveChangesAsync();

        var consumption = await context.StockLedgers
            .SingleAsync(l => l.MovementType == MovementType.ProductionConsumption);

        await service.ReverseAsync(consumption.LedgerId, "first");
        await context.SaveChangesAsync();

        var act = async () =>
        {
            await service.ReverseAsync(consumption.LedgerId, "second");
            await context.SaveChangesAsync();
        };

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*already been reversed*");
    }

    [Fact]
    public async Task A_zero_quantity_movement_is_rejected_by_the_database()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        var service = NewService(context);

        var lot = await service.ReceiveAsync(PurchasedUbe(world, 10m, world.SupplierAId));
        await context.SaveChangesAsync();

        // Exactly what the old transfer completion wrote: a zero-quantity row that explained nothing.
        context.StockLedgers.Add(new Domains.Entities.StockLedger
        {
            LotId = lot.LotId,
            ItemId = lot.ItemId,
            LocationId = lot.LocationId,
            MovementType = MovementType.TransferIn,
            Quantity = 0m,
            UomId = lot.UomId,
            ReferenceType = "Shipment",
            ReferenceId = "SH-1",
            PostedAt = DateTime.UtcNow
        });

        var act = async () => await context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
