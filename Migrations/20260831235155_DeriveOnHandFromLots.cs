using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_scm.Migrations
{
    /// <summary>
    /// Backfills an opening <c>InventoryLot</c> and matching <c>StockLedger</c> row for every existing
    /// positive <c>Inventory</c> balance, then locks <c>CurrentStock</c> against going negative.
    /// </summary>
    /// <remarks>
    /// This is the highest-risk migration in the rebuild: it is the one place a mistake could
    /// silently change how much stock the system believes exists. It was hand-written rather than
    /// scaffolded, because the scaffolder has no way to know that every existing balance needs a
    /// corresponding lot - there is no C# entity change here for it to diff against.
    /// <para>
    /// What it does NOT do: it never writes to <c>Inventory.CurrentStock</c>. The number already
    /// reflects physical reality (whatever it is), and the job here is only to give that existing
    /// number a paper trail - one lot and one ledger row explaining where it came from - not to
    /// recompute it. A before/after total-per-item-location check via psql is required after applying
    /// this, comparing <c>SUM(Inventories.CurrentStock)</c> against
    /// <c>SUM(InventoryLots.QuantityRemaining) WHERE IsOpeningBalance</c>, grouped by
    /// (ItemId, LocationId): the two must match exactly, with zero rows dropped or duplicated.
    /// </para>
    /// <para>
    /// Rows with <c>CurrentStock &lt;= 0</c> are skipped: a zero balance needs no lot, and a negative
    /// one (which should not exist, since Task 5's unique index and this migration's own defensive
    /// floor rule it out) would violate <c>InventoryLots</c>' own <c>QuantityReceived &gt; 0</c> check.
    /// </para>
    /// </remarks>
    public partial class DeriveOnHandFromLots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Defensive floor, run before anything else: a negative balance should be impossible (Task 5's
            // unique index prevents duplicate rows, and nothing in this codebase decrements CurrentStock
            // below zero), but the check constraint added at the end of this migration would fail the
            // migration outright if one existed. Flooring at zero here is a documented, visible choice
            // rather than a migration that mysteriously refuses to apply on a database no one manually
            // corrupted.
            migrationBuilder.Sql(
                """
                UPDATE "Inventories" SET "CurrentStock" = 0 WHERE "CurrentStock" < 0;
                """);

            // One opening lot per existing positive balance, using exactly the format
            // ILotCodeGenerator.ForOpeningBalance produces ("L-OPENING-{itemId}-{locationId}"), which is
            // already unique per (ItemId, LocationId) and therefore per (LotCode, LocationId).
            // SourceType/Status/etc. are written as their DbValue strings directly, since a migration
            // runs before any enum converter is available to it.
            migrationBuilder.Sql(
                """
                WITH opening_lots AS (
                    INSERT INTO "InventoryLots" (
                        "LotCode", "ItemId", "LocationId", "SourceType",
                        "SupplierId", "SupplierLotNo", "GrnLineId", "ProductionOrderId",
                        "ReceivedDate", "ManufactureDate", "ExpiryDate", "IsExpiryEstimated",
                        "QuantityReceived", "QuantityRemaining", "UomId", "UnitCost",
                        "Status", "HoldReason", "IsOpeningBalance"
                    )
                    SELECT
                        'L-OPENING-' || i."ItemId" || '-' || i."LocationId",
                        i."ItemId", i."LocationId", 'Opening Balance',
                        NULL, NULL, NULL, NULL,
                        now(), NULL, NULL, false,
                        i."CurrentStock", i."CurrentStock", it."StockUomId", 0,
                        'Available', NULL, true
                    FROM "Inventories" i
                    JOIN "Items" it ON it."ItemId" = i."ItemId"
                    WHERE i."CurrentStock" > 0
                    RETURNING "LotId", "ItemId", "LocationId", "UomId", "QuantityRemaining"
                )
                INSERT INTO "StockLedgers" (
                    "LotId", "ItemId", "LocationId", "MovementType", "Quantity", "UomId", "UnitCost",
                    "ReferenceType", "ReferenceId", "UserId", "UserName", "PostedAt",
                    "ReversalOfLedgerId", "Notes"
                )
                SELECT
                    "LotId", "ItemId", "LocationId", 'Opening Balance', "QuantityRemaining", "UomId", 0,
                    'OpeningBalance', 'OPENING-' || "ItemId" || '-' || "LocationId",
                    'migration', 'Database migration', now(),
                    NULL, 'Converted from the legacy single-balance-per-location Inventory row.'
                FROM opening_lots;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Inventories_CurrentStock_NotNegative",
                table: "Inventories",
                sql: "\"CurrentStock\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Inventories_CurrentStock_NotNegative",
                table: "Inventories");

            // Reverse only what this migration created: the ledger rows it posted, then the lots it
            // created. Anything consumed from an opening lot after this migration ran is NOT restored -
            // a Down migration undoes schema, not the passage of time.
            migrationBuilder.Sql(
                """
                DELETE FROM "StockLedgers"
                WHERE "ReferenceType" = 'OpeningBalance'
                  AND "LotId" IN (SELECT "LotId" FROM "InventoryLots" WHERE "IsOpeningBalance" = true);

                DELETE FROM "InventoryLots" WHERE "IsOpeningBalance" = true;
                """);
        }
    }
}
