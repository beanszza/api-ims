using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace api_scm.Migrations
{
    /// <inheritdoc />
    public partial class AddCycleCountsAndCosting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CycleCounts",
                columns: table => new
                {
                    CycleCountId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CountNumber = table.Column<string>(type: "text", nullable: false),
                    LocationId = table.Column<int>(type: "integer", nullable: false),
                    CountDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReconciledDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CountedBy = table.Column<string>(type: "text", nullable: false),
                    ReconciledBy = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TotalSystemValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalCountedValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalVarianceValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CycleCounts", x => x.CycleCountId);
                    table.ForeignKey(
                        name: "FK_CycleCounts_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CycleCountItems",
                columns: table => new
                {
                    CycleCountItemId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CycleCountId = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    LotId = table.Column<int>(type: "integer", nullable: true),
                    SystemQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    CountedQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CycleCountItems", x => x.CycleCountItemId);
                    table.ForeignKey(
                        name: "FK_CycleCountItems_CycleCounts_CycleCountId",
                        column: x => x.CycleCountId,
                        principalTable: "CycleCounts",
                        principalColumn: "CycleCountId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CycleCountItems_InventoryLots_LotId",
                        column: x => x.LotId,
                        principalTable: "InventoryLots",
                        principalColumn: "LotId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CycleCountItems_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CycleCountItems_CycleCountId",
                table: "CycleCountItems",
                column: "CycleCountId");

            migrationBuilder.CreateIndex(
                name: "IX_CycleCountItems_ItemId",
                table: "CycleCountItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CycleCountItems_LotId",
                table: "CycleCountItems",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_CycleCounts_CountNumber",
                table: "CycleCounts",
                column: "CountNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CycleCounts_LocationId",
                table: "CycleCounts",
                column: "LocationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CycleCountItems");

            migrationBuilder.DropTable(
                name: "CycleCounts");
        }
    }
}
