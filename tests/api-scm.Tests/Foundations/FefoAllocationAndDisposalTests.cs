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
public sealed class FefoAllocationAndDisposalTests(ScmDatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task AllocateFefo_picks_earliest_expiring_lots_first()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var today = DateOnly.FromDateTime(DateTime.Today);

        // Seed 2 available lots of Sugar with different expiry dates
        var lotLater = new InventoryLot
        {
            LotCode = "LOT-SUGAR-LATER",
            ItemId = world.SugarItemId,
            LocationId = world.MainWarehouseId,
            SourceType = LotSourceType.Purchased,
            SupplierId = world.SupplierAId,
            QuantityReceived = 50m,
            QuantityRemaining = 50m,
            UomId = world.KgUomId,
            Status = LotStatus.Available,
            ExpiryDate = today.AddDays(60),
            ReceivedDate = DateTime.UtcNow.AddDays(-10)
        };

        var lotSooner = new InventoryLot
        {
            LotCode = "LOT-SUGAR-SOONER",
            ItemId = world.SugarItemId,
            LocationId = world.MainWarehouseId,
            SourceType = LotSourceType.Purchased,
            SupplierId = world.SupplierAId,
            QuantityReceived = 30m,
            QuantityRemaining = 30m,
            UomId = world.KgUomId,
            Status = LotStatus.Available,
            ExpiryDate = today.AddDays(15), // Expiring sooner!
            ReceivedDate = DateTime.UtcNow.AddDays(-5)
        };

        context.InventoryLots.AddRange(lotLater, lotSooner);
        await context.SaveChangesAsync();

        var allocationService = ServiceFactory.Allocation(context);

        // Allocate 20 kg
        var result = await allocationService.AllocateFefoAsync(world.SugarItemId, world.MainWarehouseId, 20m);

        result.IsFulfilled.Should().BeTrue();
        result.AllocatedQuantity.Should().Be(20m);
        result.ShortfallQuantity.Should().Be(0m);
        result.Allocations.Should().HaveCount(1);
        result.Allocations[0].LotCode.Should().Be("LOT-SUGAR-SOONER", "FEFO must prioritize earliest expiring lot");
        result.Allocations[0].QuantityToDraw.Should().Be(20m);
    }

    [Fact]
    public async Task AllocateFefo_handles_partial_and_multi_lot_draws_accurately()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var today = DateOnly.FromDateTime(DateTime.Today);

        // Lot A: 25 kg (expiring in 10 days)
        var lotA = new InventoryLot
        {
            LotCode = "LOT-UBE-A",
            ItemId = world.UbeItemId,
            LocationId = world.MainWarehouseId,
            SourceType = LotSourceType.Purchased,
            SupplierId = world.SupplierAId,
            QuantityReceived = 25m,
            QuantityRemaining = 25m,
            UomId = world.KgUomId,
            Status = LotStatus.Available,
            ExpiryDate = today.AddDays(10),
            ReceivedDate = DateTime.UtcNow.AddDays(-15)
        };

        // Lot B: 50 kg (expiring in 30 days)
        var lotB = new InventoryLot
        {
            LotCode = "LOT-UBE-B",
            ItemId = world.UbeItemId,
            LocationId = world.MainWarehouseId,
            SourceType = LotSourceType.Purchased,
            SupplierId = world.SupplierAId,
            QuantityReceived = 50m,
            QuantityRemaining = 50m,
            UomId = world.KgUomId,
            Status = LotStatus.Available,
            ExpiryDate = today.AddDays(30),
            ReceivedDate = DateTime.UtcNow.AddDays(-5)
        };

        context.InventoryLots.AddRange(lotA, lotB);
        await context.SaveChangesAsync();

        var allocationService = ServiceFactory.Allocation(context);

        // Request 40 kg (needs 25 kg from Lot A + 15 kg from Lot B)
        var result = await allocationService.AllocateFefoAsync(world.UbeItemId, world.MainWarehouseId, 40m);

        result.IsFulfilled.Should().BeTrue();
        result.AllocatedQuantity.Should().Be(40m);
        result.Allocations.Should().HaveCount(2);

        result.Allocations[0].LotCode.Should().Be("LOT-UBE-A");
        result.Allocations[0].QuantityToDraw.Should().Be(25m);

        result.Allocations[1].LotCode.Should().Be("LOT-UBE-B");
        result.Allocations[1].QuantityToDraw.Should().Be(15m);
    }

    [Fact]
    public async Task AllocateFefo_ignores_expired_and_quarantine_lots()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var today = DateOnly.FromDateTime(DateTime.Today);

        // Quarantine lot: 100 kg
        var quaranLot = new InventoryLot
        {
            LotCode = "LOT-QUARAN-100",
            ItemId = world.UbeItemId,
            LocationId = world.MainWarehouseId,
            SourceType = LotSourceType.Purchased,
            SupplierId = world.SupplierAId,
            QuantityReceived = 100m,
            QuantityRemaining = 100m,
            UomId = world.KgUomId,
            Status = LotStatus.Quarantine,
            ExpiryDate = today.AddDays(30)
        };

        // Expired lot: 50 kg (expired 2 days ago)
        var expiredLot = new InventoryLot
        {
            LotCode = "LOT-EXPIRED-50",
            ItemId = world.UbeItemId,
            LocationId = world.MainWarehouseId,
            SourceType = LotSourceType.Purchased,
            SupplierId = world.SupplierAId,
            QuantityReceived = 50m,
            QuantityRemaining = 50m,
            UomId = world.KgUomId,
            Status = LotStatus.Available,
            ExpiryDate = today.AddDays(-2)
        };

        context.InventoryLots.AddRange(quaranLot, expiredLot);
        await context.SaveChangesAsync();

        var allocationService = ServiceFactory.Allocation(context);

        var result = await allocationService.AllocateFefoAsync(world.UbeItemId, world.MainWarehouseId, 10m);

        result.IsFulfilled.Should().BeFalse();
        result.AllocatedQuantity.Should().Be(0m);
        result.ShortfallQuantity.Should().Be(10m);
        result.Allocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ExpirySweep_quarantines_expired_lots_and_debits_on_hand_stock()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var today = DateOnly.FromDateTime(DateTime.Today);

        // Seed an available lot with on-hand inventory cache
        var expiredLot = new InventoryLot
        {
            LotCode = "LOT-SWEEP-EXPIRED",
            ItemId = world.SugarItemId,
            LocationId = world.MainWarehouseId,
            SourceType = LotSourceType.Purchased,
            SupplierId = world.SupplierAId,
            QuantityReceived = 40m,
            QuantityRemaining = 40m,
            UomId = world.KgUomId,
            UnitCost = 45m,
            Status = LotStatus.Available,
            ExpiryDate = today.AddDays(-1) // expired yesterday
        };
        context.InventoryLots.Add(expiredLot);

        var inv = new Inventory
        {
            ItemId = world.SugarItemId,
            LocationId = world.MainWarehouseId,
            CurrentStock = 40m
        };
        context.Inventories.Add(inv);
        await context.SaveChangesAsync();

        var user = new FakeCurrentUserService(new CurrentUser("SYSTEM", "Automated Scheduler", "system@r3b2p.com", ["System"], true));
        var disposalService = ServiceFactory.Disposals(context, user);

        // Run sweep
        var sweepResult = await disposalService.PerformExpirySweepAsync();

        sweepResult.Success.Should().BeTrue();
        sweepResult.Data!.ExpiredLotsCount.Should().Be(1);
        sweepResult.Data.TotalQuantityQuarantined.Should().Be(40m);
        sweepResult.Data.TotalEstimatedLoss.Should().Be(1800m, "40 * 45 = 1800");

        // Verify lot is marked Expired
        await using var verify = CreateContext();
        var reloadedLot = await verify.InventoryLots.FindAsync(expiredLot.LotId);
        reloadedLot!.Status.Should().Be(LotStatus.Expired);

        // Verify on-hand stock cache was debited to 0
        var reloadedInv = await verify.Inventories.FirstAsync(i => i.ItemId == world.SugarItemId && i.LocationId == world.MainWarehouseId);
        reloadedInv.CurrentStock.Should().Be(0m);
    }

    [Fact]
    public async Task CreateDisposalRecord_writes_off_lots_and_records_audit_trail()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var user = new FakeCurrentUserService(new CurrentUser("MGR-001", "Plant Manager Victor", "victor@r3b2p.com", ["Manager"], true));
        var disposalService = ServiceFactory.Disposals(context, user);

        var condemnedLot = new InventoryLot
        {
            LotCode = "LOT-CONDEMNED-01",
            ItemId = world.SugarItemId,
            LocationId = world.MainWarehouseId,
            SourceType = LotSourceType.Purchased,
            SupplierId = world.SupplierAId,
            QuantityReceived = 20m,
            QuantityRemaining = 20m,
            UomId = world.KgUomId,
            UnitCost = 50m,
            Status = LotStatus.Available
        };
        context.InventoryLots.Add(condemnedLot);

        context.Inventories.Add(new Inventory
        {
            ItemId = world.SugarItemId,
            LocationId = world.MainWarehouseId,
            CurrentStock = 20m
        });
        await context.SaveChangesAsync();

        // 1. Create Disposal Record
        var dispResult = await disposalService.CreateDisposalRecordAsync(new CreateDisposalRequest
        {
            Reason = "Water damage from pipe leak in Storage Area B",
            WitnessName = "Warehouse Supervisor Ana",
            Notes = "Inspected and condemned by QA and Sanitation",
            Items =
            [
                new CreateDisposalItemRequest
                {
                    LotId = condemnedLot.LotId,
                    QuantityDisposed = 20m,
                    Notes = "Bag saturated with water"
                }
            ]
        });

        dispResult.Success.Should().BeTrue();
        dispResult.Data!.DisposalNumber.Should().StartWith("WD-");
        dispResult.Data.TotalCost.Should().Be(1000m, "20 * 50 = 1000");

        // 2. Verify lot is marked Disposed and on-hand stock reduced
        await using var verify = CreateContext();
        var reloadedLot = await verify.InventoryLots.FindAsync(condemnedLot.LotId);
        reloadedLot!.Status.Should().Be(LotStatus.Disposed);
        reloadedLot.QuantityRemaining.Should().Be(0m);

        var reloadedInv = await verify.Inventories.FirstAsync(i => i.ItemId == world.SugarItemId && i.LocationId == world.MainWarehouseId);
        reloadedInv.CurrentStock.Should().Be(0m);
    }
}
