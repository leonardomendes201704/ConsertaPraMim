using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceAppointmentArrivalAndStart : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "ArrivedAccuracyMeters",
                table: "ServiceAppointments",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArrivedAtUtc",
                table: "ServiceAppointments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ArrivedLatitude",
                table: "ServiceAppointments",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ArrivedLongitude",
                table: "ServiceAppointments",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArrivedManualReason",
                table: "ServiceAppointments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAtUtc",
                table: "ServiceAppointments",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArrivedAccuracyMeters",
                table: "ServiceAppointments");

            migrationBuilder.DropColumn(
                name: "ArrivedAtUtc",
                table: "ServiceAppointments");

            migrationBuilder.DropColumn(
                name: "ArrivedLatitude",
                table: "ServiceAppointments");

            migrationBuilder.DropColumn(
                name: "ArrivedLongitude",
                table: "ServiceAppointments");

            migrationBuilder.DropColumn(
                name: "ArrivedManualReason",
                table: "ServiceAppointments");

            migrationBuilder.DropColumn(
                name: "StartedAtUtc",
                table: "ServiceAppointments");
        }
    }
}
