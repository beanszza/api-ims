using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace api_scm.Migrations
{
    /// <summary>
    /// Adds gapless document numbering, and makes stock balances structurally sound.
    /// </summary>
    /// <remarks>
    /// Hand-edited in three places, each of which would otherwise fail or misbehave on a populated
    /// database:
    /// <list type="number">
    ///   <item>The scaffolder emitted <c>AddColumn&lt;uint&gt;("xmin")</c>. <c>xmin</c> is a PostgreSQL
    ///   system column that exists on every table already, so creating it errors. It is mapped as a
    ///   concurrency token in the model but must never be created or dropped.</item>
    ///   <item>The unique index on <c>Inventories (ItemId, LocationId)</c> cannot be created while
    ///   duplicate rows exist, and they do: nothing prevented them, which is why application startup
    ///   carried a routine that merged duplicates on every boot. Duplicates are merged here, once,
    ///   summing their quantities so no stock is lost.</item>
    ///   <item>The unique index on <c>PoNumber</c> cannot be created while every row holds ''. Existing
    ///   orders are given the number the UI was already displaying, and the sequence table is primed so
    ///   the next generated number cannot collide with a backfilled one.</item>
    /// </list>
    /// </remarks>
    public partial class AddDocumentNumberingAndStockIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Inventories_ItemId",
                table: "Inventories");

            migrationBuilder.AddColumn<string>(
                name: "PoNumber",
                table: "PurchaseOrders",
                type: "text",
                nullable: false,
                defaultValue: "");

            // NOTE: no AddColumn for "xmin". See the class remarks.

            migrationBuilder.CreateTable(
                name: "DocumentSequences",
                columns: table => new
                {
                    DocumentSequenceId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocType = table.Column<string>(type: "text", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    LastNumber = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentSequences", x => x.DocumentSequenceId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSequences_DocType_Year",
                table: "DocumentSequences",
                columns: new[] { "DocType", "Year" },
                unique: true);

            // ---------- Merge duplicate balances ----------

            // Give the surviving row the combined quantity, so merging cannot lose stock.
            migrationBuilder.Sql("""
                WITH totals AS (
                    SELECT "ItemId", "LocationId",
                           MIN("InventoryId") AS keep_id,
                           SUM("CurrentStock") AS total
                    FROM "Inventories"
                    GROUP BY "ItemId", "LocationId"
                    HAVING COUNT(*) > 1
                )
                UPDATE "Inventories" i
                SET "CurrentStock" = t.total
                FROM totals t
                WHERE i."InventoryId" = t.keep_id;
                """);

            migrationBuilder.Sql("""
                DELETE FROM "Inventories"
                WHERE "InventoryId" IN (
                    SELECT "InventoryId" FROM (
                        SELECT "InventoryId",
                               ROW_NUMBER() OVER (
                                   PARTITION BY "ItemId", "LocationId" ORDER BY "InventoryId") AS rn
                        FROM "Inventories"
                    ) ranked
                    WHERE ranked.rn > 1
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Inventories_ItemId_LocationId",
                table: "Inventories",
                columns: new[] { "ItemId", "LocationId" },
                unique: true);

            // ---------- Backfill purchase order numbers ----------

            // Matches what the procurement screen already rendered from the primary key, so no
            // reference that anyone has written down changes shape.
            migrationBuilder.Sql("""
                UPDATE "PurchaseOrders"
                SET "PoNumber" = 'PO-'
                    || EXTRACT(YEAR FROM "OrderDate")::int::text
                    || '-' || LPAD("PoId"::text, 4, '0')
                WHERE COALESCE("PoNumber", '') = '';
                """);

            // Prime the sequence past the highest backfilled number for each year, otherwise the first
            // newly created order would be handed a number that already exists.
            migrationBuilder.Sql("""
                INSERT INTO "DocumentSequences" ("DocType", "Year", "LastNumber")
                SELECT 'PurchaseOrder', EXTRACT(YEAR FROM "OrderDate")::int, MAX("PoId")
                FROM "PurchaseOrders"
                GROUP BY EXTRACT(YEAR FROM "OrderDate")::int
                ON CONFLICT ("DocType", "Year") DO UPDATE
                SET "LastNumber" = GREATEST(
                    "DocumentSequences"."LastNumber", EXCLUDED."LastNumber");
                """);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_PoNumber",
                table: "PurchaseOrders",
                column: "PoNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentSequences");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_PoNumber",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_Inventories_ItemId_LocationId",
                table: "Inventories");

            migrationBuilder.DropColumn(
                name: "PoNumber",
                table: "PurchaseOrders");

            // NOTE: no DropColumn for "xmin". See the class remarks.
            // Merged duplicate balances are not restored: the merge is deliberately irreversible,
            // because there is no record of how a combined quantity was originally split.

            migrationBuilder.CreateIndex(
                name: "IX_Inventories_ItemId",
                table: "Inventories",
                column: "ItemId");
        }
    }
}
