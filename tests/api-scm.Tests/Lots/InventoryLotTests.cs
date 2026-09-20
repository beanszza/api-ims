using api_scm.Tests.Infrastructure;
using Applications.Services;
using Domains.Entities;
using Domains.Enums;
using Infrastructures.Persistence;
using Microsoft.EntityFrameworkCore;

namespace api_scm.Tests.Lots;

[Collection(ScmDatabaseCollection.Name)]
public sealed class InventoryLotTests(ScmDatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    /// <summary>UTC because Npgsql maps ReceivedDate to timestamptz and rejects unspecified kinds.</summary>
    private static DateTime Utc(int year, int month, int day)
        => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    private static InventoryLot NewLot(
        SeededWorld world,
        string code,
        decimal quantity,
        int? supplierId,
        DateTime receivedOn,
        DateOnly? expiry = null,
        LotStatus status = LotStatus.Available) => new()
        {
            LotCode = code,
            ItemId = world.UbeItemId,
            LocationId = world.MainWarehouseId,
            SourceType = supplierId is null ? LotSourceType.OpeningBalance : LotSourceType.Purchased,
            SupplierId = supplierId,
            ReceivedDate = receivedOn,
            ExpiryDate = expiry,
            QuantityReceived = quantity,
            QuantityRemaining = quantity,
            UomId = world.KgUomId,
            UnitCost = 120m,
            Status = status
        };

    [Fact(DisplayName = "The 9 kg + 100 kg case: stock from two suppliers coexists as distinct lots")]
    public async Task Stock_From_Two_Suppliers_Coexists_As_Distinct_Lots()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        // The exact scenario the rebuild exists for. Under the old single-integer balance this was one
        // number, 109, with no way to say where any of it came from.
        context.InventoryLots.AddRange(
            NewLot(world, "L-260610-UBE-01", 9m, world.SupplierAId,
                receivedOn: Utc(2026, 6, 10), expiry: new DateOnly(2026, 6, 25)),
            NewLot(world, "L-260618-UBE-02", 100m, world.SupplierBId,
                receivedOn: Utc(2026, 6, 18), expiry: new DateOnly(2026, 7, 3)));

        await context.SaveChangesAsync();

        await using var verify = CreateContext();
        var lots = await verify.InventoryLots
            .Include(l => l.Supplier)
            .Where(l => l.ItemId == world.UbeItemId)
            .OrderBy(l => l.ReceivedDate)
            .ToListAsync();

        lots.Should().HaveCount(2);

        lots[0].QuantityRemaining.Should().Be(9m);
        lots[0].Supplier!.CompanyName.Should().Be("Supplier A Farms");
        lots[0].ExpiryDate.Should().Be(new DateOnly(2026, 6, 25));

        lots[1].QuantityRemaining.Should().Be(100m);
        lots[1].Supplier!.CompanyName.Should().Be("Supplier B Trading");

        // Total on hand is still knowable, but now it is derived rather than being the only thing known.
        lots.Sum(l => l.AvailableQuantity).Should().Be(109m);

        // And the share of each is answerable, which is what the inventory drill-down needs.
        var total = lots.Sum(l => l.AvailableQuantity);
        Math.Round(lots[0].AvailableQuantity / total * 100m, 1).Should().Be(8.3m);
        Math.Round(lots[1].AvailableQuantity / total * 100m, 1).Should().Be(91.7m);
    }

    [Fact]
    public async Task Only_available_lots_count_toward_usable_stock()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        context.InventoryLots.AddRange(
            NewLot(world, "L-A", 10m, world.SupplierAId, DateTime.UtcNow, status: LotStatus.Available),
            NewLot(world, "L-B", 20m, world.SupplierAId, DateTime.UtcNow, status: LotStatus.Quarantine),
            NewLot(world, "L-C", 30m, world.SupplierAId, DateTime.UtcNow, status: LotStatus.Expired),
            NewLot(world, "L-D", 40m, world.SupplierAId, DateTime.UtcNow, status: LotStatus.OnHold),
            NewLot(world, "L-E", 50m, world.SupplierAId, DateTime.UtcNow, status: LotStatus.Rejected));
        await context.SaveChangesAsync();

        await using var verify = CreateContext();
        var lots = await verify.InventoryLots.Where(l => l.ItemId == world.UbeItemId).ToListAsync();

        lots.Sum(l => l.AvailableQuantity).Should().Be(10m,
            "quarantined, expired, held and rejected stock is visible but not usable");

        // All 150 kg is still physically present and accounted for, which is the point of keeping it.
        lots.Sum(l => l.QuantityRemaining).Should().Be(150m);
    }

    [Fact]
    public async Task A_lot_cannot_be_driven_negative()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var lot = NewLot(world, "L-NEG", 10m, world.SupplierAId, DateTime.UtcNow);
        context.InventoryLots.Add(lot);
        await context.SaveChangesAsync();

        lot.QuantityRemaining = -1m;
        var act = async () => await context.SaveChangesAsync();

        var thrown = await act.Should().ThrowAsync<DbUpdateException>();
        thrown.Which.InnerException.Should().BeOfType<Npgsql.PostgresException>()
            .Which.SqlState.Should().Be("23514", "a check constraint violation, enforced by the database");
    }

    [Fact]
    public async Task A_lot_cannot_hand_out_more_than_it_received()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var lot = NewLot(world, "L-OVER", 10m, world.SupplierAId, DateTime.UtcNow);
        context.InventoryLots.Add(lot);
        await context.SaveChangesAsync();

        lot.QuantityRemaining = 11m;
        var act = async () => await context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>(
            "remaining above received would mean stock appeared out of nowhere");
    }

    [Fact]
    public async Task A_purchased_lot_must_name_its_supplier()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        // Purchased but unattributed: untraceable, so the database refuses it.
        context.InventoryLots.Add(new InventoryLot
        {
            LotCode = "L-NOSUP",
            ItemId = world.UbeItemId,
            LocationId = world.MainWarehouseId,
            SourceType = LotSourceType.Purchased,
            SupplierId = null,
            ReceivedDate = DateTime.UtcNow,
            QuantityReceived = 5m,
            QuantityRemaining = 5m,
            UomId = world.KgUomId,
            Status = LotStatus.Available
        });

        var act = async () => await context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task An_opening_balance_lot_needs_no_supplier()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        // Converted legacy stock genuinely has no known origin, and pretending otherwise would be a lie.
        context.InventoryLots.Add(new InventoryLot
        {
            LotCode = "L-OPENING-1-1",
            ItemId = world.UbeItemId,
            LocationId = world.MainWarehouseId,
            SourceType = LotSourceType.OpeningBalance,
            IsOpeningBalance = true,
            ReceivedDate = DateTime.UtcNow,
            QuantityReceived = 42m,
            QuantityRemaining = 42m,
            UomId = world.KgUomId,
            Status = LotStatus.Available
        });

        await context.SaveChangesAsync();

        await using var verify = CreateContext();
        (await verify.InventoryLots.SingleAsync()).SupplierId.Should().BeNull();
    }

    [Fact]
    public async Task Lot_codes_are_unique()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        context.InventoryLots.Add(NewLot(world, "L-DUP", 1m, world.SupplierAId, DateTime.UtcNow));
        await context.SaveChangesAsync();

        await using var second = CreateContext();
        second.InventoryLots.Add(NewLot(world, "L-DUP", 1m, world.SupplierAId, DateTime.UtcNow));

        var act = async () => await second.SaveChangesAsync();

        var thrown = await act.Should().ThrowAsync<DbUpdateException>();
        thrown.Which.InnerException.Should().BeOfType<Npgsql.PostgresException>()
            .Which.SqlState.Should().Be("23505");
    }

    [Fact]
    public async Task Two_draws_on_the_same_lot_cannot_both_win()
    {
        await using var seed = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(seed);
        seed.InventoryLots.Add(NewLot(world, "L-RACE", 100m, world.SupplierAId, DateTime.UtcNow));
        await seed.SaveChangesAsync();

        await using var first = CreateContext();
        await using var second = CreateContext();

        var a = await first.InventoryLots.SingleAsync();
        var b = await second.InventoryLots.SingleAsync();

        a.QuantityRemaining -= 60m;
        await first.SaveChangesAsync();

        b.QuantityRemaining -= 60m;
        var act = async () => await second.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "without the token both draws would succeed and 20 kg would be handed out twice");

        await using var verify = CreateContext();
        (await verify.InventoryLots.SingleAsync()).QuantityRemaining.Should().Be(40m);
    }

    [Fact]
    public void Days_until_expiry_is_calculated_from_whole_calendar_days()
    {
        var lot = new InventoryLot { ExpiryDate = new DateOnly(2026, 6, 25) };

        lot.DaysUntilExpiry(new DateOnly(2026, 6, 19)).Should().Be(6);
        lot.DaysUntilExpiry(new DateOnly(2026, 6, 25)).Should().Be(0, "expires today");
        lot.DaysUntilExpiry(new DateOnly(2026, 6, 26)).Should().Be(-1, "already expired");

        lot.IsExpiredAsOf(new DateOnly(2026, 6, 25)).Should().BeFalse("still good on the day itself");
        lot.IsExpiredAsOf(new DateOnly(2026, 6, 26)).Should().BeTrue();

        var neverExpires = new InventoryLot { ExpiryDate = null };
        neverExpires.DaysUntilExpiry(new DateOnly(2026, 6, 19))
            .Should().BeNull("items that do not perish have no countdown");
        neverExpires.IsExpiredAsOf(new DateOnly(2099, 1, 1)).Should().BeFalse();
    }
}

