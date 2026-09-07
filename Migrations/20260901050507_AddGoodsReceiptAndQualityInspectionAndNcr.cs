using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace api_scm.Migrations
{
    /// <inheritdoc />
    public partial class AddGoodsReceiptAndQualityInspectionAndNcr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GoodsReceipts",
                columns: table => new
                {
                    GrnId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GrnNumber = table.Column<string>(type: "text", nullable: false),
                    PoId = table.Column<int>(type: "integer", nullable: false),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    ReceivingLocationId = table.Column<int>(type: "integer", nullable: false),
                    ReceivedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeliveryNoteNumber = table.Column<string>(type: "text", nullable: false),
                    Carrier = table.Column<string>(type: "text", nullable: true),
                    ReceivedBy = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsReceipts", x => x.GrnId);
                    table.ForeignKey(
                        name: "FK_GoodsReceipts_Locations_ReceivingLocationId",
                        column: x => x.ReceivingLocationId,
                        principalTable: "Locations",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoodsReceipts_PurchaseOrders_PoId",
                        column: x => x.PoId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "PoId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoodsReceipts_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QualityInspections",
                columns: table => new
                {
                    InspectionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InspectionNumber = table.Column<string>(type: "text", nullable: false),
                    InspectionType = table.Column<string>(type: "text", nullable: false),
                    ReferenceType = table.Column<string>(type: "text", nullable: false),
                    ReferenceId = table.Column<int>(type: "integer", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "text", nullable: false),
                    InspectorId = table.Column<string>(type: "text", nullable: false),
                    InspectorName = table.Column<string>(type: "text", nullable: false),
                    InspectionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    OverallNotes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityInspections", x => x.InspectionId);
                });

            migrationBuilder.CreateTable(
                name: "GoodsReceiptItems",
                columns: table => new
                {
                    GrnItemId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GrnId = table.Column<int>(type: "integer", nullable: false),
                    PoItemId = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    OrderedQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    DeliveredQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    PurchaseUomId = table.Column<int>(type: "integer", nullable: false),
                    LotId = table.Column<int>(type: "integer", nullable: true),
                    SupplierLotCode = table.Column<string>(type: "text", nullable: true),
                    ManufactureDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsReceiptItems", x => x.GrnItemId);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptItems_GoodsReceipts_GrnId",
                        column: x => x.GrnId,
                        principalTable: "GoodsReceipts",
                        principalColumn: "GrnId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptItems_InventoryLots_LotId",
                        column: x => x.LotId,
                        principalTable: "InventoryLots",
                        principalColumn: "LotId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptItems_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptItems_PurchaseOrderItems_PoItemId",
                        column: x => x.PoItemId,
                        principalTable: "PurchaseOrderItems",
                        principalColumn: "PoItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoodsReceiptItems_UnitOfMeasures_PurchaseUomId",
                        column: x => x.PurchaseUomId,
                        principalTable: "UnitOfMeasures",
                        principalColumn: "UomId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NonConformanceReports",
                columns: table => new
                {
                    NcrId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NcrNumber = table.Column<string>(type: "text", nullable: false),
                    InspectionId = table.Column<int>(type: "integer", nullable: true),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    LotId = table.Column<int>(type: "integer", nullable: true),
                    DefectiveQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    DefectType = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<string>(type: "text", nullable: false),
                    RootCause = table.Column<string>(type: "text", nullable: true),
                    CorrectiveAction = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedBy = table.Column<string>(type: "text", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NonConformanceReports", x => x.NcrId);
                    table.ForeignKey(
                        name: "FK_NonConformanceReports_InventoryLots_LotId",
                        column: x => x.LotId,
                        principalTable: "InventoryLots",
                        principalColumn: "LotId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NonConformanceReports_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NonConformanceReports_QualityInspections_InspectionId",
                        column: x => x.InspectionId,
                        principalTable: "QualityInspections",
                        principalColumn: "InspectionId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_NonConformanceReports_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QualityInspectionItems",
                columns: table => new
                {
                    InspectionItemId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InspectionId = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    LotId = table.Column<int>(type: "integer", nullable: true),
                    DeliveredQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    AcceptedQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    RejectedQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    ConcessionQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    DefectReason = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityInspectionItems", x => x.InspectionItemId);
                    table.ForeignKey(
                        name: "FK_QualityInspectionItems_InventoryLots_LotId",
                        column: x => x.LotId,
                        principalTable: "InventoryLots",
                        principalColumn: "LotId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QualityInspectionItems_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QualityInspectionItems_QualityInspections_InspectionId",
                        column: x => x.InspectionId,
                        principalTable: "QualityInspections",
                        principalColumn: "InspectionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReturnToVendors",
                columns: table => new
                {
                    RtvId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RtvNumber = table.Column<string>(type: "text", nullable: false),
                    NcrId = table.Column<int>(type: "integer", nullable: true),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    LotId = table.Column<int>(type: "integer", nullable: false),
                    ReturnedQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    DispatchedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreditNoteNumber = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnToVendors", x => x.RtvId);
                    table.ForeignKey(
                        name: "FK_ReturnToVendors_InventoryLots_LotId",
                        column: x => x.LotId,
                        principalTable: "InventoryLots",
                        principalColumn: "LotId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnToVendors_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnToVendors_NonConformanceReports_NcrId",
                        column: x => x.NcrId,
                        principalTable: "NonConformanceReports",
                        principalColumn: "NcrId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ReturnToVendors_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptItems_GrnId",
                table: "GoodsReceiptItems",
                column: "GrnId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptItems_ItemId",
                table: "GoodsReceiptItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptItems_LotId",
                table: "GoodsReceiptItems",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptItems_PoItemId",
                table: "GoodsReceiptItems",
                column: "PoItemId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptItems_PurchaseUomId",
                table: "GoodsReceiptItems",
                column: "PurchaseUomId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_GrnNumber",
                table: "GoodsReceipts",
                column: "GrnNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_PoId",
                table: "GoodsReceipts",
                column: "PoId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_ReceivingLocationId",
                table: "GoodsReceipts",
                column: "ReceivingLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_SupplierId",
                table: "GoodsReceipts",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_NonConformanceReports_InspectionId",
                table: "NonConformanceReports",
                column: "InspectionId");

            migrationBuilder.CreateIndex(
                name: "IX_NonConformanceReports_ItemId",
                table: "NonConformanceReports",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_NonConformanceReports_LotId",
                table: "NonConformanceReports",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_NonConformanceReports_NcrNumber",
                table: "NonConformanceReports",
                column: "NcrNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NonConformanceReports_SupplierId",
                table: "NonConformanceReports",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityInspectionItems_InspectionId",
                table: "QualityInspectionItems",
                column: "InspectionId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityInspectionItems_ItemId",
                table: "QualityInspectionItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityInspectionItems_LotId",
                table: "QualityInspectionItems",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_QualityInspections_InspectionNumber",
                table: "QualityInspections",
                column: "InspectionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QualityInspections_Reference",
                table: "QualityInspections",
                columns: new[] { "ReferenceType", "ReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReturnToVendors_ItemId",
                table: "ReturnToVendors",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnToVendors_LotId",
                table: "ReturnToVendors",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnToVendors_NcrId",
                table: "ReturnToVendors",
                column: "NcrId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnToVendors_RtvNumber",
                table: "ReturnToVendors",
                column: "RtvNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReturnToVendors_SupplierId",
                table: "ReturnToVendors",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoodsReceiptItems");

            migrationBuilder.DropTable(
                name: "QualityInspectionItems");

            migrationBuilder.DropTable(
                name: "ReturnToVendors");

            migrationBuilder.DropTable(
                name: "GoodsReceipts");

            migrationBuilder.DropTable(
                name: "NonConformanceReports");

            migrationBuilder.DropTable(
                name: "QualityInspections");
        }
    }
}
