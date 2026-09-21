using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace api_scm.Migrations
{
    /// <inheritdoc />
    public partial class AddGrnToInventoryFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "QualityInspections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompletedBy",
                table: "QualityInspections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAcceptedQuantity",
                table: "QualityInspections",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalReceivedQuantity",
                table: "QualityInspections",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalRejectedQuantity",
                table: "QualityInspections",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Disposition",
                table: "NonConformanceReports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DispositionAt",
                table: "NonConformanceReports",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DispositionBy",
                table: "NonConformanceReports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DispositionNotes",
                table: "NonConformanceReports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsExpiryTracked",
                table: "Items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsLotTracked",
                table: "Items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsQaRequired",
                table: "Items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSerialTracked",
                table: "Items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "GoodsReceipts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "GoodsReceipts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "PostedAt",
                table: "GoodsReceipts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostedBy",
                table: "GoodsReceipts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceivingBay",
                table: "GoodsReceipts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierDrNumber",
                table: "GoodsReceipts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierInvoiceNumber",
                table: "GoodsReceipts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeclaredQuantity",
                table: "GoodsReceiptItems",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeliveryItemId",
                table: "GoodsReceiptItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "GoodsReceiptItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PreviouslyReceivedQuantity",
                table: "GoodsReceiptItems",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VarianceQuantity",
                table: "GoodsReceiptItems",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "VarianceType",
                table: "GoodsReceiptItems",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LossReports",
                columns: table => new
                {
                    LossReportId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LossReportNumber = table.Column<string>(type: "text", nullable: false),
                    DiscrepancyId = table.Column<int>(type: "integer", nullable: true),
                    GrnId = table.Column<int>(type: "integer", nullable: true),
                    PoId = table.Column<int>(type: "integer", nullable: true),
                    LotId = table.Column<int>(type: "integer", nullable: true),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    LostQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    UomId = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    AuthorisedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StockLedgerEntryId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LossReports", x => x.LossReportId);
                    table.ForeignKey(
                        name: "FK_LossReports_GoodsReceipts_GrnId",
                        column: x => x.GrnId,
                        principalTable: "GoodsReceipts",
                        principalColumn: "GrnId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LossReports_InventoryLots_LotId",
                        column: x => x.LotId,
                        principalTable: "InventoryLots",
                        principalColumn: "LotId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LossReports_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LossReports_PurchaseOrders_PoId",
                        column: x => x.PoId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "PoId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LossReports_StockLedgers_StockLedgerEntryId",
                        column: x => x.StockLedgerEntryId,
                        principalTable: "StockLedgers",
                        principalColumn: "LedgerId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LossReports_UnitOfMeasures_UomId",
                        column: x => x.UomId,
                        principalTable: "UnitOfMeasures",
                        principalColumn: "UomId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PutAwayTransactions",
                columns: table => new
                {
                    PutAwayId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PutAwayNumber = table.Column<string>(type: "text", nullable: false),
                    GrnId = table.Column<int>(type: "integer", nullable: false),
                    GrnItemId = table.Column<int>(type: "integer", nullable: false),
                    QaInspectionId = table.Column<int>(type: "integer", nullable: true),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    AcceptedQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    UomId = table.Column<int>(type: "integer", nullable: false),
                    DestinationLocationId = table.Column<int>(type: "integer", nullable: true),
                    LotId = table.Column<int>(type: "integer", nullable: true),
                    LotCode = table.Column<string>(type: "text", nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SerialNumber = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    PerformedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PutAwayTransactions", x => x.PutAwayId);
                    table.ForeignKey(
                        name: "FK_PutAwayTransactions_GoodsReceiptItems_GrnItemId",
                        column: x => x.GrnItemId,
                        principalTable: "GoodsReceiptItems",
                        principalColumn: "GrnItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PutAwayTransactions_GoodsReceipts_GrnId",
                        column: x => x.GrnId,
                        principalTable: "GoodsReceipts",
                        principalColumn: "GrnId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PutAwayTransactions_InventoryLots_LotId",
                        column: x => x.LotId,
                        principalTable: "InventoryLots",
                        principalColumn: "LotId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PutAwayTransactions_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PutAwayTransactions_Locations_DestinationLocationId",
                        column: x => x.DestinationLocationId,
                        principalTable: "Locations",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PutAwayTransactions_QualityInspections_QaInspectionId",
                        column: x => x.QaInspectionId,
                        principalTable: "QualityInspections",
                        principalColumn: "InspectionId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PutAwayTransactions_UnitOfMeasures_UomId",
                        column: x => x.UomId,
                        principalTable: "UnitOfMeasures",
                        principalColumn: "UomId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Discrepancies",
                columns: table => new
                {
                    DiscrepancyId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DiscrepancyNumber = table.Column<string>(type: "text", nullable: false),
                    DiscrepancyType = table.Column<string>(type: "text", nullable: false),
                    GrnId = table.Column<int>(type: "integer", nullable: false),
                    GrnNumber = table.Column<string>(type: "text", nullable: false),
                    PoId = table.Column<int>(type: "integer", nullable: false),
                    PoNumber = table.Column<string>(type: "text", nullable: false),
                    DeliveryId = table.Column<int>(type: "integer", nullable: true),
                    DeliveryNumber = table.Column<string>(type: "text", nullable: true),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    OrderedQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    PreviouslyReceivedQty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    CurrentReceivedQty = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    DiscrepancyQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ResolutionType = table.Column<string>(type: "text", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "text", nullable: true),
                    LossReportId = table.Column<int>(type: "integer", nullable: true),
                    RtvId = table.Column<int>(type: "integer", nullable: true),
                    ResolvedBy = table.Column<string>(type: "text", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NcrId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Discrepancies", x => x.DiscrepancyId);
                    table.ForeignKey(
                        name: "FK_Discrepancies_Deliveries_DeliveryId",
                        column: x => x.DeliveryId,
                        principalTable: "Deliveries",
                        principalColumn: "DeliveryId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Discrepancies_GoodsReceipts_GrnId",
                        column: x => x.GrnId,
                        principalTable: "GoodsReceipts",
                        principalColumn: "GrnId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Discrepancies_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Discrepancies_LossReports_LossReportId",
                        column: x => x.LossReportId,
                        principalTable: "LossReports",
                        principalColumn: "LossReportId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Discrepancies_NonConformanceReports_NcrId",
                        column: x => x.NcrId,
                        principalTable: "NonConformanceReports",
                        principalColumn: "NcrId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Discrepancies_PurchaseOrders_PoId",
                        column: x => x.PoId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "PoId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Discrepancies_ReturnToVendors_RtvId",
                        column: x => x.RtvId,
                        principalTable: "ReturnToVendors",
                        principalColumn: "RtvId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptItems_DeliveryItemId",
                table: "GoodsReceiptItems",
                column: "DeliveryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Discrepancies_DeliveryId",
                table: "Discrepancies",
                column: "DeliveryId");

            migrationBuilder.CreateIndex(
                name: "IX_Discrepancies_DiscrepancyNumber",
                table: "Discrepancies",
                column: "DiscrepancyNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Discrepancies_GrnId",
                table: "Discrepancies",
                column: "GrnId");

            migrationBuilder.CreateIndex(
                name: "IX_Discrepancies_ItemId",
                table: "Discrepancies",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Discrepancies_LossReportId",
                table: "Discrepancies",
                column: "LossReportId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Discrepancies_NcrId",
                table: "Discrepancies",
                column: "NcrId");

            migrationBuilder.CreateIndex(
                name: "IX_Discrepancies_PoId",
                table: "Discrepancies",
                column: "PoId");

            migrationBuilder.CreateIndex(
                name: "IX_Discrepancies_RtvId",
                table: "Discrepancies",
                column: "RtvId");

            migrationBuilder.CreateIndex(
                name: "IX_LossReports_DiscrepancyId",
                table: "LossReports",
                column: "DiscrepancyId");

            migrationBuilder.CreateIndex(
                name: "IX_LossReports_GrnId",
                table: "LossReports",
                column: "GrnId");

            migrationBuilder.CreateIndex(
                name: "IX_LossReports_ItemId",
                table: "LossReports",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_LossReports_LossReportNumber",
                table: "LossReports",
                column: "LossReportNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LossReports_LotId",
                table: "LossReports",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_LossReports_PoId",
                table: "LossReports",
                column: "PoId");

            migrationBuilder.CreateIndex(
                name: "IX_LossReports_StockLedgerEntryId",
                table: "LossReports",
                column: "StockLedgerEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_LossReports_UomId",
                table: "LossReports",
                column: "UomId");

            migrationBuilder.CreateIndex(
                name: "IX_PutAwayTransactions_DestinationLocationId",
                table: "PutAwayTransactions",
                column: "DestinationLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_PutAwayTransactions_GrnId",
                table: "PutAwayTransactions",
                column: "GrnId");

            migrationBuilder.CreateIndex(
                name: "IX_PutAwayTransactions_GrnItemId",
                table: "PutAwayTransactions",
                column: "GrnItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PutAwayTransactions_ItemId",
                table: "PutAwayTransactions",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PutAwayTransactions_LotId",
                table: "PutAwayTransactions",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_PutAwayTransactions_PutAwayNumber",
                table: "PutAwayTransactions",
                column: "PutAwayNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PutAwayTransactions_QaInspectionId",
                table: "PutAwayTransactions",
                column: "QaInspectionId");

            migrationBuilder.CreateIndex(
                name: "IX_PutAwayTransactions_UomId",
                table: "PutAwayTransactions",
                column: "UomId");

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsReceiptItems_DeliveryItems_DeliveryItemId",
                table: "GoodsReceiptItems",
                column: "DeliveryItemId",
                principalTable: "DeliveryItems",
                principalColumn: "DeliveryItemId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GoodsReceiptItems_DeliveryItems_DeliveryItemId",
                table: "GoodsReceiptItems");

            migrationBuilder.DropTable(
                name: "Discrepancies");

            migrationBuilder.DropTable(
                name: "PutAwayTransactions");

            migrationBuilder.DropTable(
                name: "LossReports");

            migrationBuilder.DropIndex(
                name: "IX_GoodsReceiptItems_DeliveryItemId",
                table: "GoodsReceiptItems");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "QualityInspections");

            migrationBuilder.DropColumn(
                name: "CompletedBy",
                table: "QualityInspections");

            migrationBuilder.DropColumn(
                name: "TotalAcceptedQuantity",
                table: "QualityInspections");

            migrationBuilder.DropColumn(
                name: "TotalReceivedQuantity",
                table: "QualityInspections");

            migrationBuilder.DropColumn(
                name: "TotalRejectedQuantity",
                table: "QualityInspections");

            migrationBuilder.DropColumn(
                name: "Disposition",
                table: "NonConformanceReports");

            migrationBuilder.DropColumn(
                name: "DispositionAt",
                table: "NonConformanceReports");

            migrationBuilder.DropColumn(
                name: "DispositionBy",
                table: "NonConformanceReports");

            migrationBuilder.DropColumn(
                name: "DispositionNotes",
                table: "NonConformanceReports");

            migrationBuilder.DropColumn(
                name: "IsExpiryTracked",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "IsLotTracked",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "IsQaRequired",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "IsSerialTracked",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "PostedAt",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "PostedBy",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "ReceivingBay",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "SupplierDrNumber",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "SupplierInvoiceNumber",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "DeclaredQuantity",
                table: "GoodsReceiptItems");

            migrationBuilder.DropColumn(
                name: "DeliveryItemId",
                table: "GoodsReceiptItems");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "GoodsReceiptItems");

            migrationBuilder.DropColumn(
                name: "PreviouslyReceivedQuantity",
                table: "GoodsReceiptItems");

            migrationBuilder.DropColumn(
                name: "VarianceQuantity",
                table: "GoodsReceiptItems");

            migrationBuilder.DropColumn(
                name: "VarianceType",
                table: "GoodsReceiptItems");
        }
    }
}
