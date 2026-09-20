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
public sealed class CostingAndMasterPlanningTests(ScmDatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task InventoryValuation_calculates_moving_average_cost_and_category_breakdown()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var today = DateOnly.FromDateTime(DateTime.Today);

        // Seed 2 lots of Ube with different unit costs (e.g. 50 kg @ ₱40, 50 kg @ ₱60 -> Total 100 kg, Total Value ₱5,000, MAC = ₱50)
        context.InventoryLots.AddRange(
            new InventoryLot
            {
                LotCode = "LOT-VAL-UBE-01",
                ItemId = world.UbeItemId,
                LocationId = world.MainWarehouseId,
                SupplierId = world.SupplierAId,
                SourceType = LotSourceType.Purchased,
                QuantityReceived = 50m,
                QuantityRemaining = 50m,
                UomId = world.KgUomId,
                UnitCost = 40m,
                Status = LotStatus.Available,
                ExpiryDate = today.AddMonths(3)
            },
            new InventoryLot
            {
                LotCode = "LOT-VAL-UBE-02",
                ItemId = world.UbeItemId,
                LocationId = world.MainWarehouseId,
                SupplierId = world.SupplierBId,
                SourceType = LotSourceType.Purchased,
                QuantityReceived = 50m,
                QuantityRemaining = 50m,
                UomId = world.KgUomId,
                UnitCost = 60m,
                Status = LotStatus.Available,
                ExpiryDate = today.AddMonths(4)
            }
        );
        await context.SaveChangesAsync();

        var valuationService = ServiceFactory.Valuation(context);
        var reportResult = await valuationService.GetValuationReportAsync();

        reportResult.Success.Should().BeTrue();
        reportResult.Data!.TotalValuation.Should().BeGreaterThanOrEqualTo(5000m);
        reportResult.Data.ByCategory.Should().NotBeEmpty();

        var ubeItem = reportResult.Data.Items.FirstOrDefault(i => i.ItemId == world.UbeItemId);
        ubeItem.Should().NotBeNull();
        ubeItem!.OnHandQuantity.Should().Be(100m);
        ubeItem.TotalValue.Should().Be(5000m);
        ubeItem.MovingAverageUnitCost.Should().Be(50m);
    }

    [Fact]
    public async Task CycleCount_records_variances_and_reconciles_inventory_and_ledger()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        // 1. Seed physical lot with 100 units on system
        var lot = new InventoryLot
        {
            LotCode = "LOT-CC-SUGAR-01",
            ItemId = world.SugarItemId,
            LocationId = world.MainWarehouseId,
            SupplierId = world.SupplierAId,
            SourceType = LotSourceType.Purchased,
            QuantityReceived = 100m,
            QuantityRemaining = 100m,
            UomId = world.KgUomId,
            UnitCost = 35m,
            Status = LotStatus.Available
        };
        context.InventoryLots.Add(lot);

        context.Inventories.Add(new Inventory
        {
            ItemId = world.SugarItemId,
            LocationId = world.MainWarehouseId,
            CurrentStock = 100m
        });
        await context.SaveChangesAsync();

        var user = new FakeCurrentUserService(new CurrentUser("WH-01", "Warehouse Auditor Ben", "ben@r3b2p.com", ["WarehouseManager"], true));
        var cycleCountService = ServiceFactory.CycleCounts(context, user);

        // 2. Perform Cycle Count: Physically found 92 kg (variance of -8 kg due to spillage)
        var createResult = await cycleCountService.CreateCycleCountAsync(new CreateCycleCountRequest
        {
            LocationId = world.MainWarehouseId,
            Notes = "Monthly physical count audit",
            Items =
            [
                new CreateCycleCountItemDto
                {
                    ItemId = world.SugarItemId,
                    LotId = lot.LotId,
                    CountedQuantity = 92m,
                    Reason = "Spillage during warehouse shelf transfer"
                }
            ]
        });

        createResult.Success.Should().BeTrue();
        createResult.Data!.CountNumber.Should().StartWith("CC-");
        createResult.Data.TotalVarianceValue.Should().Be(-8m * 35m); // -₱280
        createResult.Data.Items[0].VarianceQuantity.Should().Be(-8m);

        // 3. Reconcile Cycle Count
        var reconcileResult = await cycleCountService.ReconcileCycleCountAsync(
            createResult.Data.CycleCountId, new ReconcileCycleCountRequest
            {
                ReconciliationNotes = "Variance approved by Plant Manager"
            });

        reconcileResult.Success.Should().BeTrue();
        reconcileResult.Data!.Status.Should().Be("Reconciled");

        // 4. Verify adjusted Lot remaining quantity (92)
        await using var verify = CreateContext();
        var reloadedLot = await verify.InventoryLots.FirstAsync(l => l.LotId == lot.LotId);
        reloadedLot.QuantityRemaining.Should().Be(92m);

        // 5. Verify on-hand stock cache updated in Inventories (92)
        var reloadedInv = await verify.Inventories
            .FirstAsync(i => i.ItemId == world.SugarItemId && i.LocationId == world.MainWarehouseId);
        reloadedInv.CurrentStock.Should().Be(92m);

        // 6. Verify StockLedger entry recorded with Adjustment movement type
        var ledgerEntry = await verify.StockLedgers
            .FirstOrDefaultAsync(l => l.ReferenceId == createResult.Data.CountNumber);
        ledgerEntry.Should().NotBeNull();
        ledgerEntry!.MovementType.Should().Be(MovementType.Adjustment);
        ledgerEntry.Quantity.Should().Be(-8m);
    }

    [Fact]
    public async Task MrpService_calculates_gross_net_requirements_and_generates_reorder_suggestions()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        // Configure Item Reorder Point and Supplier pricing
        var ubeItem = await context.Items.FindAsync(world.UbeItemId);
        ubeItem!.MinStockLevel = 50m;
        ubeItem.MaxStockLevel = 200m;
        context.Items.Update(ubeItem);

        context.SupplierItems.Add(new SupplierItem
        {
            SupplierId = world.SupplierAId,
            ItemId = world.UbeItemId,
            UnitPrice = 45m,
            IsPreferred = true,
            PurchaseUomId = world.KgUomId,
            PackSize = 1m
        });

        // Set Current on-hand stock to 20 kg (below MinStockLevel of 50 kg)
        context.Inventories.Add(new Inventory
        {
            ItemId = world.UbeItemId,
            LocationId = world.MainWarehouseId,
            CurrentStock = 20m
        });
        await context.SaveChangesAsync();

        var mrpService = ServiceFactory.Mrp(context);

        // Plan for 2 production runs of Ube Halaya (each batch uses recipe ingredients)
        var planResult = await mrpService.GeneratePlanAsync(new GenerateMrpPlanRequest
        {
            PlanningHorizonDays = 30,
            PlannedProductions =
            [
                new TargetProductionPlanDto
                {
                    RecipeId = world.UbeHalaya500RecipeId,
                    PlannedBatchesCount = 2
                }
            ]
        });

        planResult.Success.Should().BeTrue();
        planResult.Data!.MaterialRequirements.Should().NotBeEmpty();
        planResult.Data.SuggestedReorders.Should().NotBeEmpty();

        var ubeSuggestion = planResult.Data.SuggestedReorders.FirstOrDefault(s => s.ItemId == world.UbeItemId);
        ubeSuggestion.Should().NotBeNull();
        ubeSuggestion!.SuggestedOrderQuantity.Should().BeGreaterThan(0m);
        ubeSuggestion.PreferredSupplierName.Should().Be("Supplier A Farms");
        ubeSuggestion.EstimatedUnitCost.Should().Be(45m);
        ubeSuggestion.UrgencyLevel.Should().Be("Critical");
    }
}
