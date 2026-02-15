using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceAppointmentNoShowRiskPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceAppointmentNoShowRiskPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LookbackDays = table.Column<int>(type: "int", nullable: false),
                    MaxHistoryEventsPerActor = table.Column<int>(type: "int", nullable: false),
                    WeightClientNotConfirmed = table.Column<int>(type: "int", nullable: false),
                    WeightProviderNotConfirmed = table.Column<int>(type: "int", nullable: false),
                    WeightBothNotConfirmedBonus = table.Column<int>(type: "int", nullable: false),
                    WeightWindowWithin24Hours = table.Column<int>(type: "int", nullable: false),
                    WeightWindowWithin6Hours = table.Column<int>(type: "int", nullable: false),
                    WeightWindowWithin2Hours = table.Column<int>(type: "int", nullable: false),
                    WeightClientHistoryRisk = table.Column<int>(type: "int", nullable: false),
                    WeightProviderHistoryRisk = table.Column<int>(type: "int", nullable: false),
                    LowThresholdScore = table.Column<int>(type: "int", nullable: false),
                    MediumThresholdScore = table.Column<int>(type: "int", nullable: false),
                    HighThresholdScore = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceAppointmentNoShowRiskPolicies", x => x.Id);
                    table.CheckConstraint("CK_NoShowRiskPolicy_LookbackDays_Range", "[LookbackDays] BETWEEN 1 AND 365");
                    table.CheckConstraint("CK_NoShowRiskPolicy_MaxHistoryEventsPerActor_Range", "[MaxHistoryEventsPerActor] BETWEEN 1 AND 200");
                    table.CheckConstraint("CK_NoShowRiskPolicy_Thresholds_Ordered", "[LowThresholdScore] >= 0 AND [LowThresholdScore] <= [MediumThresholdScore] AND [MediumThresholdScore] <= [HighThresholdScore] AND [HighThresholdScore] <= 100");
                    table.CheckConstraint("CK_NoShowRiskPolicy_WeightBothNotConfirmedBonus_Range", "[WeightBothNotConfirmedBonus] BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_NoShowRiskPolicy_WeightClientHistoryRisk_Range", "[WeightClientHistoryRisk] BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_NoShowRiskPolicy_WeightClientNotConfirmed_Range", "[WeightClientNotConfirmed] BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_NoShowRiskPolicy_WeightProviderHistoryRisk_Range", "[WeightProviderHistoryRisk] BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_NoShowRiskPolicy_WeightProviderNotConfirmed_Range", "[WeightProviderNotConfirmed] BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_NoShowRiskPolicy_WeightWindowWithin24Hours_Range", "[WeightWindowWithin24Hours] BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_NoShowRiskPolicy_WeightWindowWithin2Hours_Range", "[WeightWindowWithin2Hours] BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_NoShowRiskPolicy_WeightWindowWithin6Hours_Range", "[WeightWindowWithin6Hours] BETWEEN 0 AND 100");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAppointmentNoShowRiskPolicies_IsActive",
                table: "ServiceAppointmentNoShowRiskPolicies",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAppointmentNoShowRiskPolicies_IsActive_UpdatedAt",
                table: "ServiceAppointmentNoShowRiskPolicies",
                columns: new[] { "IsActive", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceAppointmentNoShowRiskPolicies");
        }
    }
}
