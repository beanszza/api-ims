using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace api_scm.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovalEngineAndPurchaseRequisitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApprovalRequests",
                columns: table => new
                {
                    ApprovalRequestId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<int>(type: "integer", nullable: false),
                    DocumentNumber = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    RequestedBy = table.Column<string>(type: "text", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApproverId = table.Column<string>(type: "text", nullable: true),
                    ApproverName = table.Column<string>(type: "text", nullable: true),
                    ActionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Comments = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalRequests", x => x.ApprovalRequestId);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseRequisitions",
                columns: table => new
                {
                    PrId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PrNumber = table.Column<string>(type: "text", nullable: false),
                    Department = table.Column<string>(type: "text", nullable: false),
                    RequestedBy = table.Column<string>(type: "text", nullable: false),
                    RequestDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequiredDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Purpose = table.Column<string>(type: "text", nullable: true),
                    EstimatedTotalAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    GeneratedPoNumbers = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRequisitions", x => x.PrId);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseRequisitionItems",
                columns: table => new
                {
                    PrItemId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PrId = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    SuggestedSupplierId = table.Column<int>(type: "integer", nullable: true),
                    RequestedQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    PurchaseUomId = table.Column<int>(type: "integer", nullable: false),
                    EstimatedUnitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRequisitionItems", x => x.PrItemId);
                    table.ForeignKey(
                        name: "FK_PurchaseRequisitionItems_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseRequisitionItems_PurchaseRequisitions_PrId",
                        column: x => x.PrId,
                        principalTable: "PurchaseRequisitions",
                        principalColumn: "PrId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseRequisitionItems_Suppliers_SuggestedSupplierId",
                        column: x => x.SuggestedSupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseRequisitionItems_UnitOfMeasures_PurchaseUomId",
                        column: x => x.PurchaseUomId,
                        principalTable: "UnitOfMeasures",
                        principalColumn: "UomId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_Entity",
                table: "ApprovalRequests",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_RequestedAt",
                table: "ApprovalRequests",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_Status",
                table: "ApprovalRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitionItems_ItemId",
                table: "PurchaseRequisitionItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitionItems_PrId",
                table: "PurchaseRequisitionItems",
                column: "PrId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitionItems_PurchaseUomId",
                table: "PurchaseRequisitionItems",
                column: "PurchaseUomId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitionItems_SuggestedSupplierId",
                table: "PurchaseRequisitionItems",
                column: "SuggestedSupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_PrNumber",
                table: "PurchaseRequisitions",
                column: "PrNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_Status",
                table: "PurchaseRequisitions",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalRequests");

            migrationBuilder.DropTable(
                name: "PurchaseRequisitionItems");

            migrationBuilder.DropTable(
                name: "PurchaseRequisitions");
        }
    }
}
