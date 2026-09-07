using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace api_scm.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierDocumentsAndPoLinePricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                table: "PurchaseOrders",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<int>(
                name: "PurchaseUomId",
                table: "PurchaseOrderItems",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPrice",
                table: "PurchaseOrderItems",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            // Ensure at least one default Uom exists and backfill existing PO lines
            migrationBuilder.Sql(@"
                INSERT INTO ""UnitOfMeasures"" (""UomId"", ""Name"", ""Abbreviation"", ""Code"", ""UomType"", ""ConversionFactor"", ""IsBaseUnit"")
                SELECT 1, 'Kilogram', 'kg', 'KG', 'Weight', 1.0, true
                WHERE NOT EXISTS (SELECT 1 FROM ""UnitOfMeasures"" WHERE ""UomId"" = 1);

                UPDATE ""PurchaseOrderItems"" poi
                SET ""PurchaseUomId"" = COALESCE(NULLIF(i.""StockUomId"", 0), NULLIF(i.""UomId"", 0), 1)
                FROM ""Items"" i
                WHERE poi.""ItemId"" = i.""ItemId"";

                UPDATE ""PurchaseOrderItems""
                SET ""PurchaseUomId"" = 1
                WHERE ""PurchaseUomId"" = 0 OR ""PurchaseUomId"" NOT IN (SELECT ""UomId"" FROM ""UnitOfMeasures"");
            ");

            migrationBuilder.CreateTable(
                name: "SupplierDocuments",
                columns: table => new
                {
                    DocumentId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    DocumentType = table.Column<string>(type: "text", nullable: false),
                    DocumentNumber = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FileUrl = table.Column<string>(type: "text", nullable: true),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    VerifiedBy = table.Column<string>(type: "text", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierDocuments", x => x.DocumentId);
                    table.ForeignKey(
                        name: "FK_SupplierDocuments_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderItems_PurchaseUomId",
                table: "PurchaseOrderItems",
                column: "PurchaseUomId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierDocuments_ExpiryDate",
                table: "SupplierDocuments",
                column: "ExpiryDate");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierDocuments_Supplier_Type",
                table: "SupplierDocuments",
                columns: new[] { "SupplierId", "DocumentType" });

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderItems_UnitOfMeasures_PurchaseUomId",
                table: "PurchaseOrderItems",
                column: "PurchaseUomId",
                principalTable: "UnitOfMeasures",
                principalColumn: "UomId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderItems_UnitOfMeasures_PurchaseUomId",
                table: "PurchaseOrderItems");

            migrationBuilder.DropTable(
                name: "SupplierDocuments");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderItems_PurchaseUomId",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "PurchaseUomId",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "UnitPrice",
                table: "PurchaseOrderItems");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                table: "PurchaseOrders",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4);
        }
    }
}
