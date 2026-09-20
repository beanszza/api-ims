using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace api_scm.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryLot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryLots",
                columns: table => new
                {
                    LotId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LotCode = table.Column<string>(type: "text", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    LocationId = table.Column<int>(type: "integer", nullable: false),
                    SourceType = table.Column<string>(type: "text", nullable: false),
                    SupplierId = table.Column<int>(type: "integer", nullable: true),
                    SupplierLotNo = table.Column<string>(type: "text", nullable: true),
                    GrnLineId = table.Column<int>(type: "integer", nullable: true),
                    ProductionOrderId = table.Column<int>(type: "integer", nullable: true),
                    ReceivedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ManufactureDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsExpiryEstimated = table.Column<bool>(type: "boolean", nullable: false),
                    QuantityReceived = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    QuantityRemaining = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    UomId = table.Column<int>(type: "integer", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    HoldReason = table.Column<string>(type: "text", nullable: true),
                    IsOpeningBalance = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryLots", x => x.LotId);
                    table.CheckConstraint("CK_InventoryLots_PurchasedHasSupplier", "\"SourceType\" <> 'Purchased' OR \"SupplierId\" IS NOT NULL");
                    table.CheckConstraint("CK_InventoryLots_QuantityReceived_Positive", "\"QuantityReceived\" > 0");
                    table.CheckConstraint("CK_InventoryLots_QuantityRemaining_NotNegative", "\"QuantityRemaining\" >= 0");
                    table.CheckConstraint("CK_InventoryLots_QuantityRemaining_WithinReceived", "\"QuantityRemaining\" <= \"QuantityReceived\"");
                    table.ForeignKey(
                        name: "FK_InventoryLots_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryLots_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryLots_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryLots_UnitOfMeasures_UomId",
                        column: x => x.UomId,
                        principalTable: "UnitOfMeasures",
                        principalColumn: "UomId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLots_ExpiryDate",
                table: "InventoryLots",
                column: "ExpiryDate");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLots_ItemLocationStatus",
                table: "InventoryLots",
                columns: new[] { "ItemId", "LocationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLots_LocationId",
                table: "InventoryLots",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLots_LotCode",
                table: "InventoryLots",
                column: "LotCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLots_SupplierId",
                table: "InventoryLots",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLots_UomId",
                table: "InventoryLots",
                column: "UomId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryLots");
        }
    }
}
