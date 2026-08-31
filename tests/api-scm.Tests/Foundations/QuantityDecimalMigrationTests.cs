using api_scm.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace api_scm.Tests.Foundations;

/// <summary>
/// Verifies the int -> decimal(18,3) conversion against a database that already holds integer-era
/// rows, which is the only way to exercise the ALTER COLUMN path.
/// </summary>
/// <remarks>
/// The implementation plan expected the migration to need <c>USING column::numeric</c>. EF generated
/// a plain <c>ALTER COLUMN ... TYPE numeric(18,3)</c>, which is valid here because PostgreSQL has a
/// built-in cast from integer to numeric. This test is what turns that from an assumption into a
/// checked fact, and it will fail loudly if a future provider or column stops allowing the implicit
/// cast.
/// </remarks>
public sealed class QuantityDecimalMigrationTests
{
    private const string MigrationBeforeConversion = "AddStatusEnumConversions";
    private const string MigrationUnderTest = "QuantitiesToDecimal";

    [Fact]
    public async Task Existing_integer_quantities_survive_the_conversion_intact()
    {
        await using var database = await PostgresTestDatabase.CreateAsync("scm_migr");

        // 1. Bring the schema up to the state just before quantities became decimal.
        await using (var context = database.CreateContext())
        {
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync(MigrationBeforeConversion);
        }

        // 2. Confirm we really are on the integer schema, then write integer-era data with raw SQL,
        //    because the current entity model no longer matches these column types.
        (await GetColumnTypeAsync(database, "Inventories", "CurrentStock")).Should().Be("integer");

        await ExecuteAsync(database,
            """
            INSERT INTO "UnitOfMeasures" ("Name", "Abbreviation") VALUES ('Kilogram', 'kg');
            INSERT INTO "Categories" ("CategoryName", "Description") VALUES ('Raw Material', 'x');
            INSERT INTO "Items" ("ItemName", "UomId", "CategoryId", "MinStockLevel", "MaxStockLevel", "IsActive")
            VALUES ('Ube', 1, 1, 20, 200, true);
            INSERT INTO "Locations" ("LocationName", "LocationType", "Address", "IsActive", "Status")
            VALUES ('Main Warehouse', 'Warehouse', 'x', true, 'Active');
            INSERT INTO "Inventories" ("ItemId", "LocationId", "CurrentStock") VALUES (1, 1, 109);
            INSERT INTO "InventoryMovementLogs" ("ItemId", "LocationId", "ChangeQuantity", "ActionType", "ReferenceId", "UserId", "Timestamp")
            VALUES (1, 1, -7, 'OUT', 'legacy', 1, now());
            """);

        // 3. Apply the migration under test.
        await using (var context = database.CreateContext())
        {
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync(MigrationUnderTest);
        }

        // 4. The column is now numeric with the intended precision.
        (await GetColumnTypeAsync(database, "Inventories", "CurrentStock")).Should().Be("numeric");
        (await GetNumericScaleAsync(database, "Inventories", "CurrentStock")).Should().Be(3);

        // 5. Bring the schema fully up to date before reading through the entity model, which now
        //    expects columns added by later migrations. This also puts the UoM backfill under test:
        //    the row inserted above predates StockUomId entirely.
        await using (var context = database.CreateContext())
        {
            await context.Database.MigrateAsync();
        }

        await using var verify = database.CreateContext();

        (await verify.Items.SingleAsync()).StockUomId
            .Should().Be(1, "a pre-existing item inherits its display unit as its stocking unit");
        var inventory = await verify.Inventories.SingleAsync();
        inventory.CurrentStock.Should().Be(109m);

        var item = await verify.Items.SingleAsync();
        item.MinStockLevel.Should().Be(20m);
        item.MaxStockLevel.Should().Be(200m);

        var log = await verify.InventoryMovementLogs.SingleAsync();
        log.ChangeQuantity.Should().Be(-7m, "the signed ledger quantity must keep its sign");
    }

