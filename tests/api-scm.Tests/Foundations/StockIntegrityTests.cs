using api_scm.Contracts.Requests;
using api_scm.Tests.Infrastructure;
using Applications.Services;
using Domains.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace api_scm.Tests.Foundations;

[Collection(ScmDatabaseCollection.Name)]
public sealed class StockIntegrityTests(ScmDatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task A_second_balance_row_for_the_same_item_and_location_is_rejected()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        context.Inventories.Add(new Inventory
        {
            ItemId = world.UbeItemId,
            LocationId = world.MainWarehouseId,
            CurrentStock = 10m
        });
        await context.SaveChangesAsync();

        // The same item at the same location a second time. Previously allowed, and the reason startup
        // had to merge duplicates on every boot.
        await using var second = CreateContext();
        second.Inventories.Add(new Inventory
        {
            ItemId = world.UbeItemId,
            LocationId = world.MainWarehouseId,
            CurrentStock = 5m
        });

        var act = async () => await second.SaveChangesAsync();

        var thrown = await act.Should().ThrowAsync<DbUpdateException>();
        thrown.Which.InnerException.Should().BeOfType<Npgsql.PostgresException>()
            .Which.SqlState.Should().Be("23505", "a unique violation, not a silently accepted duplicate");
    }

    [Fact]
    public async Task The_same_item_may_still_be_held_at_different_locations()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        context.Inventories.AddRange(
            new Inventory { ItemId = world.UbeItemId, LocationId = world.MainWarehouseId, CurrentStock = 10m },
            new Inventory { ItemId = world.UbeItemId, LocationId = world.ProductionLocationId, CurrentStock = 4m });

        await context.SaveChangesAsync();

        await using var verify = CreateContext();
        (await verify.Inventories.CountAsync(i => i.ItemId == world.UbeItemId)).Should().Be(2);
    }

    [Fact]
    public async Task A_lost_update_on_a_balance_is_detected_rather_than_silently_overwriting()
    {
        await using var seed = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(seed);
        await TestDataSeeder.GiveStockAsync(seed, world.UbeItemId, world.MainWarehouseId, 100);

        // Two callers read the same balance, then each writes its own result. Without a concurrency
        // token both succeed and one deduction vanishes.
        await using var first = CreateContext();
        await using var secondReader = CreateContext();

        var firstCopy = await first.Inventories.SingleAsync(i => i.ItemId == world.UbeItemId);
        var secondCopy = await secondReader.Inventories.SingleAsync(i => i.ItemId == world.UbeItemId);

        firstCopy.CurrentStock -= 30m;
        await first.SaveChangesAsync();

        secondCopy.CurrentStock -= 40m;
        var act = async () => await secondReader.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "the second writer is working from a balance that has already moved");

        // The first deduction stands, and the second is refused rather than applied to stale data.
        await using var verify = CreateContext();
        (await verify.Inventories.SingleAsync(i => i.ItemId == world.UbeItemId))
            .CurrentStock.Should().Be(70m,
                "not 60: had the stale write been accepted, the 30 deduction would have been lost");
    }

    [Fact]
    public async Task A_purchase_order_is_created_with_a_real_document_number()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        var service = ServiceFactory.PurchaseOrders(context);

        var result = await service.CreatePurchaseOrderAsync(new CreatePurchaseOrderRequest
        {
            SupplierId = world.SupplierAId,
            ExpectedArrivalDate = DateTime.UtcNow.AddDays(5),
            PaymentType = "Cash",
            Items = [new CreatePurchaseOrderItemRequest { ItemId = world.UbeItemId, PoItemQuantity = 90m }]
        });

        result.Success.Should().BeTrue(result.Message);
        result.Data!.PoNumber.Should().Be($"PO-{DateTime.UtcNow.Year}-0001");

        await using var verify = CreateContext();
        (await verify.PurchaseOrders.SingleAsync()).PoNumber
            .Should().Be($"PO-{DateTime.UtcNow.Year}-0001");
    }

    [Fact]
    public async Task A_failure_part_way_through_a_posting_leaves_no_stock_behind()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        await TestDataSeeder.GiveStockAsync(
            context, world.UbeHalaya500ItemId, world.FinishedGoodsLocationId, 50);

        var created = await ServiceFactory.StockTransfers(context).CreateTransferAsync(
            new CreateStockTransferRequest
            {
                ProductId = world.UbeHalaya500ProductId,
                SourceLocationId = world.FinishedGoodsLocationId,
                DestLocationId = world.BranchManilaId,
                TransferQuantity = 500m // more than the 50 on hand
            });

        // Creation validates availability, so it is refused before any posting happens.
        created.Success.Should().BeFalse();

        await using var verify = CreateContext();
        (await verify.Inventories.SingleAsync(i =>
                i.ItemId == world.UbeHalaya500ItemId && i.LocationId == world.FinishedGoodsLocationId))
            .CurrentStock.Should().Be(50m, "nothing was taken");

        (await verify.InventoryMovementLogs.CountAsync()).Should().Be(0, "and nothing was logged");
    }

    [Fact]
    public async Task Dispatching_more_than_is_on_hand_rolls_back_the_whole_posting()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        await TestDataSeeder.GiveStockAsync(
            context, world.UbeHalaya500ItemId, world.FinishedGoodsLocationId, 50);

        var created = await ServiceFactory.StockTransfers(context).CreateTransferAsync(
            new CreateStockTransferRequest
            {
                ProductId = world.UbeHalaya500ProductId,
                SourceLocationId = world.FinishedGoodsLocationId,
                DestLocationId = world.BranchManilaId,
                TransferQuantity = 50m
            });
        created.Success.Should().BeTrue(created.Message);

        // Drain the stock behind the transfer's back, so dispatch finds less than it needs.
        await using (var drain = CreateContext())
        {
            var balance = await drain.Inventories.SingleAsync(i =>
                i.ItemId == world.UbeHalaya500ItemId && i.LocationId == world.FinishedGoodsLocationId);
            balance.CurrentStock = 10m;
            await drain.SaveChangesAsync();
        }

        // A fresh context, because the creating context still has the pre-drain balance tracked and
        // would hand the shortage check a stale number. That stale-read case is covered separately by
        // A_lost_update_on_a_balance_is_detected_rather_than_silently_overwriting.
        await using var dispatchContext = CreateContext();
        var dispatch = await ServiceFactory.StockTransfers(dispatchContext).UpdateTransferStatusAsync(
            created.Data!.TransferId,
            new UpdateStockTransferStatusRequest { Status = "In Transit" });

        dispatch.Success.Should().BeFalse();
        dispatch.Message.Should().Contain("Insufficient stock");

        await using var verify = CreateContext();

        // The status change and the audit entry were staged before the shortage was found. Because the
        // whole thing is one posting, neither survived.
        (await verify.StockTransfers.SingleAsync()).Status
            .Should().Be(Domains.Enums.ShipmentStatus.Pending, "the transfer was not marked dispatched");
        (await verify.AuditLogs.CountAsync(a => a.Action == "StatusUpdated"))
            .Should().Be(0, "and no status-change audit entry was left behind");
        (await verify.Inventories.SingleAsync(i =>
                i.ItemId == world.UbeHalaya500ItemId && i.LocationId == world.FinishedGoodsLocationId))
            .CurrentStock.Should().Be(10m, "and the balance is untouched");
    }

}
