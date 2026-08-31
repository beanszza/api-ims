using api_scm.Tests.Infrastructure;
using Domains.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace api_scm.Tests.Lots;

/// <summary>
/// Exercises the opening-lot backfill against a database that already holds legacy balances, which is
/// the only way to prove the highest-risk migration in the rebuild is lossless.
/// </summary>
/// <remarks>
/// The dev database this rebuild has been running against happens to hold zero <c>Inventory</c> rows,
/// so applying the migration there proves only that it does not error - not that it is correct. This
/// test builds a populated database at the migration immediately before this one, writes legacy-shaped
/// balances with raw SQL, applies the migration under test, and checks the exact invariant the task
/// description calls for: the sum of <c>InventoryLots.QuantityRemaining</c> for opening lots must equal
/// the pre-existing <c>Inventory.CurrentStock</c>, per (ItemId, LocationId), with zero rows dropped or
/// duplicated.
/// </remarks>
public sealed class DeriveOnHandFromLotsMigrationTests
{
    private const string MigrationBeforeConversion = "FixInventoryLotCodeUniqueness";
    private const string MigrationUnderTest = "DeriveOnHandFromLots";

    [Fact]
    public async Task Every_positive_balance_gets_exactly_one_opening_lot_with_a_matching_total()
    {
        await using var database = await PostgresTestDatabase.CreateAsync("scm_openlot");

        await using (var context = database.CreateContext())
        {
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync(MigrationBeforeConversion);
        }

        await ExecuteAsync(database,
            """
            INSERT INTO "UnitOfMeasures" ("Name", "Abbreviation", "Code", "UomType", "ConversionFactor", "IsBaseUnit")
            VALUES ('Kilogram', 'kg', 'kg', 'Weight', 1, true);
            INSERT INTO "Categories" ("CategoryName", "Description") VALUES ('Raw Materials', 'x');
            INSERT INTO "Locations" ("LocationName", "LocationType", "Address", "IsActive", "Status", "IsSystemLocation")
            VALUES ('Main Warehouse', 'Warehouse', 'x', true, 'Active', true);
            INSERT INTO "Locations" ("LocationName", "LocationType", "Address", "IsActive", "Status", "IsSystemLocation")
            VALUES ('Branch Manila', 'Branch', 'x', true, 'Active', false);
            INSERT INTO "Items" ("ItemName", "UomId", "StockUomId", "CategoryId", "MinStockLevel", "MaxStockLevel", "IsActive")
            VALUES ('Ube', 1, 1, 1, 20, 200, true);
            INSERT INTO "Items" ("ItemName", "UomId", "StockUomId", "CategoryId", "MinStockLevel", "MaxStockLevel", "IsActive")
            VALUES ('Sugar', 1, 1, 1, 10, 100, true);

            -- Same item, two locations: the shape a real "on hand at the warehouse and at a branch" split takes.
            INSERT INTO "Inventories" ("ItemId", "LocationId", "CurrentStock") VALUES (1, 1, 42.500);
            INSERT INTO "Inventories" ("ItemId", "LocationId", "CurrentStock") VALUES (1, 2, 7.250);
            INSERT INTO "Inventories" ("ItemId", "LocationId", "CurrentStock") VALUES (2, 1, 15.000);

            -- A zero balance: must NOT get an opening lot (there is nothing to trace).
            INSERT INTO "Inventories" ("ItemId", "LocationId", "CurrentStock") VALUES (2, 2, 0);
            """);

        await using (var context = database.CreateContext())
        {
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync(MigrationUnderTest);
        }

        await using var verify = database.CreateContext();

        var balances = await verify.Inventories
            .Select(i => new { i.ItemId, i.LocationId, i.CurrentStock })
            .OrderBy(i => i.ItemId).ThenBy(i => i.LocationId)
            .ToListAsync();
        balances.Should().HaveCount(4, "the migration must not drop, merge or duplicate any Inventory row");

        var openingLots = await verify.InventoryLots
            .Where(l => l.IsOpeningBalance)
            .ToListAsync();

        // Exactly one opening lot per positive balance - not per Inventory row.
        openingLots.Should().HaveCount(3, "the zero balance must not receive an opening lot");

        foreach (var balance in balances.Where(b => b.CurrentStock > 0))
        {
            var lot = openingLots.Should()
                .ContainSingle(l => l.ItemId == balance.ItemId && l.LocationId == balance.LocationId)
                .Subject;

            lot.QuantityRemaining.Should().Be(balance.CurrentStock,
                "the opening lot's remaining quantity must equal the exact balance it was carved from");
            lot.QuantityReceived.Should().Be(balance.CurrentStock);
            lot.LotCode.Should().Be($"L-OPENING-{balance.ItemId}-{balance.LocationId}",
                "matching the format ILotCodeGenerator.ForOpeningBalance produces");
            lot.SourceType.Should().Be(Domains.Enums.LotSourceType.OpeningBalance);
            lot.Status.Should().Be(Domains.Enums.LotStatus.Available,
                "an opening balance was already usable stock before the rebuild, so it starts usable");
            lot.SupplierId.Should().BeNull("legacy balances never recorded who supplied them");
            lot.ExpiryDate.Should().BeNull("legacy balances never recorded an expiry");
        }

        // Grand total across all lots must equal the grand total across all balances: nothing invented,
        // nothing lost, for every item/location taken together.
        var totalFromBalances = balances.Sum(b => b.CurrentStock);
        var totalFromLots = openingLots.Sum(l => l.QuantityRemaining);
        totalFromLots.Should().Be(totalFromBalances);

        // Every opening lot has exactly one ledger row explaining it, and the ledger agrees with the lot.
        var ledgerRows = await verify.StockLedgers
            .Where(l => l.ReferenceType == "OpeningBalance")
            .ToListAsync();
        ledgerRows.Should().HaveCount(3);

        foreach (var lot in openingLots)
        {
            var entry = ledgerRows.Should().ContainSingle(l => l.LotId == lot.LotId).Subject;
            entry.Quantity.Should().Be(lot.QuantityRemaining, "a receipt is posted positive");
            entry.MovementType.Should().Be(Domains.Enums.MovementType.OpeningBalance);
        }

        // Inventory.CurrentStock itself must be untouched - the migration explains the number, it does
        // not recompute it.
        balances.Should().BeEquivalentTo(
            [
                new { ItemId = 1, LocationId = 1, CurrentStock = 42.500m },
                new { ItemId = 1, LocationId = 2, CurrentStock = 7.250m },
                new { ItemId = 2, LocationId = 1, CurrentStock = 15.000m },
                new { ItemId = 2, LocationId = 2, CurrentStock = 0m }
            ]);
    }

    [Fact]
    public async Task The_cache_can_no_longer_be_written_negative_after_this_migration()
    {
        await using var database = await PostgresTestDatabase.CreateAsync("scm_openlot_ck");
        await database.MigrateAsync();

        await using var context = database.CreateContext();
        var world = await TestDataSeeder.SeedBaselineAsync(context);

        context.Inventories.Add(new Inventory
        {
            ItemId = world.UbeItemId,
            LocationId = world.MainWarehouseId,
            CurrentStock = -5m
        });

        var act = async () => await context.SaveChangesAsync();
        var thrown = await act.Should().ThrowAsync<DbUpdateException>();
        thrown.Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be("23514", "a CHECK constraint violation");
    }

    private static async Task ExecuteAsync(PostgresTestDatabase database, string sql)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