    [Fact]
    public async Task Fractional_quantities_round_trip_at_three_decimal_places()
    {
        await using var database = await PostgresTestDatabase.CreateAsync("scm_frac");
        await database.MigrateAsync();

        await using (var seed = database.CreateContext())
        {
            var world = await TestDataSeeder.SeedBaselineAsync(seed);

            // The whole point of the change: a recipe that needs 0.75 kg, and stock measured to grams.
            var ingredient = await seed.RecipeIngredients.SingleAsync(i => i.ItemId == world.SugarItemId);
            ingredient.StandardQuantity = 0.75m;

            seed.Inventories.Add(new Domains.Entities.Inventory
            {
                ItemId = world.UbeItemId,
                LocationId = world.MainWarehouseId,
                CurrentStock = 9.125m
            });

            await seed.SaveChangesAsync();
        }

        await using var verify = database.CreateContext();

        (await verify.RecipeIngredients.SingleAsync(i => i.StandardQuantity > 0 && i.StandardQuantity < 1))
            .StandardQuantity.Should().Be(0.75m, "0.75 was previously truncated to 0 by an int column");

        (await verify.Inventories.SingleAsync()).CurrentStock.Should().Be(9.125m);
    }

    [Fact]
    public async Task Precision_beyond_three_decimals_is_rounded_not_rejected()
    {
        await using var database = await PostgresTestDatabase.CreateAsync("scm_round");
        await database.MigrateAsync();

        await using (var seed = database.CreateContext())
        {
            var world = await TestDataSeeder.SeedBaselineAsync(seed);
            seed.Inventories.Add(new Domains.Entities.Inventory
            {
                ItemId = world.UbeItemId,
                LocationId = world.MainWarehouseId,
                CurrentStock = 1.23456m
            });
            await seed.SaveChangesAsync();
        }

        await using var verify = database.CreateContext();

        // numeric(18,3) rounds half away from zero, so 1.23456 stores as 1.235. Documented here so
        // the behaviour is a decision rather than a surprise.
        (await verify.Inventories.SingleAsync()).CurrentStock.Should().Be(1.235m);
    }

    [Fact]
    public async Task Every_quantity_column_ended_up_as_numeric_18_3()
    {
        await using var database = await PostgresTestDatabase.CreateAsync("scm_cols");
        await database.MigrateAsync();

        (string Table, string Column)[] quantityColumns =
        [
            ("Inventories", "CurrentStock"),
            ("Items", "MinStockLevel"),
            ("Items", "MaxStockLevel"),
            ("RecipeIngredients", "StandardQuantity"),
            ("PurchaseOrderItems", "PoItemQuantity"),
            ("PurchaseOrderItems", "ReceivedQuantity"),
            ("ProductionBatches", "BatchMultiplier"),
            ("ProductionBatches", "EstimatedQuantity"),
            ("ProductionBatches", "ActualQuantity"),
            ("BatchConsumptions", "RequiredQuantity"),
            ("StockTransfers", "TransferQuantity"),
            ("InventoryMovementLogs", "ChangeQuantity")
        ];

        foreach (var (table, column) in quantityColumns)
        {
            (await GetColumnTypeAsync(database, table, column))
                .Should().Be("numeric", $"{table}.{column} is a stock quantity");
            (await GetNumericScaleAsync(database, table, column))
                .Should().Be(3, $"{table}.{column} must not drift from the agreed scale");
        }
    }

    private static async Task ExecuteAsync(PostgresTestDatabase database, string sql)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> GetColumnTypeAsync(
        PostgresTestDatabase database, string table, string column)
        => (string)(await ScalarAsync(database,
            """
            SELECT data_type FROM information_schema.columns
            WHERE table_name = @table AND column_name = @column
            """, table, column))!;

    private static async Task<int> GetNumericScaleAsync(
        PostgresTestDatabase database, string table, string column)
        => Convert.ToInt32(await ScalarAsync(database,
            """
            SELECT numeric_scale FROM information_schema.columns
            WHERE table_name = @table AND column_name = @column
            """, table, column));

    private static async Task<object?> ScalarAsync(
        PostgresTestDatabase database, string sql, string table, string column)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("table", table);
        command.Parameters.AddWithValue("column", column);
        return await command.ExecuteScalarAsync();
    }
}
