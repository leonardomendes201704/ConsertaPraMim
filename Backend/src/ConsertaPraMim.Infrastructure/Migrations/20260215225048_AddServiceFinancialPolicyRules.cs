using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceFinancialPolicyRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceFinancialPolicyRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    MinHoursBeforeWindowStart = table.Column<int>(type: "int", nullable: false),
                    MaxHoursBeforeWindowStart = table.Column<int>(type: "int", nullable: true),
                    PenaltyPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    CounterpartyCompensationPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    PlatformRetainedPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceFinancialPolicyRules", x => x.Id);
                    table.CheckConstraint("CK_ServiceFinancialPolicyRule_Hours_NonNegative", "[MinHoursBeforeWindowStart] >= 0 AND ([MaxHoursBeforeWindowStart] IS NULL OR [MaxHoursBeforeWindowStart] >= 0)");
                    table.CheckConstraint("CK_ServiceFinancialPolicyRule_Hours_Ordered", "[MaxHoursBeforeWindowStart] IS NULL OR [MinHoursBeforeWindowStart] <= [MaxHoursBeforeWindowStart]");
                    table.CheckConstraint("CK_ServiceFinancialPolicyRule_Percentages_Consistency", "([CounterpartyCompensationPercent] + [PlatformRetainedPercent]) <= [PenaltyPercent]");
                    table.CheckConstraint("CK_ServiceFinancialPolicyRule_Percentages_Range", "[PenaltyPercent] BETWEEN 0 AND 100 AND [CounterpartyCompensationPercent] BETWEEN 0 AND 100 AND [PlatformRetainedPercent] BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_ServiceFinancialPolicyRule_Priority_Positive", "[Priority] >= 1");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceFinancialPolicyRules_EventType_MinHoursBeforeWindowStart_MaxHoursBeforeWindowStart_Priority",
                table: "ServiceFinancialPolicyRules",
                columns: new[] { "EventType", "MinHoursBeforeWindowStart", "MaxHoursBeforeWindowStart", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceFinancialPolicyRules_IsActive_EventType_Priority",
                table: "ServiceFinancialPolicyRules",
                columns: new[] { "IsActive", "EventType", "Priority" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceFinancialPolicyRules");
        }
    }
}
