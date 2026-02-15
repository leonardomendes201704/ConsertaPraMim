using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceAppointmentNoShowRiskAssessment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NoShowRiskCalculatedAtUtc",
                table: "ServiceAppointments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NoShowRiskLevel",
                table: "ServiceAppointments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NoShowRiskReasons",
                table: "ServiceAppointments",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NoShowRiskScore",
                table: "ServiceAppointments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinClientHistoryRiskEvents",
                table: "ServiceAppointmentNoShowRiskPolicies",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinProviderHistoryRiskEvents",
                table: "ServiceAppointmentNoShowRiskPolicies",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAppointments_NoShowRiskLevel_WindowStartUtc",
                table: "ServiceAppointments",
                columns: new[] { "NoShowRiskLevel", "WindowStartUtc" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ServiceAppointments_NoShowRiskScore_Range",
                table: "ServiceAppointments",
                sql: "[NoShowRiskScore] IS NULL OR ([NoShowRiskScore] BETWEEN 0 AND 100)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_NoShowRiskPolicy_MinClientHistoryRiskEvents_Range",
                table: "ServiceAppointmentNoShowRiskPolicies",
                sql: "[MinClientHistoryRiskEvents] BETWEEN 1 AND 50");

            migrationBuilder.AddCheckConstraint(
                name: "CK_NoShowRiskPolicy_MinProviderHistoryRiskEvents_Range",
                table: "ServiceAppointmentNoShowRiskPolicies",
                sql: "[MinProviderHistoryRiskEvents] BETWEEN 1 AND 50");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ServiceAppointments_NoShowRiskLevel_WindowStartUtc",
                table: "ServiceAppointments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ServiceAppointments_NoShowRiskScore_Range",
                table: "ServiceAppointments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_NoShowRiskPolicy_MinClientHistoryRiskEvents_Range",
                table: "ServiceAppointmentNoShowRiskPolicies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_NoShowRiskPolicy_MinProviderHistoryRiskEvents_Range",
                table: "ServiceAppointmentNoShowRiskPolicies");

            migrationBuilder.DropColumn(
                name: "NoShowRiskCalculatedAtUtc",
                table: "ServiceAppointments");

            migrationBuilder.DropColumn(
                name: "NoShowRiskLevel",
                table: "ServiceAppointments");

            migrationBuilder.DropColumn(
                name: "NoShowRiskReasons",
                table: "ServiceAppointments");

            migrationBuilder.DropColumn(
                name: "NoShowRiskScore",
                table: "ServiceAppointments");

            migrationBuilder.DropColumn(
                name: "MinClientHistoryRiskEvents",
                table: "ServiceAppointmentNoShowRiskPolicies");

            migrationBuilder.DropColumn(
                name: "MinProviderHistoryRiskEvents",
                table: "ServiceAppointmentNoShowRiskPolicies");
        }
    }
}
