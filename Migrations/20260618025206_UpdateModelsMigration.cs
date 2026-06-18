using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_scm.Migrations
{
    /// <inheritdoc />
    public partial class UpdateModelsMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The database already has these columns because EnsureCreated() ran successfully 
            // before this migration was generated. We leave this empty so EF Core marks the 
            // migration as applied without trying to add the existing columns again.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InspectedBy",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "QaInspectedDate",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "QaNotes",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "QaStatus",
                table: "PurchaseOrders");
        }
    }
}
