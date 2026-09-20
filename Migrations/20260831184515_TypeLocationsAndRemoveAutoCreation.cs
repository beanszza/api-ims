using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_scm.Migrations
{
    /// <inheritdoc />
    public partial class TypeLocationsAndRemoveAutoCreation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // LocationType becomes a closed set. The column stays text, so no type change is needed, but
            // the stored values have to be canonicalised first: reads now go through an enum, and a value
            // outside the set throws rather than being quietly tolerated.
            //
            // "Storage" was invented by the old receiving fallback and "FinishedGoods" by the old finished
            // goods posting. Both are canonicalised here rather than left to read-time aliases.
            migrationBuilder.Sql("""
                UPDATE "Locations" SET "LocationType" = 'Warehouse'
                WHERE lower(TRIM("LocationType")) IN ('storage', 'warehouse');

                UPDATE "Locations" SET "LocationType" = 'Finished Goods'
                WHERE lower(TRIM("LocationType")) IN ('finishedgoods', 'finished goods', 'finished good');

                UPDATE "Locations" SET "LocationType" = 'Production'
                WHERE lower(TRIM("LocationType")) = 'production';

                UPDATE "Locations" SET "LocationType" = 'WIP'
                WHERE lower(TRIM("LocationType")) IN ('wip', 'work in progress');

                UPDATE "Locations" SET "LocationType" = 'Quarantine'
                WHERE lower(TRIM("LocationType")) = 'quarantine';

                UPDATE "Locations" SET "LocationType" = 'In Transit'
                WHERE lower(TRIM("LocationType")) IN ('intransit', 'in transit');

                UPDATE "Locations" SET "LocationType" = 'Branch'
                WHERE lower(TRIM("LocationType")) = 'branch';

                UPDATE "Locations" SET "LocationType" = 'Bazaar'
                WHERE lower(TRIM("LocationType")) = 'bazaar';

                UPDATE "Locations" SET "LocationType" = 'Disposal'
                WHERE lower(TRIM("LocationType")) IN ('disposal', 'write-off', 'writeoff');

                -- Anything still unrecognised becomes the empty string, which maps to Unspecified.
                -- Guessing would be worse: labelling an unknown location a Warehouse would make it a
                -- candidate for receiving stock.
                UPDATE "Locations" SET "LocationType" = ''
                WHERE "LocationType" NOT IN (
                    'Warehouse', 'Production', 'WIP', 'Quarantine', 'Finished Goods',
                    'In Transit', 'Branch', 'Bazaar', 'Disposal', '');
                """);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystemLocation",
                table: "Locations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ParentLocationId",
                table: "Locations",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Locations_ParentLocationId",
                table: "Locations",
                column: "ParentLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_SystemRole",
                table: "Locations",
                column: "LocationType",
                unique: true,
                filter: "\"IsSystemLocation\" = true");

            migrationBuilder.AddForeignKey(
                name: "FK_Locations_Locations_ParentLocationId",
                table: "Locations",
                column: "ParentLocationId",
                principalTable: "Locations",
                principalColumn: "LocationId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Locations_Locations_ParentLocationId",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Locations_ParentLocationId",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Locations_SystemRole",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "IsSystemLocation",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "ParentLocationId",
                table: "Locations");
        }
    }
}
