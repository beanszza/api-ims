using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_scm.Migrations
{
    /// <inheritdoc />
    public partial class AddMasterDataCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SupplierCode",
                table: "Suppliers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RecipeCode",
                table: "Recipes",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ItemCode",
                table: "Items",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SupplierCode",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "RecipeCode",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "ItemCode",
                table: "Items");
        }
    }
}
