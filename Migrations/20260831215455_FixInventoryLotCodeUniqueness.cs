using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_scm.Migrations
{
    /// <inheritdoc />
    public partial class FixInventoryLotCodeUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InventoryLots_LotCode",
                table: "InventoryLots");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLots_LotCode_LocationId",
                table: "InventoryLots",
                columns: new[] { "LotCode", "LocationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InventoryLots_LotCode_LocationId",
                table: "InventoryLots");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLots_LotCode",
                table: "InventoryLots",
                column: "LotCode",
                unique: true);
        }
    }
}
