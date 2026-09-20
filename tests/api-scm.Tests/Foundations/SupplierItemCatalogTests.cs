using System;
using System.Linq;
using System.Threading.Tasks;
using api_scm.Contracts.Requests;
using api_scm.Tests.Infrastructure;
using Applications.Services;
using Domains.Entities;
using Domains.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace api_scm.Tests.Foundations;

[Collection(ScmDatabaseCollection.Name)]
public sealed class SupplierItemCatalogTests(ScmDatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task Adding_a_supplier_item_creates_catalog_entry_with_pricing_and_pack_size()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var uomService = new UomConversionService(context);
        var service = new SupplierItemService(context, uomService, NullLogger<SupplierItemService>.Instance);

        var request = new CreateOrUpdateSupplierItemRequest
        {
            SupplierId = world.SupplierAId,
            ItemId = world.UbeItemId,
            SupplierSku = "AGRI-UBE-50KG",
            SupplierItemName = "Fresh Mountain Ube",
            UnitPrice = 85.50m,
            Currency = "PHP",
            PurchaseUomId = world.KgUomId,
            PackSize = 50m,
            LeadTimeDays = 2,
            MinOrderQuantity = 2m,
            IsPreferred = true,
            IsActive = true
        };

        var result = await service.UpsertSupplierItemAsync(request);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.SupplierId.Should().Be(world.SupplierAId);
        result.Data.ItemId.Should().Be(world.UbeItemId);
        result.Data.UnitPrice.Should().Be(85.50m);
        result.Data.PackSize.Should().Be(50m);
        result.Data.IsPreferred.Should().BeTrue();

        await using var verify = CreateContext();
        var saved = await verify.SupplierItems.SingleAsync(si => si.SupplierId == world.SupplierAId && si.ItemId == world.UbeItemId);
        saved.UnitPrice.Should().Be(85.50m);
        saved.PackSize.Should().Be(50m);
    }

    [Fact]
    public async Task Upserting_existing_supplier_item_updates_record()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var uomService = new UomConversionService(context);
        var service = new SupplierItemService(context, uomService, NullLogger<SupplierItemService>.Instance);

        // Initial create
        await service.UpsertSupplierItemAsync(new CreateOrUpdateSupplierItemRequest
        {
            SupplierId = world.SupplierAId,
            ItemId = world.UbeItemId,
            UnitPrice = 80m,
            PurchaseUomId = world.KgUomId,
            PackSize = 1m
        });

        // Update with new price and pack size
        var updateResult = await service.UpsertSupplierItemAsync(new CreateOrUpdateSupplierItemRequest
        {
            SupplierId = world.SupplierAId,
            ItemId = world.UbeItemId,
            UnitPrice = 92.50m,
            PurchaseUomId = world.KgUomId,
            PackSize = 25m,
            LeadTimeDays = 4
        });

        updateResult.Success.Should().BeTrue();
        updateResult.Data!.UnitPrice.Should().Be(92.50m);
        updateResult.Data.PackSize.Should().Be(25m);
        updateResult.Data.LeadTimeDays.Should().Be(4);

        await using var verify = CreateContext();
        (await verify.SupplierItems.CountAsync(si => si.SupplierId == world.SupplierAId && si.ItemId == world.UbeItemId)).Should().Be(1);
    }

    [Fact]
    public async Task Setting_is_preferred_unsets_other_preferred_suppliers_for_same_item()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var uomService = new UomConversionService(context);
        var service = new SupplierItemService(context, uomService, NullLogger<SupplierItemService>.Instance);

        // Supplier A is preferred
        await service.UpsertSupplierItemAsync(new CreateOrUpdateSupplierItemRequest
        {
            SupplierId = world.SupplierAId,
            ItemId = world.UbeItemId,
            UnitPrice = 85m,
            PurchaseUomId = world.KgUomId,
            IsPreferred = true
        });

        // Supplier B is now marked preferred for the same item
        await service.UpsertSupplierItemAsync(new CreateOrUpdateSupplierItemRequest
        {
            SupplierId = world.SupplierBId,
            ItemId = world.UbeItemId,
            UnitPrice = 82m,
            PurchaseUomId = world.KgUomId,
            IsPreferred = true
        });

        await using var verify = CreateContext();
        var supplierAEntry = await verify.SupplierItems.SingleAsync(si => si.SupplierId == world.SupplierAId && si.ItemId == world.UbeItemId);
        var supplierBEntry = await verify.SupplierItems.SingleAsync(si => si.SupplierId == world.SupplierBId && si.ItemId == world.UbeItemId);

        supplierAEntry.IsPreferred.Should().BeFalse("Supplier A was unseated as preferred");
        supplierBEntry.IsPreferred.Should().BeTrue("Supplier B is the new preferred vendor");
    }

    [Fact]
    public async Task GetSuppliersByItem_returns_only_catalogued_active_suppliers_with_preferred_first()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var uomService = new UomConversionService(context);
        var service = new SupplierItemService(context, uomService, NullLogger<SupplierItemService>.Instance);

        // Catalog: Supplier A (not preferred, 100 PHP) and Supplier B (preferred, 85 PHP)
        await service.UpsertSupplierItemAsync(new CreateOrUpdateSupplierItemRequest
        {
            SupplierId = world.SupplierAId,
            ItemId = world.UbeItemId,
            UnitPrice = 100m,
            PurchaseUomId = world.KgUomId,
            IsPreferred = false
        });

        await service.UpsertSupplierItemAsync(new CreateOrUpdateSupplierItemRequest
        {
            SupplierId = world.SupplierBId,
            ItemId = world.UbeItemId,
            UnitPrice = 85m,
            PurchaseUomId = world.KgUomId,
            IsPreferred = true
        });

        var result = await service.GetSuppliersByItemAsync(world.UbeItemId);

        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(2);
        result.Data![0].SupplierId.Should().Be(world.SupplierBId, "preferred supplier sorts first");
        result.Data[0].IsPreferred.Should().BeTrue();
        result.Data[1].SupplierId.Should().Be(world.SupplierAId);
        result.Data[1].IsPreferred.Should().BeFalse();
    }
}
