using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace api_scm.Migrations
{
    /// <inheritdoc />
    public partial class AddStockInModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAt",
                table: "GoodsReceipts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectedBy",
                table: "GoodsReceipts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "GoodsReceipts",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StockIns",
                columns: table => new
                {
                    StockInId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StockInNumber = table.Column<string>(type: "text", nullable: false),
                    GrnId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SubmittedBy = table.Column<string>(type: "text", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<string>(type: "text", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedBy = table.Column<string>(type: "text", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockIns", x => x.StockInId);
                    table.ForeignKey(
                        name: "FK_StockIns_GoodsReceipts_GrnId",
                        column: x => x.GrnId,
                        principalTable: "GoodsReceipts",
                        principalColumn: "GrnId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockInLines",
                columns: table => new
                {
                    StockInLineId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StockInId = table.Column<int>(type: "integer", nullable: false),
                    GrnItemId = table.Column<int>(type: "integer", nullable: true),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    PurchaseUomId = table.Column<int>(type: "integer", nullable: true),
                    QuantityToStock = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    CurrentStockBeforeCommit = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    LotCode = table.Column<string>(type: "text", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CommittedToInventory = table.Column<bool>(type: "boolean", nullable: false),
                    InventoryLotId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockInLines", x => x.StockInLineId);
                    table.ForeignKey(
                        name: "FK_StockInLines_GoodsReceiptItems_GrnItemId",
                        column: x => x.GrnItemId,
                        principalTable: "GoodsReceiptItems",
                        principalColumn: "GrnItemId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StockInLines_InventoryLots_InventoryLotId",
                        column: x => x.InventoryLotId,
                        principalTable: "InventoryLots",
                        principalColumn: "LotId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StockInLines_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockInLines_StockIns_StockInId",
                        column: x => x.StockInId,
                        principalTable: "StockIns",
                        principalColumn: "StockInId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StockInLines_UnitOfMeasures_PurchaseUomId",
                        column: x => x.PurchaseUomId,
                        principalTable: "UnitOfMeasures",
                        principalColumn: "UomId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockInLines_GrnItemId",
                table: "StockInLines",
                column: "GrnItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StockInLines_InventoryLotId",
                table: "StockInLines",
                column: "InventoryLotId");

            migrationBuilder.CreateIndex(
                name: "IX_StockInLines_ItemId",
                table: "StockInLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StockInLines_PurchaseUomId",
                table: "StockInLines",
                column: "PurchaseUomId");

            migrationBuilder.CreateIndex(
                name: "IX_StockInLines_StockInId",
                table: "StockInLines",
                column: "StockInId");

            migrationBuilder.CreateIndex(
                name: "IX_StockIns_GrnId",
                table: "StockIns",
                column: "GrnId");

            migrationBuilder.CreateIndex(
                name: "IX_StockIns_StockInNumber",
                table: "StockIns",
                column: "StockInNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockInLines");

            migrationBuilder.DropTable(
                name: "StockIns");

            migrationBuilder.DropColumn(
                name: "RejectedAt",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "RejectedBy",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "GoodsReceipts");
        }
    }
}
