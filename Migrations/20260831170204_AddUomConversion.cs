using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_scm.Migrations
{
    /// <summary>
    /// Makes units of measure convertible, and gives every item an explicit stocking unit.
    /// </summary>
    /// <remarks>
    /// Hand-edited. The scaffolded version added the columns and then immediately created a unique
    /// index and two foreign keys, which cannot succeed on a populated database:
    /// <list type="number">
    ///   <item><c>Code</c> arrives as '' on every row, so a unique index over it collides as soon as
    ///   there are two units.</item>
    ///   <item><c>StockUomId</c> arrives as 0, which satisfies no foreign key to UnitOfMeasures.</item>
    ///   <item><c>ConversionFactor</c> arrives as 0, which would make every conversion divide by zero.</item>
    ///   <item><c>UomType</c> arrives as '', which is not a valid UomType and would throw on read.</item>
    /// </list>
    /// So the data backfill is interleaved: add columns, populate them, then add the constraints.
    /// </remarks>
    public partial class AddUomConversion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Items_UnitOfMeasures_UomId",
                table: "Items");

            // ---------- 1. Columns ----------

            migrationBuilder.AddColumn<int>(
                name: "BaseUomId",
                table: "UnitOfMeasures",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "UnitOfMeasures",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ConversionFactor",
                table: "UnitOfMeasures",
                type: "numeric(18,9)",
                precision: 18,
                scale: 9,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<bool>(
                name: "IsBaseUnit",
                table: "UnitOfMeasures",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "UomType",
                table: "UnitOfMeasures",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "StockUomId",
                table: "Items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // ---------- 2. Backfill ----------

            // Codes: prefer the abbreviation, fall back to the id, then break any collisions so the
            // unique index below can be created.
            migrationBuilder.Sql("""
                UPDATE "UnitOfMeasures"
                SET "Code" = CASE
                        WHEN COALESCE(NULLIF(TRIM("Abbreviation"), ''), '') = ''
                            THEN 'uom-' || "UomId"::text
                        ELSE TRIM("Abbreviation")
                    END
                WHERE COALESCE("Code", '') = '';
                """);

            migrationBuilder.Sql("""
                UPDATE "UnitOfMeasures" u
                SET "Code" = u."Code" || '-' || u."UomId"::text
                WHERE EXISTS (
                    SELECT 1 FROM "UnitOfMeasures" o
                    WHERE lower(o."Code") = lower(u."Code") AND o."UomId" < u."UomId"
                );
                """);

            // Dimensions and factors, matched on abbreviation. Factors are "base units per one of
            // this unit", with bases kg, L, pcs and m.
            migrationBuilder.Sql("""
                UPDATE "UnitOfMeasures" SET "UomType" = 'Weight', "ConversionFactor" = 1, "IsBaseUnit" = true
                WHERE lower(TRIM("Abbreviation")) IN ('kg', 'kgs', 'kilogram');

                UPDATE "UnitOfMeasures" SET "UomType" = 'Weight', "ConversionFactor" = 0.001, "IsBaseUnit" = false
                WHERE lower(TRIM("Abbreviation")) IN ('g', 'gram', 'grams');

                UPDATE "UnitOfMeasures" SET "UomType" = 'Weight', "ConversionFactor" = 50, "IsBaseUnit" = false
                WHERE lower(TRIM("Abbreviation")) IN ('sack');

                UPDATE "UnitOfMeasures" SET "UomType" = 'Volume', "ConversionFactor" = 1, "IsBaseUnit" = true
                WHERE lower(TRIM("Abbreviation")) IN ('l', 'litre', 'liter');

                UPDATE "UnitOfMeasures" SET "UomType" = 'Volume', "ConversionFactor" = 0.001, "IsBaseUnit" = false
                WHERE lower(TRIM("Abbreviation")) IN ('ml', 'millilitre', 'milliliter');

                UPDATE "UnitOfMeasures" SET "UomType" = 'Length', "ConversionFactor" = 1, "IsBaseUnit" = true
                WHERE lower(TRIM("Abbreviation")) IN ('m', 'meter', 'metre');

                -- Anything unrecognised becomes an each-style count with a factor of 1. That keeps it
                -- usable and, crucially, keeps it from converting into a weight or a volume.
                UPDATE "UnitOfMeasures" SET "UomType" = 'Count', "ConversionFactor" = 1, "IsBaseUnit" = false
                WHERE COALESCE("UomType", '') = '';

                UPDATE "UnitOfMeasures" SET "IsBaseUnit" = true
                WHERE lower(TRIM("Abbreviation")) IN ('pcs', 'pc', 'piece', 'pieces');

                -- A factor of zero or less would make conversion divide by zero.
                UPDATE "UnitOfMeasures" SET "ConversionFactor" = 1 WHERE "ConversionFactor" <= 0;
                """);

            // Exactly one base per dimension: drop extras, then promote a base for any dimension that
            // ended up without one.
            migrationBuilder.Sql("""
                UPDATE "UnitOfMeasures" u SET "IsBaseUnit" = false
                WHERE u."IsBaseUnit" AND EXISTS (
                    SELECT 1 FROM "UnitOfMeasures" o
                    WHERE o."IsBaseUnit" AND o."UomType" = u."UomType" AND o."UomId" < u."UomId"
                );

                UPDATE "UnitOfMeasures" u SET "IsBaseUnit" = true
                WHERE NOT EXISTS (
                        SELECT 1 FROM "UnitOfMeasures" b
                        WHERE b."IsBaseUnit" AND b."UomType" = u."UomType")
                  AND u."UomId" = (
                        SELECT MIN(o."UomId") FROM "UnitOfMeasures" o WHERE o."UomType" = u."UomType");
                """);

            // Point every non-base unit at its dimension's base; bases reference nothing.
            migrationBuilder.Sql("""
                UPDATE "UnitOfMeasures" SET "BaseUomId" = NULL WHERE "IsBaseUnit";

                UPDATE "UnitOfMeasures" u SET "BaseUomId" = b."UomId"
                FROM (
                    SELECT "UomType", MIN("UomId") AS "UomId"
                    FROM "UnitOfMeasures" WHERE "IsBaseUnit" GROUP BY "UomType"
                ) b
                WHERE b."UomType" = u."UomType" AND NOT u."IsBaseUnit";
                """);

            // Items stock in the unit they were already defined with, which is what the old code
            // silently assumed. The second statement is a safety net for any orphaned reference.
            migrationBuilder.Sql("""
                UPDATE "Items" SET "StockUomId" = "UomId" WHERE "StockUomId" = 0;

                UPDATE "Items" i SET "StockUomId" = (SELECT MIN("UomId") FROM "UnitOfMeasures")
                WHERE NOT EXISTS (
                    SELECT 1 FROM "UnitOfMeasures" u WHERE u."UomId" = i."StockUomId"
                );
                """);

            // ---------- 3. Constraints ----------

            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasures_BaseUomId",
                table: "UnitOfMeasures",
                column: "BaseUomId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitOfMeasures_Code",
                table: "UnitOfMeasures",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Items_StockUomId",
                table: "Items",
                column: "StockUomId");

            migrationBuilder.AddForeignKey(
                name: "FK_Items_UnitOfMeasures_StockUomId",
                table: "Items",
                column: "StockUomId",
                principalTable: "UnitOfMeasures",
                principalColumn: "UomId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Items_UnitOfMeasures_UomId",
                table: "Items",
                column: "UomId",
                principalTable: "UnitOfMeasures",
                principalColumn: "UomId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UnitOfMeasures_UnitOfMeasures_BaseUomId",
                table: "UnitOfMeasures",
                column: "BaseUomId",
                principalTable: "UnitOfMeasures",
                principalColumn: "UomId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Items_UnitOfMeasures_StockUomId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_UnitOfMeasures_UomId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_UnitOfMeasures_UnitOfMeasures_BaseUomId",
                table: "UnitOfMeasures");

            migrationBuilder.DropIndex(
                name: "IX_UnitOfMeasures_BaseUomId",
                table: "UnitOfMeasures");

            migrationBuilder.DropIndex(
                name: "IX_UnitOfMeasures_Code",
                table: "UnitOfMeasures");

            migrationBuilder.DropIndex(
                name: "IX_Items_StockUomId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "BaseUomId",
                table: "UnitOfMeasures");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "UnitOfMeasures");

            migrationBuilder.DropColumn(
                name: "ConversionFactor",
                table: "UnitOfMeasures");

            migrationBuilder.DropColumn(
                name: "IsBaseUnit",
                table: "UnitOfMeasures");

            migrationBuilder.DropColumn(
                name: "UomType",
                table: "UnitOfMeasures");

            migrationBuilder.DropColumn(
                name: "StockUomId",
                table: "Items");

            migrationBuilder.AddForeignKey(
                name: "FK_Items_UnitOfMeasures_UomId",
                table: "Items",
                column: "UomId",
                principalTable: "UnitOfMeasures",
                principalColumn: "UomId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
