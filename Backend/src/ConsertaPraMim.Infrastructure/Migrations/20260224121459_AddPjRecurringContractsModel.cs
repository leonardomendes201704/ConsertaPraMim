using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPjRecurringContractsModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PjRecurringContracts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientPjType = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    ProviderEligibility = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Cadence = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    MonthlyAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IncludedVisitsPerCycle = table.Column<int>(type: "int", nullable: false),
                    ResponseSlaHours = table.Column<int>(type: "int", nullable: false),
                    OperationalWindowStartMinute = table.Column<int>(type: "int", nullable: false),
                    OperationalWindowEndMinute = table.Column<int>(type: "int", nullable: false),
                    OperationalDaysMask = table.Column<int>(type: "int", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextRenewalAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRenewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastPaymentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AutoRenew = table.Column<bool>(type: "bit", nullable: false),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PjRecurringContracts", x => x.Id);
                    table.CheckConstraint("CK_PjRecurringContracts_Cadence_Valid", "[Cadence] IN (1,2,3,4,5,6)");
                    table.CheckConstraint("CK_PjRecurringContracts_ClientPjType_Valid", "[ClientPjType] IN (1,2,3,4,5,6,7,8,9,10,99)");
                    table.CheckConstraint("CK_PjRecurringContracts_DaysMask_Valid", "[OperationalDaysMask] >= 1 AND [OperationalDaysMask] <= 127");
                    table.CheckConstraint("CK_PjRecurringContracts_MonthlyAmount_NonNegative", "[MonthlyAmount] >= 0");
                    table.CheckConstraint("CK_PjRecurringContracts_NextRenewal_Gte_Start", "[NextRenewalAtUtc] >= [StartsAtUtc]");
                    table.CheckConstraint("CK_PjRecurringContracts_ProviderEligibility_Valid", "[ProviderEligibility] IN (0,1,2)");
                    table.CheckConstraint("CK_PjRecurringContracts_ResponseSlaHours_Range", "[ResponseSlaHours] >= 1 AND [ResponseSlaHours] <= 168");
                    table.CheckConstraint("CK_PjRecurringContracts_Status_Valid", "[Status] IN (1,2,3,4,5,6)");
                    table.CheckConstraint("CK_PjRecurringContracts_Visits_Positive", "[IncludedVisitsPerCycle] > 0");
                    table.CheckConstraint("CK_PjRecurringContracts_Window_Valid", "[OperationalWindowEndMinute] > [OperationalWindowStartMinute]");
                    table.CheckConstraint("CK_PjRecurringContracts_WindowEnd_Range", "[OperationalWindowEndMinute] >= 1 AND [OperationalWindowEndMinute] <= 1440");
                    table.CheckConstraint("CK_PjRecurringContracts_WindowStart_Range", "[OperationalWindowStartMinute] >= 0 AND [OperationalWindowStartMinute] <= 1439");
                    table.ForeignKey(
                        name: "FK_PjRecurringContracts_Users_ClientUserId",
                        column: x => x.ClientUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PjRecurringContracts_ClientUserId_Status_Category",
                table: "PjRecurringContracts",
                columns: new[] { "ClientUserId", "Status", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_PjRecurringContracts_Status_NextRenewalAtUtc",
                table: "PjRecurringContracts",
                columns: new[] { "Status", "NextRenewalAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PjRecurringContracts");
        }
    }
}
