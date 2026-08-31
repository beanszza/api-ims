using api_scm.Contracts.Requests;
using api_scm.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace api_scm.Tests.Characterization;

/// <summary>
/// Pins the behaviour of distribution. The headline finding is that a completed transfer adds nothing
/// anywhere, so branch stock is permanently zero and the last mile of a recall chain does not exist.
/// Task 37 closes this.
/// </summary>
[Collection(ScmDatabaseCollection.Name)]
public sealed class StockTransferCharacterizationTests(ScmDatabaseFixture fixture)
    : DatabaseTestBase(fixture)
{
    [Fact(DisplayName = "Defect 12: a completed transfer adds nothing to the destination")]
    public async Task Defect12_Completed_Transfer_Never_Credits_Destination()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        await TestDataSeeder.GiveStockAsync(
            context, world.UbeHalaya500ItemId, world.FinishedGoodsLocationId, 50);

        var service = ServiceFactory.StockTransfers(context);

        var created = await service.CreateTransferAsync(new CreateStockTransferRequest
        {
            ProductId = world.UbeHalaya500ProductId,
            SourceLocationId = world.FinishedGoodsLocationId,
            DestLocationId = world.BranchManilaId,
            TransferQuantity = 20m
        });

        created.Success.Should().BeTrue(created.Message);
        var transferId = created.Data!.TransferId;

        await service.UpdateTransferStatusAsync(
            transferId, new UpdateStockTransferStatusRequest { Status = "In Transit" });
        await service.UpdateTransferStatusAsync(
            transferId, new UpdateStockTransferStatusRequest { Status = "Completed" });

        await using var verify = CreateContext();

        var source = await verify.Inventories.SingleAsync(i =>
            i.ItemId == world.UbeHalaya500ItemId && i.LocationId == world.FinishedGoodsLocationId);
        source.CurrentStock.Should().Be(30m, "the source is debited when the transfer enters transit");

        var destination = await verify.Inventories.FirstOrDefaultAsync(i =>
            i.ItemId == world.UbeHalaya500ItemId && i.LocationId == world.BranchManilaId);
        destination.Should().BeNull(
            "no inventory row is ever created at the branch, so branch stock is permanently zero");

        var completionLog = await verify.InventoryMovementLogs
            .SingleAsync(l => l.ActionType == "Transfer Completed (No Addition)");
        completionLog.ChangeQuantity.Should().Be(0m,
            "the completion writes a zero-quantity ledger row - 20 jars left the building and arrived " +
            "nowhere the system can see");
    }

    [Fact(DisplayName = "Defect 12: stock in transit is invisible - it belongs to no location")]
    public async Task Defect12_In_Transit_Stock_Is_Unaccounted()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        await TestDataSeeder.GiveStockAsync(
            context, world.UbeHalaya500ItemId, world.FinishedGoodsLocationId, 50);

        var service = ServiceFactory.StockTransfers(context);
        var created = await service.CreateTransferAsync(new CreateStockTransferRequest
        {
            ProductId = world.UbeHalaya500ProductId,
            SourceLocationId = world.FinishedGoodsLocationId,
            DestLocationId = world.BranchManilaId,
            TransferQuantity = 20m
        });

        await service.UpdateTransferStatusAsync(
            created.Data!.TransferId,
            new UpdateStockTransferStatusRequest { Status = "In Transit" });

        await using var verify = CreateContext();
        var totalOnHand = await verify.Inventories
            .Where(i => i.ItemId == world.UbeHalaya500ItemId)
            .SumAsync(i => i.CurrentStock);

        totalOnHand.Should().Be(30m,
            "20 of the original 50 jars have vanished from the balance sheet while on the truck; there " +
            "is no in-transit location to hold them. Task 36 introduces one");
    }

    [Fact(DisplayName = "Defect 13: transfers only accept finished products, not raw materials")]
    public async Task Defect13_Raw_Materials_Cannot_Be_Transferred()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        await TestDataSeeder.GiveStockAsync(context, world.UbeItemId, world.MainWarehouseId, 100);

        // StockTransfer.ProductId is a FinishedProduct FK. Raw ube has no FinishedProduct row, so moving
        // it between warehouses is not expressible at all.
        var hasFinishedProductRow = await context.FinishedProducts
            .AnyAsync(p => p.ItemId == world.UbeItemId);
        hasFinishedProductRow.Should().BeFalse();

        // There is no ProductId that denotes raw ube, so the only thing a caller can do is pass an id
        // that does not resolve to a FinishedProduct - and be rejected.
        var unmappedProductId = await context.FinishedProducts.MaxAsync(p => p.ProductId) + 1;

        var result = await ServiceFactory.StockTransfers(context).CreateTransferAsync(
            new CreateStockTransferRequest
            {
                ProductId = unmappedProductId,
                SourceLocationId = world.MainWarehouseId,
                DestLocationId = world.BranchManilaId,
                TransferQuantity = 10m
            });

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Product not found");

        // Meanwhile 100 kg of ube sits at the Main Warehouse with no way to move it anywhere.
        var strandedRawMaterial = await context.Inventories
            .SingleAsync(i => i.ItemId == world.UbeItemId && i.LocationId == world.MainWarehouseId);
        strandedRawMaterial.CurrentStock.Should().Be(100m);
    }

    [Fact(DisplayName = "Defect 14: a driver is bound to an inventory balance, not to a shipment")]
    public async Task Defect14_Driver_Is_Modelled_On_Inventory()
    {
        await using var context = CreateContext();
        await TestDataSeeder.SeedBaselineAsync(context);

        var inventoryProperties = context.Model
            .FindEntityType(typeof(Domains.Entities.Inventory))!
            .GetProperties()
            .Select(p => p.Name)
            .ToArray();
        var transferProperties = context.Model
            .FindEntityType(typeof(Domains.Entities.StockTransfer))!
            .GetProperties()
            .Select(p => p.Name)
            .ToArray();

        inventoryProperties.Should().Contain("DriverId", "the driver hangs off a stock balance");
        transferProperties.Should().NotContain("DriverId",
            "but the document that actually involves a driver has no reference to one");
        transferProperties.Should().NotContain("VehiclePlate");

        // Task 35 moves the driver onto the shipment where it belongs.
    }

    [Fact(DisplayName = "Defect 12: no lot identity travels with a transfer")]
    public async Task Defect12_Transfer_Carries_No_Lot_Identity()
    {
        await using var context = CreateContext();
        await TestDataSeeder.SeedBaselineAsync(context);

        var transferProperties = context.Model
            .FindEntityType(typeof(Domains.Entities.StockTransfer))!
            .GetProperties()
            .Select(p => p.Name)
            .ToArray();

        transferProperties.Should().NotContain("LotId");
        transferProperties.Should().NotContain("LotCode");

        // Without a lot on the shipment line, "which branch received the affected batch" is
        // unanswerable, which is precisely what a recall needs to answer. Task 36 adds it.
    }
}
