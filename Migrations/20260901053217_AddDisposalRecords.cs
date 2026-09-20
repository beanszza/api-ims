using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace api_scm.Migrations
{
    /// <inheritdoc />
    public partial class AddDisposalRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DisposalRecords",
                columns: table => new
                {
                    DisposalId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DisposalNumber = table.Column<string>(type: "text", nullable: false),
                    DisposalDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    TotalCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    AuthorizedBy = table.Column<string>(type: "text", nullable: false),
                    WitnessName = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisposalRecords", x => x.DisposalId);
                });

            migrationBuilder.CreateTable(
                name: "DisposalRecordItems",
                columns: table => new
                {
                    DisposalItemId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DisposalId = table.Column<int>(type: "integer", nullable: false),
                    LotId = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    QuantityDisposed = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisposalRecordItems", x => x.DisposalItemId);
                    table.ForeignKey(
                        name: "FK_DisposalRecordItems_DisposalRecords_DisposalId",
                        column: x => x.DisposalId,
                        principalTable: "DisposalRecords",
                        principalColumn: "DisposalId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DisposalRecordItems_InventoryLots_LotId",
                        column: x => x.LotId,
                        principalTable: "InventoryLots",
                        principalColumn: "LotId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DisposalRecordItems_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DisposalRecordItems_DisposalId",
                table: "DisposalRecordItems",
                column: "DisposalId");

            migrationBuilder.CreateIndex(
                name: "IX_DisposalRecordItems_ItemId",
                table: "DisposalRecordItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_DisposalRecordItems_LotId",
                table: "DisposalRecordItems",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_DisposalRecords_DisposalNumber",
                table: "DisposalRecords",
                column: "DisposalNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DisposalRecordItems");

            migrationBuilder.DropTable(
                name: "DisposalRecords");
        }
    }
}
