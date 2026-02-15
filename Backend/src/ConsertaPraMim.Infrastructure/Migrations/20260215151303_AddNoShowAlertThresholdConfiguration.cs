using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNoShowAlertThresholdConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NoShowAlertThresholdConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    NoShowRateWarningPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    NoShowRateCriticalPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    HighRiskQueueWarningCount = table.Column<int>(type: "int", nullable: false),
                    HighRiskQueueCriticalCount = table.Column<int>(type: "int", nullable: false),
                    ReminderSendSuccessWarningPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    ReminderSendSuccessCriticalPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoShowAlertThresholdConfigurations", x => x.Id);
                    table.CheckConstraint("CK_NoShowAlertThreshold_HighRiskQueue_Ordered", "[HighRiskQueueWarningCount] <= [HighRiskQueueCriticalCount]");
                    table.CheckConstraint("CK_NoShowAlertThreshold_HighRiskQueue_Range", "[HighRiskQueueWarningCount] BETWEEN 0 AND 100000 AND [HighRiskQueueCriticalCount] BETWEEN 0 AND 100000");
                    table.CheckConstraint("CK_NoShowAlertThreshold_NoShowRate_Ordered", "[NoShowRateWarningPercent] <= [NoShowRateCriticalPercent]");
                    table.CheckConstraint("CK_NoShowAlertThreshold_NoShowRate_Range", "[NoShowRateWarningPercent] BETWEEN 0 AND 100 AND [NoShowRateCriticalPercent] BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_NoShowAlertThreshold_ReminderSuccess_Ordered", "[ReminderSendSuccessCriticalPercent] <= [ReminderSendSuccessWarningPercent]");
                    table.CheckConstraint("CK_NoShowAlertThreshold_ReminderSuccess_Range", "[ReminderSendSuccessWarningPercent] BETWEEN 0 AND 100 AND [ReminderSendSuccessCriticalPercent] BETWEEN 0 AND 100");
                });

            migrationBuilder.CreateIndex(
                name: "IX_NoShowAlertThresholdConfigurations_IsActive",
                table: "NoShowAlertThresholdConfigurations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_NoShowAlertThresholdConfigurations_IsActive_UpdatedAt",
                table: "NoShowAlertThresholdConfigurations",
                columns: new[] { "IsActive", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NoShowAlertThresholdConfigurations");
        }
    }
}
