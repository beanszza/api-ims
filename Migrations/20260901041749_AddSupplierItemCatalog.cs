using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_scm.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierItemCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupplierItems",
                columns: table => new
                {
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    SupplierSku = table.Column<string>(type: "text", nullable: true),
                    SupplierItemName = table.Column<string>(type: "text", nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    PurchaseUomId = table.Column<int>(type: "integer", nullable: false),
                    PackSize = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    LeadTimeDays = table.Column<int>(type: "integer", nullable: false),
                    MinOrderQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    IsPreferred = table.Column<bool>(type: "boolean", nullable: false),
                    LastPurchasePrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    LastPurchaseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierItems", x => new { x.SupplierId, x.ItemId });
                    table.CheckConstraint("CK_SupplierItems_MinOrderQuantity_Positive", "\"MinOrderQuantity\" > 0");
                    table.CheckConstraint("CK_SupplierItems_PackSize_Positive", "\"PackSize\" > 0");
                    table.CheckConstraint("CK_SupplierItems_UnitPrice_Positive", "\"UnitPrice\" >= 0");
                    table.ForeignKey(
                        name: "FK_SupplierItems_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierItems_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierItems_UnitOfMeasures_PurchaseUomId",
                        column: x => x.PurchaseUomId,
                        principalTable: "UnitOfMeasures",
                        principalColumn: "UomId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierItems_ItemId",
                table: "SupplierItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierItems_PurchaseUomId",
                table: "SupplierItems",
                column: "PurchaseUomId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierItems");
        }
    }
}
