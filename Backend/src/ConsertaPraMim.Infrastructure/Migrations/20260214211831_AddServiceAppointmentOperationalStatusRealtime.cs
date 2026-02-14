using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceAppointmentOperationalStatusRealtime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OperationalStatus",
                table: "ServiceAppointments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperationalStatusReason",
                table: "ServiceAppointments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OperationalStatusUpdatedAtUtc",
                table: "ServiceAppointments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NewOperationalStatus",
                table: "ServiceAppointmentHistories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreviousOperationalStatus",
                table: "ServiceAppointmentHistories",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAppointments_ProviderId_OperationalStatus_WindowStartUtc",
                table: "ServiceAppointments",
                columns: new[] { "ProviderId", "OperationalStatus", "WindowStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAppointmentHistories_ServiceAppointmentId_NewOperationalStatus_OccurredAtUtc",
                table: "ServiceAppointmentHistories",
                columns: new[] { "ServiceAppointmentId", "NewOperationalStatus", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ServiceAppointments_ProviderId_OperationalStatus_WindowStartUtc",
                table: "ServiceAppointments");

            migrationBuilder.DropIndex(
                name: "IX_ServiceAppointmentHistories_ServiceAppointmentId_NewOperationalStatus_OccurredAtUtc",
                table: "ServiceAppointmentHistories");

            migrationBuilder.DropColumn(
                name: "OperationalStatus",
                table: "ServiceAppointments");

            migrationBuilder.DropColumn(
                name: "OperationalStatusReason",
                table: "ServiceAppointments");

            migrationBuilder.DropColumn(
                name: "OperationalStatusUpdatedAtUtc",
                table: "ServiceAppointments");

            migrationBuilder.DropColumn(
                name: "NewOperationalStatus",
                table: "ServiceAppointmentHistories");

            migrationBuilder.DropColumn(
                name: "PreviousOperationalStatus",
                table: "ServiceAppointmentHistories");
        }
    }
}
