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
public sealed class TraceabilityAndMockRecallTests(ScmDatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task ForwardTrace_from_raw_material_lot_finds_batches_fg_lots_and_branch_shipments()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var today = DateOnly.FromDateTime(DateTime.Today);

        // 1. Seed raw material lot (Raw Ube)
        var rawLot = new InventoryLot
        {
            LotCode = "LOT-RM-UBE-99",
            ItemId = world.UbeItemId,
            LocationId = world.MainWarehouseId,
            SupplierId = world.SupplierAId,
            SourceType = LotSourceType.Purchased,
            QuantityReceived = 100m,
            QuantityRemaining = 60m,
            UomId = world.KgUomId,
            UnitCost = 45m,
            Status = LotStatus.Available,
            ManufactureDate = today.AddDays(-5),
            ExpiryDate = today.AddMonths(3)
        };
        context.InventoryLots.Add(rawLot);
        await context.SaveChangesAsync();

        // 2. Seed production batch consuming this raw lot
        var batch = new ProductionBatch
        {
            BatchNumber = "MB-2026-0099",
            RecipeId = world.UbeHalaya500RecipeId,
            ProductId = world.UbeHalaya500ProductId,
            ProductionDate = DateTime.UtcNow.AddDays(-1),
            EstimatedQuantity = 40m,
            ActualQuantity = 40m,
            BatchMultiplier = 1m,
            Status = BatchStatus.Completed,
            QualityStatus = QcStatus.Approved,
            AssignedCook = "Chef Mario",
            CompletedDate = DateTime.UtcNow.AddDays(-1)
        };
        context.ProductionBatches.Add(batch);
        await context.SaveChangesAsync();

        var consumption = new BatchConsumption
        {
            BatchId = batch.BatchId,
            ItemId = world.UbeItemId,
            LotId = rawLot.LotId,
            QuantityUsed = 40m,
            UnitCost = 45m,
            UomId = world.KgUomId
        };
        context.BatchConsumptions.Add(consumption);

        // 3. Seed produced FG lot
        var fgLot = new InventoryLot
        {
            LotCode = "LOT-FG-HALAYA-99",
            ItemId = world.UbeHalaya500ItemId,
            LocationId = world.FinishedGoodsLocationId,
            ProductionOrderId = batch.BatchId,
            SourceType = LotSourceType.Produced,
            QuantityReceived = 40m,
            QuantityRemaining = 40m,
            UomId = world.PieceUomId,
            UnitCost = 65m,
            Status = LotStatus.Available,
            ManufactureDate = today.AddDays(-1),
            ExpiryDate = today.AddMonths(6)
        };
        context.InventoryLots.Add(fgLot);
        await context.SaveChangesAsync();

        batch.FgLotId = fgLot.LotId;
        context.ProductionBatches.Update(batch);

        // 4. Seed branch shipment
        var shipment = new StockTransfer
        {
            TransferNumber = "SH-2026-0099",
            ProductId = world.UbeHalaya500ProductId,
            SourceLocationId = world.FinishedGoodsLocationId,
            DestLocationId = world.BranchManilaId,
            TransferQuantity = 20m,
            Status = ShipmentStatus.InTransit,
            DriverName = "Driver Carlos",
            TransferDate = DateTime.UtcNow
        };
        context.StockTransfers.Add(shipment);
        await context.SaveChangesAsync();

        // 5. Run Forward Trace
        var traceability = ServiceFactory.Traceability(context);
        var forwardResult = await traceability.TraceForwardAsync("LOT-RM-UBE-99");

        forwardResult.Success.Should().BeTrue();
        forwardResult.Data!.SourceLot.LotCode.Should().Be("LOT-RM-UBE-99");
        forwardResult.Data.SupplierOrigin.Should().NotBeNull();
        forwardResult.Data.SupplierOrigin!.SupplierName.Should().Be("Supplier A Farms");
        forwardResult.Data.AffectedProductionBatches.Should().HaveCount(1);
        forwardResult.Data.AffectedProductionBatches[0].BatchNumber.Should().Be("MB-2026-0099");
        forwardResult.Data.AffectedFinishedGoodsLots.Should().HaveCount(1);
        forwardResult.Data.AffectedFinishedGoodsLots[0].LotCode.Should().Be("LOT-FG-HALAYA-99");
        forwardResult.Data.DownstreamBranchShipments.Should().HaveCount(1);
        forwardResult.Data.DownstreamBranchShipments[0].DestinationBranchName.Should().Be("Branch Manila");
    }

    [Fact]
    public async Task BackwardTrace_from_fg_lot_finds_batch_ingredients_and_suppliers()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var today = DateOnly.FromDateTime(DateTime.Today);

        // 1. Raw ingredient lots
        var sugarLot = new InventoryLot
        {
            LotCode = "LOT-SUGAR-77",
            ItemId = world.SugarItemId,
            LocationId = world.MainWarehouseId,
            SupplierId = world.SupplierAId,
            SourceType = LotSourceType.Purchased,
            QuantityReceived = 50m,
            QuantityRemaining = 40m,
            UomId = world.KgUomId,
            UnitCost = 40m,
            Status = LotStatus.Available,
            ExpiryDate = today.AddYears(1)
        };
        context.InventoryLots.Add(sugarLot);
        await context.SaveChangesAsync();

        // 2. Production Batch & Consumptions
        var batch = new ProductionBatch
        {
            BatchNumber = "MB-2026-0077",
            RecipeId = world.UbeHalaya500RecipeId,
            ProductId = world.UbeHalaya500ProductId,
            ProductionDate = DateTime.UtcNow.AddDays(-2),
            EstimatedQuantity = 50m,
            ActualQuantity = 50m,
            BatchMultiplier = 1m,
            Status = BatchStatus.Completed,
            QualityStatus = QcStatus.Approved,
            AssignedCook = "Chef Mario"
        };
        context.ProductionBatches.Add(batch);
        await context.SaveChangesAsync();

        context.BatchConsumptions.Add(new BatchConsumption
        {
            BatchId = batch.BatchId,
            ItemId = world.SugarItemId,
            LotId = sugarLot.LotId,
            QuantityUsed = 10m,
            UnitCost = 40m,
            UomId = world.KgUomId
        });

        // 3. FG Lot
        var fgLot = new InventoryLot
        {
            LotCode = "LOT-FG-HALAYA-77",
            ItemId = world.UbeHalaya500ItemId,
            LocationId = world.FinishedGoodsLocationId,
            ProductionOrderId = batch.BatchId,
            SourceType = LotSourceType.Produced,
            QuantityReceived = 50m,
            QuantityRemaining = 50m,
            UomId = world.PieceUomId,
            UnitCost = 60m,
            Status = LotStatus.Available,
            ManufactureDate = today.AddDays(-2),
            ExpiryDate = today.AddMonths(6)
        };
        context.InventoryLots.Add(fgLot);
        await context.SaveChangesAsync();

        batch.FgLotId = fgLot.LotId;
        context.ProductionBatches.Update(batch);
        await context.SaveChangesAsync();

        // 4. Run Backward Trace
        var traceability = ServiceFactory.Traceability(context);
        var backwardResult = await traceability.TraceBackwardAsync("LOT-FG-HALAYA-77");

        backwardResult.Success.Should().BeTrue();
        backwardResult.Data!.FinishedGoodsLot.LotCode.Should().Be("LOT-FG-HALAYA-77");
        backwardResult.Data.ProductionBatch.Should().NotBeNull();
        backwardResult.Data.ProductionBatch!.BatchNumber.Should().Be("MB-2026-0077");
        backwardResult.Data.ConsumedIngredientsAndPackaging.Should().HaveCount(1);
        backwardResult.Data.ConsumedIngredientsAndPackaging[0].LotCode.Should().Be("LOT-SUGAR-77");
        backwardResult.Data.ConsumedIngredientsAndPackaging[0].SupplierName.Should().Be("Supplier A Farms");
    }

    [Fact]
    public async Task MockRecall_simulates_financial_exposure_and_affected_branches()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        // Seed raw material lot
        var badRawLot = new InventoryLot
        {
            LotCode = "LOT-CONTAMINATED-UBE-01",
            ItemId = world.UbeItemId,
            LocationId = world.MainWarehouseId,
            SupplierId = world.SupplierAId,
            SourceType = LotSourceType.Purchased,
            QuantityReceived = 100m,
            QuantityRemaining = 50m,
            UomId = world.KgUomId,
            UnitCost = 50m,
            Status = LotStatus.Available
        };
        context.InventoryLots.Add(badRawLot);
        await context.SaveChangesAsync();

        // Seed Batch
        var batch = new ProductionBatch
        {
            BatchNumber = "MB-2026-RECALL-01",
            RecipeId = world.UbeHalaya500RecipeId,
            ProductId = world.UbeHalaya500ProductId,
            ProductionDate = DateTime.UtcNow.AddDays(-1),
            EstimatedQuantity = 50m,
            ActualQuantity = 50m,
            BatchMultiplier = 1m,
            Status = BatchStatus.Completed,
            QualityStatus = QcStatus.Approved,
            CompletedDate = DateTime.UtcNow.AddDays(-1)
        };
        context.ProductionBatches.Add(batch);
        await context.SaveChangesAsync();

        context.BatchConsumptions.Add(new BatchConsumption
        {
            BatchId = batch.BatchId,
            ItemId = world.UbeItemId,
            LotId = badRawLot.LotId,
            QuantityUsed = 50m,
            UnitCost = 50m,
            UomId = world.KgUomId
        });

        // Seed FG Lot (50 jars @ ₱70 unit cost = ₱3,500 exposure)
        var fgLot = new InventoryLot
        {
            LotCode = "LOT-FG-RECALL-01",
            ItemId = world.UbeHalaya500ItemId,
            LocationId = world.FinishedGoodsLocationId,
            ProductionOrderId = batch.BatchId,
            SourceType = LotSourceType.Produced,
            QuantityReceived = 50m,
            QuantityRemaining = 50m,
            UomId = world.PieceUomId,
            UnitCost = 70m,
            Status = LotStatus.Available
        };
        context.InventoryLots.Add(fgLot);
        await context.SaveChangesAsync();

        batch.FgLotId = fgLot.LotId;
        context.ProductionBatches.Update(batch);

        // Seed shipment to Branch Manila
        context.StockTransfers.Add(new StockTransfer
        {
            TransferNumber = "SH-2026-RECALL-01",
            ProductId = world.UbeHalaya500ProductId,
            SourceLocationId = world.FinishedGoodsLocationId,
            DestLocationId = world.BranchManilaId,
            TransferQuantity = 25m,
            Status = ShipmentStatus.InTransit,
            TransferDate = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var user = new FakeCurrentUserService(new CurrentUser("QA-01", "QA Lead Doctor", "qa@r3b2p.com", ["QualityAssurance"], true));
        var recallService = ServiceFactory.Recall(context, user);

        // Run Mock Recall Simulation without applying hold
        var simResponse = await recallService.SimulateRecallAsync(new SimulateRecallRequest
        {
            LotCode = "LOT-CONTAMINATED-UBE-01",
            Reason = "Microbial foreign object contamination test",
            ApplyQuarantineHold = false
        });

        simResponse.Success.Should().BeTrue();
        simResponse.Data!.AffectedBatchesCount.Should().Be(1);
        simResponse.Data.AffectedFinishedGoodsLotsCount.Should().Be(1);
        simResponse.Data.TotalFinishedGoodsQuantityAtRisk.Should().Be(50m);
        simResponse.Data.TotalEstimatedLoss.Should().Be(3500m);
        simResponse.Data.AffectedBranchesCount.Should().Be(1);
        simResponse.Data.AffectedBranchNames.Should().Contain("Branch Manila");
        simResponse.Data.QuarantineHoldApplied.Should().BeFalse();

        // Verify FG lot remains Available (simulation only)
        await using var verify = CreateContext();
        var reloadedFgLot = await verify.InventoryLots.FirstAsync(l => l.LotCode == "LOT-FG-RECALL-01");
        reloadedFgLot.Status.Should().Be(LotStatus.Available);
    }

    [Fact]
    public async Task MockRecall_with_hold_cascade_quarantines_all_downstream_fg_lots_and_debits_available_stock()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        // 1. Raw Lot
        var badRawLot = new InventoryLot
        {
            LotCode = "LOT-BAD-RAW-02",
            ItemId = world.UbeItemId,
            LocationId = world.MainWarehouseId,
            SupplierId = world.SupplierAId,
            SourceType = LotSourceType.Purchased,
            QuantityReceived = 100m,
            QuantityRemaining = 50m,
            UomId = world.KgUomId,
            UnitCost = 50m,
            Status = LotStatus.Available
        };
        context.InventoryLots.Add(badRawLot);
        await context.SaveChangesAsync();

        // 2. Batch
        var batch = new ProductionBatch
        {
            BatchNumber = "MB-2026-CASCADE-02",
            RecipeId = world.UbeHalaya500RecipeId,
            ProductId = world.UbeHalaya500ProductId,
            ProductionDate = DateTime.UtcNow.AddDays(-1),
            EstimatedQuantity = 40m,
            ActualQuantity = 40m,
            BatchMultiplier = 1m,
            Status = BatchStatus.Completed,
            QualityStatus = QcStatus.Approved,
            CompletedDate = DateTime.UtcNow.AddDays(-1)
        };
        context.ProductionBatches.Add(batch);
        await context.SaveChangesAsync();

        context.BatchConsumptions.Add(new BatchConsumption
        {
            BatchId = batch.BatchId,
            ItemId = world.UbeItemId,
            LotId = badRawLot.LotId,
            QuantityUsed = 40m,
            UnitCost = 50m,
            UomId = world.KgUomId
        });

        // 3. FG Lot + Inventory balance
        var fgLot = new InventoryLot
        {
            LotCode = "LOT-FG-CASCADE-02",
            ItemId = world.UbeHalaya500ItemId,
            LocationId = world.FinishedGoodsLocationId,
            ProductionOrderId = batch.BatchId,
            SourceType = LotSourceType.Produced,
            QuantityReceived = 40m,
            QuantityRemaining = 40m,
            UomId = world.PieceUomId,
            UnitCost = 70m,
            Status = LotStatus.Available
        };
        context.InventoryLots.Add(fgLot);

        context.Inventories.Add(new Inventory
        {
            ItemId = world.UbeHalaya500ItemId,
            LocationId = world.FinishedGoodsLocationId,
            CurrentStock = 40m
        });
        await context.SaveChangesAsync();

        batch.FgLotId = fgLot.LotId;
        context.ProductionBatches.Update(batch);
        await context.SaveChangesAsync();

        var user = new FakeCurrentUserService(new CurrentUser("QA-01", "QA Lead Doctor", "qa@r3b2p.com", ["QualityAssurance"], true));
        var recallService = ServiceFactory.Recall(context, user);

        // 4. Run Recall with Active Hold Cascade
        var cascadeResponse = await recallService.SimulateRecallAsync(new SimulateRecallRequest
        {
            LotCode = "LOT-BAD-RAW-02",
            Reason = "Pesticide residue test failure - immediate hold required",
            ApplyQuarantineHold = true
        });

        cascadeResponse.Success.Should().BeTrue();
        cascadeResponse.Data!.QuarantineHoldApplied.Should().BeTrue();

        // 5. Verify FG lot transitioned to Quarantine
        await using var verify = CreateContext();
        var reloadedFgLot = await verify.InventoryLots.FirstAsync(l => l.LotCode == "LOT-FG-CASCADE-02");
        reloadedFgLot.Status.Should().Be(LotStatus.Quarantine, "Hold cascade places downstream FG lot in quarantine");

        // 6. Verify Finished Goods available stock cache was debited to 0
        var fgInv = await verify.Inventories
            .FirstAsync(i => i.ItemId == world.UbeHalaya500ItemId && i.LocationId == world.FinishedGoodsLocationId);
        fgInv.CurrentStock.Should().Be(0m, "Quarantined stock is immediately removed from available on-hand balance");
    }
}
