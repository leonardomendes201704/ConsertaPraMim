using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceAppointmentPresenceResponseFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ClientPresenceConfirmed",
                table: "ServiceAppointments",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientPresenceReason",
                table: "ServiceAppointments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClientPresenceRespondedAtUtc",
                table: "ServiceAppointments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ProviderPresenceConfirmed",
                table: "ServiceAppointments",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderPresenceReason",
                table: "ServiceAppointments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProviderPresenceRespondedAtUtc",
                table: "ServiceAppointments",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientPresenceConfirmed",
                table: "ServiceAppointments");

            migrationBuilder.DropColumn(
                name: "ClientPresenceReason",
                table: "ServiceAppointments");

            migrationBuilder.DropColumn(
                name: "ClientPresenceRespondedAtUtc",
                table: "ServiceAppointments");

            migrationBuilder.DropColumn(
                name: "ProviderPresenceConfirmed",
                table: "ServiceAppointments");

            migrationBuilder.DropColumn(
                name: "ProviderPresenceReason",
                table: "ServiceAppointments");

            migrationBuilder.DropColumn(
                name: "ProviderPresenceRespondedAtUtc",
                table: "ServiceAppointments");
        }
    }
}