[Collection(ScmDatabaseCollection.Name)]
public sealed class LotCodeGeneratorTests(ScmDatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private LotCodeGenerator NewGenerator(ScmDbContext context)
        => new(context, new DocumentNumberService(context));

    [Fact]
    public async Task A_purchased_lot_code_carries_the_date_and_the_item()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        var code = await NewGenerator(context)
            .ForPurchasedLotAsync(world.UbeItemId, new DateTime(2026, 6, 19));

        code.Should().Be("L-260619-UBE-01");
    }

    [Fact]
    public async Task The_sequence_restarts_per_item_per_day()
    {
        await using var context = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);
        var generator = NewGenerator(context);
        var day = new DateTime(2026, 6, 19);

        (await generator.ForPurchasedLotAsync(world.UbeItemId, day)).Should().Be("L-260619-UBE-01");
        (await generator.ForPurchasedLotAsync(world.UbeItemId, day)).Should().Be("L-260619-UBE-02");

        // A different item on the same day starts again.
        (await generator.ForPurchasedLotAsync(world.SugarItemId, day)).Should().Be("L-260619-SUGA-01");

        // The same item on a different day starts again.
        (await generator.ForPurchasedLotAsync(world.UbeItemId, day.AddDays(1)))
            .Should().Be("L-260620-UBE-01");
    }

    [Fact]
    public async Task Concurrent_generation_never_repeats_a_code()
    {
        await using var seed = CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(seed);
        var day = new DateTime(2026, 6, 19);

        var tasks = Enumerable.Range(0, 20).Select(async _ =>
        {
            await using var context = CreateContext();
            return await NewGenerator(context).ForPurchasedLotAsync(world.UbeItemId, day);
        }).ToList();

        var codes = await Task.WhenAll(tasks);

        codes.Should().OnlyHaveUniqueItems(
            "two deliveries booked in at the same moment must not share a lot code");
    }

    [Fact]
    public async Task A_finished_goods_code_leads_with_the_product_because_it_is_printed_on_the_jar()
    {
        await using var context = CreateContext();
        await TestDataSeeder.SeedBaselineAsync(context);

        var code = await NewGenerator(context)
            .ForProducedLotAsync("UH500", new DateTime(2026, 6, 19));

        code.Should().Be("UH500-260619-01");
    }

    [Fact]
    public async Task Punctuation_and_spacing_never_reach_a_lot_code()
    {
        await using var context = CreateContext();
        await TestDataSeeder.SeedBaselineAsync(context);

        // A code has to be readable aloud and typeable into a search box.
        var code = await NewGenerator(context)
            .ForProducedLotAsync("Ube Halaya - 500g!", new DateTime(2026, 6, 19));

        code.Should().Be("UBEHALAYA500G-260619-01");
    }
}
