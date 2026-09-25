using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_scm.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CommittedAt",
                table: "StockIns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommittedBy",
                table: "StockIns",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommittedAt",
                table: "StockIns");

            migrationBuilder.DropColumn(
                name: "CommittedBy",
                table: "StockIns");
        }
    }
}
