using Api.Contracts.Production;
using api_scm.Tests.Infrastructure;
using Domains.Enums;
using Domains.Exceptions;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;

namespace api_scm.Tests.Characterization;

/// <summary>
/// Pins the behaviour of production: what it consumes, from where, and what it fails to record.
/// Tests named "FIXED in Task N" document behaviour the rebuild has already corrected; the rest still
/// describe open defects.
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

    [Fact(DisplayName = "Defect 01: BatchConsumption is never written - traceability is dead code")]
    public async Task Defect01_No_Consumption_Records_Are_Written()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        await TestDataSeeder.GiveStockAsync(context, world.UbeItemId, world.MainWarehouseId, 50);
        await TestDataSeeder.GiveStockAsync(context, world.SugarItemId, world.MainWarehouseId, 50);

        await StartBatchAsync(context, world);

        await using var verify = CreateContext();
        var consumptions = await verify.BatchConsumptions.CountAsync();

        consumptions.Should().Be(0,
            "the table exists in the ERD and DbContext but no service ever inserts into it, " +
            "so there is no record of which material went into which batch. Task 28 fixes this");
    }

    [Fact(DisplayName = "Defect 02: production consumes stock from ANY location, including branches")]
    public async Task Defect02_Consumption_Ignores_Location()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        // Nothing at the warehouse. Everything sits at a retail branch and in finished goods.
        await TestDataSeeder.GiveStockAsync(context, world.UbeItemId, world.BranchManilaId, 10);
        await TestDataSeeder.GiveStockAsync(context, world.SugarItemId, world.FinishedGoodsLocationId, 5);

        await StartBatchAsync(context, world);

        await using var verify = CreateContext();
        var branchUbe = await verify.Inventories
            .SingleAsync(i => i.ItemId == world.UbeItemId && i.LocationId == world.BranchManilaId);
        var finishedGoodsSugar = await verify.Inventories
            .SingleAsync(i => i.ItemId == world.SugarItemId && i.LocationId == world.FinishedGoodsLocationId);

        branchUbe.CurrentStock.Should().Be(0m,
            "the query filters only on ItemId and CurrentStock > 0, so a Manila branch was drained " +
            "by a batch cooked at the commissary");
        finishedGoodsSugar.CurrentStock.Should().Be(0m,
            "finished-goods stock is equally fair game");
    }

    [Fact(DisplayName = "Defect 03: consumption order is LocationId, not FIFO or FEFO")]
    public async Task Defect03_Consumption_Order_Is_Arbitrary()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        // Give the LOWEST LocationId the newest stock. A FIFO or FEFO system would not touch it first,
        // but OrderBy(LocationId) does exactly that.
        await TestDataSeeder.GiveStockAsync(context, world.UbeItemId, world.MainWarehouseId, 10);
        await TestDataSeeder.GiveStockAsync(context, world.UbeItemId, world.BranchManilaId, 10);
        await TestDataSeeder.GiveStockAsync(context, world.SugarItemId, world.MainWarehouseId, 10);

        await StartBatchAsync(context, world);

        await using var verify = CreateContext();
        var main = await verify.Inventories
            .SingleAsync(i => i.ItemId == world.UbeItemId && i.LocationId == world.MainWarehouseId);
        var branch = await verify.Inventories
            .SingleAsync(i => i.ItemId == world.UbeItemId && i.LocationId == world.BranchManilaId);

        main.CurrentStock.Should().Be(0m, "lowest LocationId is drained first");
        branch.CurrentStock.Should().Be(10m, "higher LocationId is untouched");

        // There is no received date and no expiry on a balance, so neither FIFO nor FEFO is even
        // expressible. Ordering by a surrogate key is the only option the model allows.
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

        // Enough ube, no sugar at all. Ube is the first ingredient, so it is deducted before the loop
        // reaches sugar and fails.
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

        ube.CurrentStock.Should().Be(10m,
            "the ube deduction is rolled back with the rest of the posting. Before Task 5 this " +
            "happened to hold only because the exception escaped before the single SaveChanges call");

        // The stage change staged alongside the deductions is rolled back too.
        var stored = await verify.ProductionBatches.SingleAsync(b => b.BatchId == batch.BatchId);
        stored.Status.Should().Be(BatchStatus.Scheduled, "the batch was never really started");
    }

    [Fact(DisplayName = "Defect 18: posting a batch to inventory mutates the item's category")]
    public async Task Defect18_AddToInventory_Mutates_Master_Data()
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
        await service.UpdateStageAsync(batchId, new UpdateStageRequest { Stage = "QA Review", ActualQuantity = 20m });
        await service.UpdateQaApprovalAsync(batchId, new UpdateQaApprovalRequest { IsApproved = true });
        await service.AddBatchToInventoryAsync(batchId);

        await using var verify = CreateContext();
        var after = await verify.Items.SingleAsync(i => i.ItemId == world.UbeHalaya500ItemId);

        after.CategoryId.Should().Be(world.FinishedGoodCategoryId,
            "a stock posting silently rewrote master data so that a UI tab query would find the row. " +
            "Task 31 removes this side effect");
    }

    [Fact(DisplayName = "Defect 19/22: rejecting a batch loses the ingredients; packaging never moves")]
    public async Task Defect19And22_Rejected_Batch_Loses_Material_And_Packaging_Is_Untouched()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        await TestDataSeeder.GiveStockAsync(context, world.UbeItemId, world.MainWarehouseId, 10);
        await TestDataSeeder.GiveStockAsync(context, world.SugarItemId, world.MainWarehouseId, 5);
        await TestDataSeeder.GiveStockAsync(context, world.Jar500ItemId, world.MainWarehouseId, 100);
        await TestDataSeeder.GiveStockAsync(context, world.LidItemId, world.MainWarehouseId, 100);

        var service = ServiceFactory.Production(context);
        var batchId = await StartBatchAsync(context, world);

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

        // Consumed, rejected, and now simply gone: no scrap document, no reversal, no cost of loss.
        var creditRecords = await verify.InventoryMovementLogs.CountAsync(l => l.ChangeQuantity > 0);
        creditRecords.Should().Be(0, "nothing records what happened to the lost material. Task 33 fixes this");

        // Packaging is a stage name only. Jars and lids are never consumed.
        var jars = await verify.Inventories.SingleAsync(i => i.ItemId == world.Jar500ItemId);
        var lids = await verify.Inventories.SingleAsync(i => i.ItemId == world.LidItemId);
        jars.CurrentStock.Should().Be(100m, "Tools and Supplies inventory never moves. Task 31 fixes this");
        lids.CurrentStock.Should().Be(100m);
    }

    [Fact(DisplayName = "Defect 21: no lot, expiry, or shelf life exists anywhere in the model")]
    public async Task Defect21_No_Lot_Or_Expiry_Concept()
    {
        await using var context = CreateContext();
        await TestDataSeeder.SeedBaselineAsync(context);

        var inventoryProperties = context.Model
            .FindEntityType(typeof(Domains.Entities.Inventory))!
            .GetProperties()
            .Select(p => p.Name)
            .ToArray();

        inventoryProperties.Should().NotContain("ExpiryDate");
        inventoryProperties.Should().NotContain("LotCode");
        inventoryProperties.Should().NotContain("SupplierId");
        inventoryProperties.Should().NotContain("ReceivedDate");

        // A stock balance is a bare number with no identity, origin, age, or cost. Every gap the
        // rebuild addresses - traceability, FEFO, expiry, partial QA, bad-supplier attribution -
        // follows from this single fact. Task 8 introduces InventoryLot.
        inventoryProperties.Should().Contain("CurrentStock");
    }
}
