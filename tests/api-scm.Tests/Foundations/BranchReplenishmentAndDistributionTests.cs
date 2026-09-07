using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Tests.Infrastructure;
using Applications.Interfaces;
using Domains.Entities;
using Domains.Enums;
using Domains.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace api_scm.Tests.Foundations;

[Collection(ScmDatabaseCollection.Name)]
public sealed class BranchReplenishmentAndDistributionTests(ScmDatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task CreateBranchRequest_generates_canonical_number_and_pending_status()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var user = new FakeCurrentUserService(new CurrentUser("BM-01", "Branch Manager Leo", "leo@r3b2p.com", ["BranchManager"], true));
        var distribution = ServiceFactory.BranchDistribution(context, user);

        var response = await distribution.CreateBranchRequestAsync(new CreateBranchRequestDto
        {
            BranchId = world.BranchManilaId,
            RequiredDate = DateTime.UtcNow.AddDays(2),
            Notes = "Weekend surge replenishment",
            Items =
            [
                new CreateBranchRequestItemDto
                {
                    ProductId = world.UbeHalaya500ProductId,
                    RequestedQuantity = 48m,
                    Notes = "High demand item"
                }
            ]
        });

        response.Success.Should().BeTrue();
        response.Data!.RequestNumber.Should().StartWith("BR-");
        response.Data.Status.Should().Be("Pending");
        response.Data.BranchName.Should().Be("Branch Manila");
        response.Data.Items.Should().HaveCount(1);
        response.Data.Items[0].RequestedQuantity.Should().Be(48m);
    }

    [Fact]
    public async Task ApproveBranchRequest_generates_shipment_order()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var user = new FakeCurrentUserService(new CurrentUser("LOG-01", "Logistics Lead Sarah", "sarah@r3b2p.com", ["Logistics"], true));
        var distribution = ServiceFactory.BranchDistribution(context, user);

        // 1. Submit Request
        var req = await distribution.CreateBranchRequestAsync(new CreateBranchRequestDto
        {
            BranchId = world.BranchManilaId,
            Items = [new CreateBranchRequestItemDto { ProductId = world.UbeHalaya500ProductId, RequestedQuantity = 24m }]
        });

        // 2. Approve Request
        var approvalResponse = await distribution.ApproveAndGenerateShipmentAsync(req.Data!.BranchRequestId, new DispatchShipmentRequest
        {
            DriverName = "Driver Juan Dela Cruz",
            VehiclePlate = "ABC-1234"
        });

        approvalResponse.Success.Should().BeTrue();
        approvalResponse.Data!.TransferNumber.Should().StartWith("SH-");
        approvalResponse.Data.Status.Should().Be("Pending");
        approvalResponse.Data.DriverName.Should().Be("Driver Juan Dela Cruz");
        approvalResponse.Data.VehiclePlate.Should().Be("ABC-1234");
        approvalResponse.Data.TransferQuantity.Should().Be(24m);

        // Verify Branch Request status transitioned to Approved
        var reloadedReq = await distribution.GetBranchRequestByIdAsync(req.Data.BranchRequestId);
        reloadedReq.Data!.Status.Should().Be("Approved");
    }

    [Fact]
    public async Task DispatchShipment_deducts_commissary_finished_goods_inventory()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var today = DateOnly.FromDateTime(DateTime.Today);

        // Seed available finished goods lot at FinishedGoods location (50 jars)
        var fgLot = new InventoryLot
        {
            LotCode = "LOT-FG-HALAYA-50",
            ItemId = world.UbeHalaya500ItemId,
            LocationId = world.FinishedGoodsLocationId,
            SourceType = LotSourceType.Produced,
            QuantityReceived = 50m,
            QuantityRemaining = 50m,
            UomId = world.PieceUomId,
            UnitCost = 55m,
            Status = LotStatus.Available,
            ManufactureDate = today.AddDays(-2),
            ExpiryDate = today.AddMonths(6)
        };
        context.InventoryLots.Add(fgLot);

        context.Inventories.Add(new Inventory
        {
            ItemId = world.UbeHalaya500ItemId,
            LocationId = world.FinishedGoodsLocationId,
            CurrentStock = 50m
        });
        await context.SaveChangesAsync();

        var user = new FakeCurrentUserService(new CurrentUser("LOG-01", "Logistics Lead Sarah", "sarah@r3b2p.com", ["Logistics"], true));
        var distribution = ServiceFactory.BranchDistribution(context, user);
        var transferService = ServiceFactory.StockTransfers(context, user);

        // 1. Create and Approve Branch Request
        var req = await distribution.CreateBranchRequestAsync(new CreateBranchRequestDto
        {
            BranchId = world.BranchManilaId,
            Items = [new CreateBranchRequestItemDto { ProductId = world.UbeHalaya500ProductId, RequestedQuantity = 20m }]
        });

        var shipment = await distribution.ApproveAndGenerateShipmentAsync(req.Data!.BranchRequestId, new DispatchShipmentRequest
        {
            DriverName = "Driver Juan",
            VehiclePlate = "NQR-789"
        });

        // 2. Dispatch Shipment (InTransit)
        var dispatchResult = await transferService.UpdateTransferStatusAsync(shipment.Data!.TransferId, new UpdateStockTransferStatusRequest
        {
            Status = "In Transit"
        });

        dispatchResult.Success.Should().BeTrue();
        dispatchResult.Data!.Status.Should().Be("In Transit");

        // 3. Verify on-hand finished goods inventory at Commissary is deducted (50 - 20 = 30)
        await using var verify = CreateContext();
        var fgInv = await verify.Inventories
            .FirstAsync(i => i.ItemId == world.UbeHalaya500ItemId && i.LocationId == world.FinishedGoodsLocationId);
        fgInv.CurrentStock.Should().Be(30m, "Dispatching finished goods to retail branch deducts commissary stock");
    }

    [Fact]
    public async Task CompleteShipment_fulfills_branch_request_and_logs_delivery()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var today = DateOnly.FromDateTime(DateTime.Today);

        // Seed available finished goods lot
        var fgLot = new InventoryLot
        {
            LotCode = "LOT-FG-HALAYA-30",
            ItemId = world.UbeHalaya500ItemId,
            LocationId = world.FinishedGoodsLocationId,
            SourceType = LotSourceType.Produced,
            QuantityReceived = 30m,
            QuantityRemaining = 30m,
            UomId = world.PieceUomId,
            UnitCost = 55m,
            Status = LotStatus.Available,
            ExpiryDate = today.AddMonths(6)
        };
        context.InventoryLots.Add(fgLot);
        context.Inventories.Add(new Inventory
        {
            ItemId = world.UbeHalaya500ItemId,
            LocationId = world.FinishedGoodsLocationId,
            CurrentStock = 30m
        });
        await context.SaveChangesAsync();

        var user = new FakeCurrentUserService(new CurrentUser("LOG-01", "Logistics Lead Sarah", "sarah@r3b2p.com", ["Logistics"], true));
        var distribution = ServiceFactory.BranchDistribution(context, user);
        var transferService = ServiceFactory.StockTransfers(context, user);

        // 1. Create and Approve
        var req = await distribution.CreateBranchRequestAsync(new CreateBranchRequestDto
        {
            BranchId = world.BranchManilaId,
            Items = [new CreateBranchRequestItemDto { ProductId = world.UbeHalaya500ProductId, RequestedQuantity = 15m }]
        });

        var shipment = await distribution.ApproveAndGenerateShipmentAsync(req.Data!.BranchRequestId);

        // 2. Dispatch
        await transferService.UpdateTransferStatusAsync(shipment.Data!.TransferId, new UpdateStockTransferStatusRequest
        {
            Status = "In Transit"
        });

        // 3. Confirm Delivery at Branch
        var deliveryResult = await transferService.UpdateTransferStatusAsync(shipment.Data.TransferId, new UpdateStockTransferStatusRequest
        {
            Status = "Completed"
        });

        deliveryResult.Success.Should().BeTrue();
        deliveryResult.Data!.Status.Should().Be("Completed");

        // 4. Verify Branch Request status is Fulfilled
        var reloadedReq = await distribution.GetBranchRequestByIdAsync(req.Data.BranchRequestId);
        reloadedReq.Data!.Status.Should().Be("Fulfilled");
        reloadedReq.Data.Items[0].ReceivedQuantity.Should().Be(15m);
    }

    [Fact]
    public async Task CreateAndReceiveBranchReturn_tracks_store_defect_returns()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var user = new FakeCurrentUserService(new CurrentUser("BM-01", "Branch Manager Leo", "leo@r3b2p.com", ["BranchManager"], true));
        var distribution = ServiceFactory.BranchDistribution(context, user);

        // 1. Record Branch Return (e.g. broken jars during shelf restocking)
        var returnResult = await distribution.CreateBranchReturnAsync(new CreateBranchReturnRequest
        {
            BranchId = world.BranchManilaId,
            Reason = "Damaged in Store",
            Notes = "Broken jars during shelf display setup",
            Items =
            [
                new CreateBranchReturnItemDto
                {
                    ProductId = world.UbeHalaya500ProductId,
                    ReturnedQuantity = 3m,
                    DefectCondition = "Cracked glass seal",
                    Notes = "Discarded safely"
                }
            ]
        });

        returnResult.Success.Should().BeTrue();
        returnResult.Data!.ReturnNumber.Should().StartWith("RET-");
        returnResult.Data.Status.Should().Be("Pending");
        returnResult.Data.Items.Should().HaveCount(1);
        returnResult.Data.Items[0].ReturnedQuantity.Should().Be(3m);

        // 2. Receive and Acknowledge Return at Commissary
        var receiveResult = await distribution.ReceiveBranchReturnAsync(returnResult.Data.BranchReturnId);
        receiveResult.Success.Should().BeTrue();
        receiveResult.Data!.Status.Should().Be("ReceivedAtWarehouse");
    }
}
