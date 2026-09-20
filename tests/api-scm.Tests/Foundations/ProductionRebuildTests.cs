using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Api.Contracts.Production;
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
public sealed class ProductionRebuildTests(ScmDatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task CreateBatch_assigns_canonical_number_and_schedules_run()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var production = ServiceFactory.Production(context);

        var response = await production.CreateBatchAsync(new CreateProductionBatchRequest
        {
            RecipeId = world.UbeHalaya500RecipeId,
            ProductId = world.UbeHalaya500ProductId,
            BatchMultiplier = 2.0m,
            ScheduleDate = DateTime.UtcNow,
            AssignedCook = "Chef Gordon"
        });

        response.BatchNumber.Should().StartWith("MB-");
        response.EstimatedQuantity.Should().Be(40m, "Recipe output 20 * 2.0 multiplier = 40");
        response.Status.Should().Be("Scheduled");
        response.QualityStatus.Should().Be("Pending");
    }

    [Fact]
    public async Task StartBatch_consumes_ingredients_via_fefo_and_records_batch_consumption()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var today = DateOnly.FromDateTime(DateTime.Today);

        // Seed available ingredient lots:
        // Ube (Recipe needs 10kg * 1 = 10kg)
        var ubeLot = new InventoryLot
        {
            LotCode = "LOT-UBE-PROD-01",
            ItemId = world.UbeItemId,
            LocationId = world.MainWarehouseId,
            SourceType = LotSourceType.Purchased,
            SupplierId = world.SupplierAId,
            QuantityReceived = 15m,
            QuantityRemaining = 15m,
            UomId = world.KgUomId,
            UnitCost = 80m,
            Status = LotStatus.Available,
            ExpiryDate = today.AddDays(30)
        };

        // Sugar (Recipe needs 5kg * 1 = 5kg)
        var sugarLot = new InventoryLot
        {
            LotCode = "LOT-SUGAR-PROD-01",
            ItemId = world.SugarItemId,
            LocationId = world.MainWarehouseId,
            SourceType = LotSourceType.Purchased,
            SupplierId = world.SupplierAId,
            QuantityReceived = 10m,
            QuantityRemaining = 10m,
            UomId = world.KgUomId,
            UnitCost = 45m,
            Status = LotStatus.Available,
            ExpiryDate = today.AddDays(90)
        };

        context.InventoryLots.AddRange(ubeLot, sugarLot);

        context.Inventories.AddRange(
            new Inventory { ItemId = world.UbeItemId, LocationId = world.MainWarehouseId, CurrentStock = 15m },
            new Inventory { ItemId = world.SugarItemId, LocationId = world.MainWarehouseId, CurrentStock = 10m }
        );
        await context.SaveChangesAsync();

        var production = ServiceFactory.Production(context);

        // 1. Create Batch
        var batchResponse = await production.CreateBatchAsync(new CreateProductionBatchRequest
        {
            RecipeId = world.UbeHalaya500RecipeId,
            ProductId = world.UbeHalaya500ProductId,
            BatchMultiplier = 1.0m,
            ScheduleDate = DateTime.UtcNow,
            AssignedCook = "Chef Marco"
        });

        // 2. Start Batch (Cooking stage triggers ingredient deduction)
        var startResponse = await production.UpdateStageAsync(batchResponse.BatchId, new UpdateStageRequest
        {
            Stage = "Cooking"
        });

        startResponse.Status.Should().Be("In Progress");

        // 3. Verify stock balances were deducted
        await using var verify = CreateContext();
        var reloadedUbeLot = await verify.InventoryLots.FindAsync(ubeLot.LotId);
        reloadedUbeLot!.QuantityRemaining.Should().Be(5m, "15 - 10 = 5kg remaining");

        var reloadedSugarLot = await verify.InventoryLots.FindAsync(sugarLot.LotId);
        reloadedSugarLot!.QuantityRemaining.Should().Be(5m, "10 - 5 = 5kg remaining");

        // 4. Verify BatchConsumption rows were recorded
        var consumptions = await verify.BatchConsumptions.Where(c => c.BatchId == batchResponse.BatchId).ToListAsync();
        consumptions.Should().HaveCount(2);
        consumptions.Should().Contain(c => c.ItemId == world.UbeItemId && c.QuantityUsed == 10m && c.UnitCost == 80m);
        consumptions.Should().Contain(c => c.ItemId == world.SugarItemId && c.QuantityUsed == 5m && c.UnitCost == 45m);

        // Verify Batch total material cost = (10*80) + (5*45) = 800 + 225 = 1025
        var reloadedBatch = await verify.ProductionBatches.FindAsync(batchResponse.BatchId);
        reloadedBatch!.TotalMaterialCost.Should().Be(1025m);
    }

    [Fact]
    public async Task CompleteBatch_stages_fg_lot_in_quarantine_and_qa_release_enables_stock()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        // Seed ingredient stock
        var ubeLot = new InventoryLot
        {
            LotCode = "LOT-UBE-02",
            ItemId = world.UbeItemId,
            LocationId = world.MainWarehouseId,
            SourceType = LotSourceType.Purchased,
            SupplierId = world.SupplierAId,
            QuantityReceived = 10m,
            QuantityRemaining = 10m,
            UomId = world.KgUomId,
            UnitCost = 80m,
            Status = LotStatus.Available
        };
        var sugarLot = new InventoryLot
        {
            LotCode = "LOT-SUGAR-02",
            ItemId = world.SugarItemId,
            LocationId = world.MainWarehouseId,
            SourceType = LotSourceType.Purchased,
            SupplierId = world.SupplierAId,
            QuantityReceived = 10m,
            QuantityRemaining = 10m,
            UomId = world.KgUomId,
            UnitCost = 50m,
            Status = LotStatus.Available
        };

        context.InventoryLots.AddRange(ubeLot, sugarLot);
        context.Inventories.AddRange(
            new Inventory { ItemId = world.UbeItemId, LocationId = world.MainWarehouseId, CurrentStock = 10m },
            new Inventory { ItemId = world.SugarItemId, LocationId = world.MainWarehouseId, CurrentStock = 10m }
        );
        await context.SaveChangesAsync();

        var production = ServiceFactory.Production(context);

        // 1. Create and Start Batch
        var batch = await production.CreateBatchAsync(new CreateProductionBatchRequest
        {
            RecipeId = world.UbeHalaya500RecipeId,
            ProductId = world.UbeHalaya500ProductId,
            BatchMultiplier = 1.0m,
            ScheduleDate = DateTime.UtcNow,
            AssignedCook = "Chef Pierre"
        });

        await production.UpdateStageAsync(batch.BatchId, new UpdateStageRequest { Stage = "Cooking" });

        // 2. Complete Batch (Yield = 20 jars)
        var completeResponse = await production.UpdateStageAsync(batch.BatchId, new UpdateStageRequest
        {
            Stage = "Completed",
            ActualQuantity = 20m
        });

        completeResponse.Status.Should().Be("Completed");

        // 3. Verify Finished Goods Lot exists in Quarantine (NOT yet in available inventory)
        await using var verify = CreateContext();
        var reloadedBatch = await verify.ProductionBatches.Include(b => b.FgLot).FirstAsync(b => b.BatchId == batch.BatchId);
        reloadedBatch.FgLot.Should().NotBeNull();
        reloadedBatch.FgLot!.Status.Should().Be(LotStatus.Quarantine, "FG lot must be staged in Quarantine upon completion");
        reloadedBatch.FgLot.QuantityRemaining.Should().Be(20m);
        reloadedBatch.YieldPercentage.Should().Be(100m);
        reloadedBatch.UnitCost.Should().Be(52.5m, "(10*80 + 5*50) / 20 = (800 + 250) / 20 = 1050 / 20 = 52.50");

        // Available on-hand finished goods inventory should still be 0
        var fgInventoryBeforeQa = await verify.Inventories
            .FirstOrDefaultAsync(i => i.ItemId == world.UbeHalaya500ItemId && i.LocationId == world.FinishedGoodsLocationId);
        fgInventoryBeforeQa?.CurrentStock.Should().Be(0m);

        // 4. QA Inspection & Release
        var qaResponse = await production.UpdateQaApprovalAsync(batch.BatchId, new UpdateQaApprovalRequest
        {
            IsApproved = true
        });

        qaResponse.QualityStatus.Should().Be("Approved");
        qaResponse.Status.Should().Be("Passed QA");

        // 5. Verify FG Lot is now Available and credited to Finished Goods Inventory
        await using var verifyAfterQa = CreateContext();
        var releasedLot = await verifyAfterQa.InventoryLots.FindAsync(reloadedBatch.FgLotId);
        releasedLot!.Status.Should().Be(LotStatus.Available);

        var fgInventoryAfterQa = await verifyAfterQa.Inventories
            .FirstAsync(i => i.ItemId == world.UbeHalaya500ItemId && i.LocationId == world.FinishedGoodsLocationId);
        fgInventoryAfterQa.CurrentStock.Should().Be(20m, "QA approval must credit available finished goods stock");
    }
}
