using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceAppointmentReschedulePolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ProposedWindowEndUtc",
                table: "ServiceAppointments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProposedWindowStartUtc",
                table: "ServiceAppointments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RescheduleRequestReason",
                table: "ServiceAppointments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RescheduleRequestedAtUtc",
                table: "ServiceAppointments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RescheduleRequestedByRole",
                table: "ServiceAppointments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAppointments_ProviderId_ProposedWindowStartUtc_ProposedWindowEndUtc",
                table: "ServiceAppointments",
                columns: new[] { "ProviderId", "ProposedWindowStartUtc", "ProposedWindowEndUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ServiceAppointments_ProviderId_ProposedWindowStartUtc_ProposedWindowEndUtc",
                table: "ServiceAppointments");

            migrationBuilder.DropColumn(
                name: "ProposedWindowEndUtc",
                table: "ServiceAppointments");

            migrationBuilder.DropColumn(
                name: "ProposedWindowStartUtc",
                table: "ServiceAppointments");

            migrationBuilder.DropColumn(
                name: "RescheduleRequestReason",
                table: "ServiceAppointments");

            migrationBuilder.DropColumn(
                name: "RescheduleRequestedAtUtc",
                table: "ServiceAppointments");

            migrationBuilder.DropColumn(
                name: "RescheduleRequestedByRole",
                table: "ServiceAppointments");
        }
    }
}
