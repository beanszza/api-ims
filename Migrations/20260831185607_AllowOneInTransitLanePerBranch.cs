using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_scm.Migrations
{
    /// <inheritdoc />
    public partial class AllowOneInTransitLanePerBranch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Locations_SystemRole",
                table: "Locations");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_SystemRole",
                table: "Locations",
                column: "LocationType",
                unique: true,
                filter: "\"IsSystemLocation\" = true AND \"LocationType\" <> 'In Transit'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Locations_SystemRole",
                table: "Locations");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_SystemRole",
                table: "Locations",
                column: "LocationType",
                unique: true,
                filter: "\"IsSystemLocation\" = true");
        }
    }
}
