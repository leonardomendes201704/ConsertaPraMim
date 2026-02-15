using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentReminderDeliveryAndResponseTelemetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAtUtc",
                table: "AppointmentReminderDispatches",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ResponseConfirmed",
                table: "AppointmentReminderDispatches",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponseReason",
                table: "AppointmentReminderDispatches",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResponseReceivedAtUtc",
                table: "AppointmentReminderDispatches",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveredAtUtc",
                table: "AppointmentReminderDispatches");

            migrationBuilder.DropColumn(
                name: "ResponseConfirmed",
                table: "AppointmentReminderDispatches");

            migrationBuilder.DropColumn(
                name: "ResponseReason",
                table: "AppointmentReminderDispatches");

            migrationBuilder.DropColumn(
                name: "ResponseReceivedAtUtc",
                table: "AppointmentReminderDispatches");
        }
    }
}
