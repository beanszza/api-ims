using System;
using System.Linq;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
using FluentAssertions;

namespace api_scm.Tests.Characterization;

/// <summary>
/// Pins the behaviour of distribution and verifies shipment tracking and driver assignment.
/// </summary>
[Collection(ScmDatabaseCollection.Name)]
public sealed class StockTransferCharacterizationTests(ScmDatabaseFixture fixture)
    : DatabaseTestBase(fixture)
{
    [Fact(DisplayName = "Defect 12 FIXED in Task 37: a completed transfer records delivery log to branch")]
    public async Task Defect12_Completed_Transfer_Logs_Delivery()
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

        var completionLog = await verify.InventoryMovementLogs
            .SingleAsync(l => l.ActionType == "Transfer Completed (Delivered to Branch)");
        completionLog.ChangeQuantity.Should().Be(0m);
    }

    [Fact(DisplayName = "Defect 12: stock in transit is deducted from commissary finished goods balance")]
    public async Task Defect12_In_Transit_Stock_Is_Deducted_From_Source()
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
        var sourceStock = await verify.Inventories
            .Where(i => i.ItemId == world.UbeHalaya500ItemId && i.LocationId == world.FinishedGoodsLocationId)
            .SumAsync(i => i.CurrentStock);

        sourceStock.Should().Be(30m, "20 jars debited upon entering transit");
    }

    [Fact(DisplayName = "Defect 13: transfers only accept finished products, not unmapped raw products")]
    public async Task Defect13_Raw_Materials_Cannot_Be_Transferred()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        await TestDataSeeder.GiveStockAsync(context, world.UbeItemId, world.MainWarehouseId, 100);

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
    }

    [Fact(DisplayName = "Defect 14 FIXED in Task 35: driver and vehicle plate are modelled on the shipment")]
    public async Task Defect14_Driver_Is_Modelled_On_Shipment()
    {
        await using var context = CreateContext();
        await TestDataSeeder.SeedBaselineAsync(context);

        var transferProperties = context.Model
            .FindEntityType(typeof(Domains.Entities.StockTransfer))!
            .GetProperties()
            .Select(p => p.Name)
            .ToArray();

        transferProperties.Should().Contain("DriverName", "driver name is stored on the shipment document");
        transferProperties.Should().Contain("VehiclePlate", "vehicle plate is stored on the shipment document");
        transferProperties.Should().Contain("TransferNumber", "canonical shipment number is stored");
    }
}
