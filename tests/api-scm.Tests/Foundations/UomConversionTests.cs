using api_scm.Tests.Infrastructure;
using Applications.Services;
using Domains.Entities;
using Domains.Enums;
using Domains.Exceptions;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;

namespace api_scm.Tests.Foundations;

[Collection(ScmDatabaseCollection.Name)]
public sealed class UomConversionTests(ScmDatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    /// <summary>Seeds the full standard unit set and returns a lookup by code.</summary>
    private static async Task<Dictionary<string, int>> SeedUnitsAsync(ScmDbContext context)
    {
        UnitOfMeasureSeeder.Seed(context);
        return await context.UnitOfMeasures
            .AsNoTracking()
            .ToDictionaryAsync(u => u.Code, u => u.UomId);
    }

    [Fact]
    public async Task Grams_convert_to_kilograms()
    {
        await using var context = CreateContext();
        var units = await SeedUnitsAsync(context);
        var service = new UomConversionService(context);

        (await service.ConvertAsync(500m, units["g"], units["kg"])).Should().Be(0.5m);
        (await service.ConvertAsync(10_000m, units["g"], units["kg"])).Should().Be(10m,
            "this is defect 5 exactly: 10 000 g is 10 kg, not 10 000 kg");
    }

    [Fact]
    public async Task Kilograms_convert_back_to_grams()
    {
        await using var context = CreateContext();
        var units = await SeedUnitsAsync(context);
        var service = new UomConversionService(context);

        (await service.ConvertAsync(2.5m, units["kg"], units["g"])).Should().Be(2500m);
    }

    [Fact]
    public async Task A_fifty_kilo_sack_converts_to_fifty_kilograms()
    {
        await using var context = CreateContext();
        var units = await SeedUnitsAsync(context);
        var service = new UomConversionService(context);

        (await service.ConvertAsync(1m, units["sack50"], units["kg"])).Should().Be(50m);
        (await service.ConvertAsync(3m, units["sack50"], units["kg"])).Should().Be(150m);

        // And back, so a purchase quantity can be expressed either way.
        (await service.ConvertAsync(100m, units["kg"], units["sack50"])).Should().Be(2m);
    }

    [Fact]
    public async Task Millilitres_convert_to_litres()
    {
        await using var context = CreateContext();
        var units = await SeedUnitsAsync(context);
        var service = new UomConversionService(context);

        (await service.ConvertAsync(250m, units["mL"], units["L"])).Should().Be(0.25m);
    }

    [Fact]
    public async Task Converting_weight_to_volume_is_refused()
    {
        await using var context = CreateContext();
        var units = await SeedUnitsAsync(context);
        var service = new UomConversionService(context);

        var act = async () => await service.ConvertAsync(1m, units["kg"], units["L"]);

        (await act.Should().ThrowAsync<UomConversionException>())
            .WithMessage("*kg (Weight)*L (Volume)*")
            .WithMessage("*measure different things*");
    }

    [Fact]
    public async Task Converting_a_count_to_a_weight_is_refused()
    {
        await using var context = CreateContext();
        var units = await SeedUnitsAsync(context);
        var service = new UomConversionService(context);

        var act = async () => await service.ConvertAsync(1m, units["pcs"], units["kg"]);
        await act.Should().ThrowAsync<UomConversionException>();
    }

    [Fact]
    public async Task An_unknown_unit_is_refused_rather_than_assumed()
    {
        await using var context = CreateContext();
        var units = await SeedUnitsAsync(context);
        var service = new UomConversionService(context);

        var act = async () => await service.ConvertAsync(1m, 999_999, units["kg"]);

        (await act.Should().ThrowAsync<UomConversionException>())
            .WithMessage("*999999 was not found*");
    }

    [Fact]
    public async Task Same_unit_conversion_is_the_identity_and_costs_no_query()
    {
        await using var context = CreateContext();
        var units = await SeedUnitsAsync(context);
        var service = new UomConversionService(context);

        (await service.ConvertAsync(7.125m, units["kg"], units["kg"])).Should().Be(7.125m);
    }

    [Fact]
    public async Task Container_units_stay_at_one_because_pack_size_is_supplier_specific()
    {
        await using var context = CreateContext();
        var units = await SeedUnitsAsync(context);
        var service = new UomConversionService(context);

        // How many pieces are in a box depends on the supplier, so the unit itself cannot claim a
        // factor. That information lives on SupplierItem.PackSize once Task 12 lands.
        (await service.ConvertAsync(1m, units["box"], units["pcs"])).Should().Be(1m);
    }

    [Fact]
    public async Task ConvertForStock_rounds_to_the_three_decimals_the_column_holds()
    {
        await using var context = CreateContext();
        var units = await SeedUnitsAsync(context);
        var service = new UomConversionService(context);

        // 1 g = 0.001 kg, so 1.5 g is 0.0015 kg, which rounds to 0.002 at the stored scale.
        (await service.ConvertForStockAsync(1.5m, units["g"], units["kg"])).Should().Be(0.002m);

        // Unrounded, the true value is available for intermediate arithmetic.
        (await service.ConvertAsync(1.5m, units["g"], units["kg"])).Should().Be(0.0015m);
    }

    [Fact]
    public async Task CanConvert_answers_without_throwing_so_it_is_usable_for_validation()
    {
        await using var context = CreateContext();
        var units = await SeedUnitsAsync(context);
        var service = new UomConversionService(context);

        (await service.CanConvertAsync(units["g"], units["kg"])).Should().BeTrue();
        (await service.CanConvertAsync(units["kg"], units["L"])).Should().BeFalse();
        (await service.CanConvertAsync(999_999, units["kg"])).Should().BeFalse();
    }

    [Fact]
    public async Task Converting_into_an_items_stocking_unit_uses_StockUomId()
    {
        await using var context = CreateContext();
        var units = await SeedUnitsAsync(context);

        var category = new Category { CategoryName = "Raw Material", Description = "x" };
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var ube = new Item
        {
            ItemName = "Ube",
            UomId = units["kg"],
            StockUomId = units["kg"],
            CategoryId = category.CategoryId,
            IsActive = true
        };
        context.Items.Add(ube);
        await context.SaveChangesAsync();

        var service = new UomConversionService(context);

        (await service.ConvertToItemStockUomAsync(10_000m, units["g"], ube.ItemId))
            .Should().Be(10m, "an ingredient in grams must land on a kilogram balance as kilograms");
    }

    // ---------- Seeder invariants ----------

    [Fact]
    public async Task The_seeder_is_idempotent()
    {
        await using var context = CreateContext();
        UnitOfMeasureSeeder.Seed(context);
        var firstCount = await context.UnitOfMeasures.CountAsync();

        UnitOfMeasureSeeder.Seed(context);
        UnitOfMeasureSeeder.Seed(context);

        (await context.UnitOfMeasures.CountAsync()).Should().Be(firstCount);
    }

    [Fact]
    public async Task Every_dimension_has_exactly_one_base_unit_and_non_bases_point_at_it()
    {
        await using var context = CreateContext();
        UnitOfMeasureSeeder.Seed(context);

        var units = await context.UnitOfMeasures.AsNoTracking().ToListAsync();

        foreach (var group in units.GroupBy(u => u.UomType))
        {
            group.Count(u => u.IsBaseUnit).Should().Be(1,
                $"{group.Key} must have exactly one base unit");

            var baseUnit = group.Single(u => u.IsBaseUnit);
            baseUnit.ConversionFactor.Should().Be(1m, "a base unit is its own reference point");
            baseUnit.BaseUomId.Should().BeNull("a base unit references nothing");

            foreach (var derived in group.Where(u => !u.IsBaseUnit))
            {
                derived.BaseUomId.Should().Be(baseUnit.UomId,
                    $"{derived.Code} must point at the {group.Key} base");
            }
        }
    }

    [Fact]
    public async Task No_unit_has_a_factor_that_would_divide_by_zero()
    {
        await using var context = CreateContext();
        UnitOfMeasureSeeder.Seed(context);

        (await context.UnitOfMeasures.AnyAsync(u => u.ConversionFactor <= 0)).Should().BeFalse();
    }

    [Fact]
    public async Task The_seeder_adopts_pre_existing_units_instead_of_duplicating_them()
    {
        await using var context = CreateContext();

        // A unit created before codes and dimensions existed.
        context.UnitOfMeasures.Add(new UnitOfMeasure { Name = "Kilogram", Abbreviation = "kg" });
        await context.SaveChangesAsync();

        UnitOfMeasureSeeder.Seed(context);

        var kilograms = await context.UnitOfMeasures
            .AsNoTracking()
            .Where(u => u.Abbreviation == "kg")
            .ToListAsync();

        kilograms.Should().ContainSingle("the existing row is adopted, not duplicated");
        kilograms[0].Code.Should().Be("kg");
        kilograms[0].UomType.Should().Be(UomType.Weight);
        kilograms[0].IsBaseUnit.Should().BeTrue();
    }
}
