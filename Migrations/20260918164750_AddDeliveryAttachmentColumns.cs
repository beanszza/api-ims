using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_scm.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryAttachmentColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArrivalAttachment",
                table: "Deliveries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DispatchAttachment",
                table: "Deliveries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentType",
                table: "Deliveries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScheduledAttachment",
                table: "Deliveries",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArrivalAttachment",
                table: "Deliveries");

            migrationBuilder.DropColumn(
                name: "DispatchAttachment",
                table: "Deliveries");

            migrationBuilder.DropColumn(
                name: "PaymentType",
                table: "Deliveries");

            migrationBuilder.DropColumn(
                name: "ScheduledAttachment",
                table: "Deliveries");
        }
    }
}
