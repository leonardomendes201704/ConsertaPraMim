using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleCalendarSyncRetryDeadLetterObservability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ServiceAppointmentCalendarSyncs_SyncStatus_Valid",
                table: "ServiceAppointmentCalendarSyncs");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeadLetterAtUtc",
                table: "ServiceAppointmentCalendarSyncs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastErrorCode",
                table: "ServiceAppointmentCalendarSyncs",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LastLatencyMs",
                table: "ServiceAppointmentCalendarSyncs",
                type: "float(10)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastOperation",
                table: "ServiceAppointmentCalendarSyncs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxRetryAttempts",
                table: "ServiceAppointmentCalendarSyncs",
                type: "int",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextRetryAtUtc",
                table: "ServiceAppointmentCalendarSyncs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "ServiceAppointmentCalendarSyncs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAppointmentCalendarSyncs_SyncStatus_NextRetryAtUtc",
                table: "ServiceAppointmentCalendarSyncs",
                columns: new[] { "SyncStatus", "NextRetryAtUtc" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ServiceAppointmentCalendarSyncs_Retry_NonNegative",
                table: "ServiceAppointmentCalendarSyncs",
                sql: "[RetryCount] >= 0 AND [MaxRetryAttempts] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ServiceAppointmentCalendarSyncs_SyncStatus_Valid",
                table: "ServiceAppointmentCalendarSyncs",
                sql: "[SyncStatus] IN (1,2,3,4,5)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ServiceAppointmentCalendarSyncs_SyncStatus_NextRetryAtUtc",
                table: "ServiceAppointmentCalendarSyncs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ServiceAppointmentCalendarSyncs_Retry_NonNegative",
                table: "ServiceAppointmentCalendarSyncs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ServiceAppointmentCalendarSyncs_SyncStatus_Valid",
                table: "ServiceAppointmentCalendarSyncs");

            migrationBuilder.DropColumn(
                name: "DeadLetterAtUtc",
                table: "ServiceAppointmentCalendarSyncs");

            migrationBuilder.DropColumn(
                name: "LastErrorCode",
                table: "ServiceAppointmentCalendarSyncs");

            migrationBuilder.DropColumn(
                name: "LastLatencyMs",
                table: "ServiceAppointmentCalendarSyncs");

            migrationBuilder.DropColumn(
                name: "LastOperation",
                table: "ServiceAppointmentCalendarSyncs");

            migrationBuilder.DropColumn(
                name: "MaxRetryAttempts",
                table: "ServiceAppointmentCalendarSyncs");

            migrationBuilder.DropColumn(
                name: "NextRetryAtUtc",
                table: "ServiceAppointmentCalendarSyncs");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "ServiceAppointmentCalendarSyncs");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ServiceAppointmentCalendarSyncs_SyncStatus_Valid",
                table: "ServiceAppointmentCalendarSyncs",
                sql: "[SyncStatus] IN (1,2,3,4)");
        }
    }
}
