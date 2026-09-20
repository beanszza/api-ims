using System;
using System.Linq;
using System.Threading.Tasks;
using Api.Contracts.Production;
using api_scm.Tests.Infrastructure;
using Domains.Enums;
using Domains.Exceptions;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;
using FluentAssertions;

namespace api_scm.Tests.Characterization;

/// <summary>
/// Pins the behaviour of production: what it consumes, from where, and lot-level consumption tracking.
/// </summary>
[Collection(ScmDatabaseCollection.Name)]
public sealed class ProductionConsumptionCharacterizationTests(ScmDatabaseFixture fixture)
    : DatabaseTestBase(fixture)
{
    private static async Task<int> StartBatchAsync(
        ScmDbContext context,
        SeededWorld world,
        decimal batchMultiplier = 1m)
    {
        var service = ServiceFactory.Production(context);
        var batch = await service.CreateBatchAsync(new CreateProductionBatchRequest
        {
            RecipeId = world.UbeHalaya500RecipeId,
            ProductId = world.UbeHalaya500ProductId,
            BatchMultiplier = batchMultiplier,
            ScheduleDate = DateTime.UtcNow,
            AssignedCook = "Cook One"
        });

        await service.UpdateStageAsync(batch.BatchId, new UpdateStageRequest { Stage = "Cooking" });
        return batch.BatchId;
    }

    [Fact(DisplayName = "Defect 01 FIXED in Task 28: BatchConsumption records are written on batch start")]
    public async Task Defect01_Fixed_BatchConsumption_Records_Are_Written()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        await TestDataSeeder.GiveStockAsync(context, world.UbeItemId, world.MainWarehouseId, 50);
        await TestDataSeeder.GiveStockAsync(context, world.SugarItemId, world.MainWarehouseId, 50);

        var batchId = await StartBatchAsync(context, world);

        await using var verify = CreateContext();
        var consumptions = await verify.BatchConsumptions.Where(c => c.BatchId == batchId).ToListAsync();

        consumptions.Should().HaveCount(2, "Task 28 logs a BatchConsumption row for each ingredient consumed");
        consumptions.Should().Contain(c => c.ItemId == world.UbeItemId && c.QuantityUsed == 10m);
        consumptions.Should().Contain(c => c.ItemId == world.SugarItemId && c.QuantityUsed == 5m);
    }

    [Fact(DisplayName = "Defect 02 FIXED in Task 28: production requires warehouse stock and does not drain branches")]
    public async Task Defect02_Fixed_Consumption_Requires_Warehouse_Stock()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        // Nothing at the warehouse. Everything sits at a retail branch and in finished goods.
        await TestDataSeeder.GiveStockAsync(context, world.UbeItemId, world.BranchManilaId, 10);
        await TestDataSeeder.GiveStockAsync(context, world.SugarItemId, world.FinishedGoodsLocationId, 5);

        var act = async () => await StartBatchAsync(context, world);

        await act.Should().ThrowAsync<InsufficientStockException>(
            "Production must only consume raw materials from Commissary Warehouse, never branch inventory");
    }

    [Fact(DisplayName = "Defect 03 FIXED in Task 28: consumption strictly draws warehouse stock via FEFO")]
    public async Task Defect03_Fixed_Consumption_Draws_Warehouse_Stock()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        await TestDataSeeder.GiveStockAsync(context, world.UbeItemId, world.MainWarehouseId, 10);
        await TestDataSeeder.GiveStockAsync(context, world.UbeItemId, world.BranchManilaId, 10);
        await TestDataSeeder.GiveStockAsync(context, world.SugarItemId, world.MainWarehouseId, 10);

        await StartBatchAsync(context, world);

        await using var verify = CreateContext();
        var main = await verify.Inventories
            .SingleAsync(i => i.ItemId == world.UbeItemId && i.LocationId == world.MainWarehouseId);
        var branch = await verify.Inventories
            .SingleAsync(i => i.ItemId == world.UbeItemId && i.LocationId == world.BranchManilaId);

        main.CurrentStock.Should().Be(0m, "Main warehouse raw material is consumed");
        branch.CurrentStock.Should().Be(10m, "Branch inventory is untouched");
    }

    [Fact(DisplayName = "Defect 04 FIXED in Task 3: quantities are decimal, so fractions survive")]
    public async Task Defect04_Fixed_Quantities_Are_Now_Decimal()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var ingredient = await context.RecipeIngredients
            .SingleAsync(i => i.ItemId == world.UbeItemId);

        ingredient.StandardQuantity.GetType().Should().Be(typeof(decimal),
            "Task 3 converted every stock quantity to decimal(18,3)");

        // A recipe needing 10.5 kg is now expressible, and consumption tracks it exactly.
        ingredient.StandardQuantity = 10.5m;
        await context.SaveChangesAsync();

        await TestDataSeeder.GiveStockAsync(context, world.UbeItemId, world.MainWarehouseId, 12);
        await TestDataSeeder.GiveStockAsync(context, world.SugarItemId, world.MainWarehouseId, 10);

        await StartBatchAsync(context, world);

        await using var verify = CreateContext();
        var ube = await verify.Inventories
            .SingleAsync(i => i.ItemId == world.UbeItemId && i.LocationId == world.MainWarehouseId);

        ube.CurrentStock.Should().Be(1.5m,
            "12 kg less a 10.5 kg requirement leaves 1.5 kg; as an int this rounded to 2 kg remaining");
    }

    [Fact(DisplayName = "Defect 05 FIXED in Task 4: a recipe in grams draws the right kilograms")]
    public async Task Defect05_Fixed_Recipe_Uom_Is_Converted_To_The_Items_Stocking_Unit()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        // Ube is stocked in kilograms. Express the recipe requirement in grams instead.
        var ingredient = await context.RecipeIngredients
            .SingleAsync(i => i.ItemId == world.UbeItemId);
        ingredient.UomId = world.GramUomId;
        ingredient.StandardQuantity = 10_000m; // 10 000 g == 10 kg
        await context.SaveChangesAsync();

        await TestDataSeeder.GiveStockAsync(context, world.UbeItemId, world.MainWarehouseId, 25);
        await TestDataSeeder.GiveStockAsync(context, world.SugarItemId, world.MainWarehouseId, 10);

        await StartBatchAsync(context, world);

        await using var verify = CreateContext();
        var onHand = await verify.Inventories
            .SingleAsync(i => i.ItemId == world.UbeItemId && i.LocationId == world.MainWarehouseId);

        onHand.CurrentStock.Should().Be(15m,
            "25 kg less a 10 000 g (= 10 kg) requirement leaves 15 kg. Before Task 4 the service " +
            "subtracted the number 10 000 from a kilogram balance, a thousand-fold error");
    }

    [Fact(DisplayName = "Defect 05 FIXED in Task 4: an unconvertible recipe unit is refused up front")]
    public async Task Defect05_Fixed_Cross_Dimension_Consumption_Is_Refused()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        // Ube is a weight. Ask for it in litres.
        var litres = await context.UnitOfMeasures
            .SingleAsync(u => u.Code == UnitOfMeasureSeeder.Codes.Litre);

        var ingredient = await context.RecipeIngredients
            .SingleAsync(i => i.ItemId == world.UbeItemId);
        ingredient.UomId = litres.UomId;
        await context.SaveChangesAsync();

        await TestDataSeeder.GiveStockAsync(context, world.UbeItemId, world.MainWarehouseId, 100);
        await TestDataSeeder.GiveStockAsync(context, world.SugarItemId, world.MainWarehouseId, 100);

        var act = async () => await StartBatchAsync(context, world);

        await act.Should().ThrowAsync<UomConversionException>(
            "consuming litres of a kilogram-stocked item is nonsense and must stop the batch");

        await using var verify = CreateContext();
        var onHand = await verify.Inventories
            .SingleAsync(i => i.ItemId == world.UbeItemId && i.LocationId == world.MainWarehouseId);
        onHand.CurrentStock.Should().Be(100m, "and nothing may be deducted on the way out");
    }

    [Fact(DisplayName = "Task 5: a shortage found mid-deduction rolls back the earlier deductions")]
    public async Task Task05_Shortage_Rolls_Back_Every_Deduction()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        // Enough ube, no sugar at all.
        await TestDataSeeder.GiveStockAsync(context, world.UbeItemId, world.MainWarehouseId, 10);

        var service = ServiceFactory.Production(context);
        var batch = await service.CreateBatchAsync(new CreateProductionBatchRequest
        {
            RecipeId = world.UbeHalaya500RecipeId,
            ProductId = world.UbeHalaya500ProductId,
            BatchMultiplier = 1m,
            ScheduleDate = DateTime.UtcNow,
            AssignedCook = "Cook One"
        });

        var act = async () => await service.UpdateStageAsync(
            batch.BatchId, new UpdateStageRequest { Stage = "Cooking" });

        await act.Should().ThrowAsync<Exception>().WithMessage("*Insufficient stock*Sugar*");

        await using var verify = CreateContext();
        var ube = await verify.Inventories
            .SingleAsync(i => i.ItemId == world.UbeItemId && i.LocationId == world.MainWarehouseId);

        ube.CurrentStock.Should().Be(10m, "the ube deduction is rolled back when sugar is short");

        // The stage change staged alongside the deductions is rolled back too.
        var stored = await verify.ProductionBatches.SingleAsync(b => b.BatchId == batch.BatchId);
        stored.Status.Should().Be(BatchStatus.Scheduled, "the batch was never really started");
    }

    [Fact(DisplayName = "Defect 18 FIXED in Task 7: posting a batch no longer rewrites master data")]
    public async Task Defect18_Fixed_AddToInventory_Leaves_Master_Data_Alone()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        await TestDataSeeder.GiveStockAsync(context, world.UbeItemId, world.MainWarehouseId, 50);
        await TestDataSeeder.GiveStockAsync(context, world.SugarItemId, world.MainWarehouseId, 50);

        // Deliberately mis-categorise the finished good as a raw material.
        var item = await context.Items.SingleAsync(i => i.ItemId == world.UbeHalaya500ItemId);
        item.CategoryId = world.RawMaterialCategoryId;
        await context.SaveChangesAsync();

        var service = ServiceFactory.Production(context);
        var batchId = await StartBatchAsync(context, world);
        await service.UpdateStageAsync(batchId, new UpdateStageRequest { Stage = "Completed", ActualQuantity = 20m });
        await service.UpdateQaApprovalAsync(batchId, new UpdateQaApprovalRequest { IsApproved = true });
        await service.AddBatchToInventoryAsync(batchId);

        await using var verify = CreateContext();
        var after = await verify.Items.SingleAsync(i => i.ItemId == world.UbeHalaya500ItemId);

        after.CategoryId.Should().Be(world.RawMaterialCategoryId,
            "the mis-categorisation is left exactly as it was found. Master data is preserved");

        // The finished goods location is credited properly
        var posted = await verify.Inventories
            .SingleAsync(i => i.ItemId == world.UbeHalaya500ItemId && i.LocationId == world.FinishedGoodsLocationId);
        posted.CurrentStock.Should().Be(20m);
    }

    [Fact(DisplayName = "Defect 19/22: rejecting a batch sets rejected QA and locks FG lot from inventory")]
    public async Task Defect19And22_Rejected_Batch_Sets_Rejected_Status()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        await TestDataSeeder.GiveStockAsync(context, world.UbeItemId, world.MainWarehouseId, 10);
        await TestDataSeeder.GiveStockAsync(context, world.SugarItemId, world.MainWarehouseId, 5);

        var service = ServiceFactory.Production(context);
        var batchId = await StartBatchAsync(context, world);
        await service.UpdateStageAsync(batchId, new UpdateStageRequest { Stage = "Completed", ActualQuantity = 20m });

        await service.UpdateQaApprovalAsync(batchId, new UpdateQaApprovalRequest
        {
            IsApproved = false,
            RejectionReason = "Off flavour"
        });

        await using var verify = CreateContext();

        var ube = await verify.Inventories.SingleAsync(i => i.ItemId == world.UbeItemId);
        var sugar = await verify.Inventories.SingleAsync(i => i.ItemId == world.SugarItemId);
        ube.CurrentStock.Should().Be(0m);
        sugar.CurrentStock.Should().Be(0m);

        var batch = await verify.ProductionBatches.Include(b => b.FgLot).SingleAsync(b => b.BatchId == batchId);
        batch.Status.Should().Be(BatchStatus.Rejected);
        batch.QualityStatus.Should().Be(QcStatus.Rejected);
        batch.FgLot!.Status.Should().Be(LotStatus.Rejected);

        var fgInv = await verify.Inventories.FirstOrDefaultAsync(i => i.ItemId == world.UbeHalaya500ItemId && i.LocationId == world.FinishedGoodsLocationId);
        fgInv?.CurrentStock.Should().Be(0m, "Rejected batch is never credited to available finished goods inventory");
    }

    [Fact(DisplayName = "Defect 21: InventoryLot provides full lot and expiry tracking")]
    public async Task Defect21_Lot_Tracking_Available_In_Model()
    {
        await using var context = CreateContext();
        await TestDataSeeder.SeedBaselineAsync(context);

        var lotProperties = context.Model
            .FindEntityType(typeof(Domains.Entities.InventoryLot))!
            .GetProperties()
            .Select(p => p.Name)
            .ToArray();

        lotProperties.Should().Contain("ExpiryDate");
        lotProperties.Should().Contain("LotCode");
        lotProperties.Should().Contain("SupplierId");
        lotProperties.Should().Contain("ReceivedDate");
        lotProperties.Should().Contain("QuantityRemaining");
        lotProperties.Should().Contain("Status");
    }
}
