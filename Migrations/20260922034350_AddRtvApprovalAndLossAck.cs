using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_scm.Migrations
{
    /// <inheritdoc />
    public partial class AddRtvApprovalAndLossAck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalRequestNotes",
                table: "ReturnToVendors",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "ReturnToVendors",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedBy",
                table: "ReturnToVendors",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAt",
                table: "ReturnToVendors",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectedBy",
                table: "ReturnToVendors",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "ReturnToVendors",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AcknowledgedAt",
                table: "LossReports",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcknowledgedBy",
                table: "LossReports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAcknowledged",
                table: "LossReports",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovalRequestNotes",
                table: "ReturnToVendors");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "ReturnToVendors");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "ReturnToVendors");

            migrationBuilder.DropColumn(
                name: "RejectedAt",
                table: "ReturnToVendors");

            migrationBuilder.DropColumn(
                name: "RejectedBy",
                table: "ReturnToVendors");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "ReturnToVendors");

            migrationBuilder.DropColumn(
                name: "AcknowledgedAt",
                table: "LossReports");

            migrationBuilder.DropColumn(
                name: "AcknowledgedBy",
                table: "LossReports");

            migrationBuilder.DropColumn(
                name: "IsAcknowledged",
                table: "LossReports");
        }
    }
}
