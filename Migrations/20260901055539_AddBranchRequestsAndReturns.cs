using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace api_scm.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchRequestsAndReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BranchRequestId",
                table: "StockTransfers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DispatchedDate",
                table: "StockTransfers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DriverName",
                table: "StockTransfers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceivedBy",
                table: "StockTransfers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceivedDate",
                table: "StockTransfers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransferNumber",
                table: "StockTransfers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VehiclePlate",
                table: "StockTransfers",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BranchRequests",
                columns: table => new
                {
                    BranchRequestId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestNumber = table.Column<string>(type: "text", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    RequestDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequiredDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    RequestedBy = table.Column<string>(type: "text", nullable: false),
                    ApprovedBy = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchRequests", x => x.BranchRequestId);
                    table.ForeignKey(
                        name: "FK_BranchRequests_Locations_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Locations",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BranchReturns",
                columns: table => new
                {
                    BranchReturnId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReturnNumber = table.Column<string>(type: "text", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    ReturnDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ReturnedBy = table.Column<string>(type: "text", nullable: false),
                    AuthorizedBy = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchReturns", x => x.BranchReturnId);
                    table.ForeignKey(
                        name: "FK_BranchReturns_Locations_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Locations",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BranchRequestItems",
                columns: table => new
                {
                    BranchRequestItemId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BranchRequestId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    RequestedQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    ApprovedQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    DispatchedQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchRequestItems", x => x.BranchRequestItemId);
                    table.ForeignKey(
                        name: "FK_BranchRequestItems_BranchRequests_BranchRequestId",
                        column: x => x.BranchRequestId,
                        principalTable: "BranchRequests",
                        principalColumn: "BranchRequestId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BranchRequestItems_FinishedProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "FinishedProducts",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BranchReturnItems",
                columns: table => new
                {
                    BranchReturnItemId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BranchReturnId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    LotId = table.Column<int>(type: "integer", nullable: true),
                    ReturnedQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    DefectCondition = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchReturnItems", x => x.BranchReturnItemId);
                    table.ForeignKey(
                        name: "FK_BranchReturnItems_BranchReturns_BranchReturnId",
                        column: x => x.BranchReturnId,
                        principalTable: "BranchReturns",
                        principalColumn: "BranchReturnId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BranchReturnItems_FinishedProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "FinishedProducts",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchReturnItems_InventoryLots_LotId",
                        column: x => x.LotId,
                        principalTable: "InventoryLots",
                        principalColumn: "LotId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_BranchRequestId",
                table: "StockTransfers",
                column: "BranchRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchRequestItems_BranchRequestId",
                table: "BranchRequestItems",
                column: "BranchRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchRequestItems_ProductId",
                table: "BranchRequestItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchRequests_BranchId",
                table: "BranchRequests",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchRequests_RequestNumber",
                table: "BranchRequests",
                column: "RequestNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BranchReturnItems_BranchReturnId",
                table: "BranchReturnItems",
                column: "BranchReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchReturnItems_LotId",
                table: "BranchReturnItems",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchReturnItems_ProductId",
                table: "BranchReturnItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchReturns_BranchId",
                table: "BranchReturns",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchReturns_ReturnNumber",
                table: "BranchReturns",
                column: "ReturnNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StockTransfers_BranchRequests_BranchRequestId",
                table: "StockTransfers",
                column: "BranchRequestId",
                principalTable: "BranchRequests",
                principalColumn: "BranchRequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockTransfers_BranchRequests_BranchRequestId",
                table: "StockTransfers");

            migrationBuilder.DropTable(
                name: "BranchRequestItems");

            migrationBuilder.DropTable(
                name: "BranchReturnItems");

            migrationBuilder.DropTable(
                name: "BranchRequests");

            migrationBuilder.DropTable(
                name: "BranchReturns");

            migrationBuilder.DropIndex(
                name: "IX_StockTransfers_BranchRequestId",
                table: "StockTransfers");

            migrationBuilder.DropColumn(
                name: "BranchRequestId",
                table: "StockTransfers");

            migrationBuilder.DropColumn(
                name: "DispatchedDate",
                table: "StockTransfers");

            migrationBuilder.DropColumn(
                name: "DriverName",
                table: "StockTransfers");

            migrationBuilder.DropColumn(
                name: "ReceivedBy",
                table: "StockTransfers");

            migrationBuilder.DropColumn(
                name: "ReceivedDate",
                table: "StockTransfers");

            migrationBuilder.DropColumn(
                name: "TransferNumber",
                table: "StockTransfers");

            migrationBuilder.DropColumn(
                name: "VehiclePlate",
                table: "StockTransfers");
        }
    }
}
