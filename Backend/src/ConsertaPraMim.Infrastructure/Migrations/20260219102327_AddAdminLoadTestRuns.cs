using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminLoadTestRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminLoadTestRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalRunId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Scenario = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BaseUrl = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DurationSeconds = table.Column<double>(type: "float", nullable: false),
                    TotalRequests = table.Column<long>(type: "bigint", nullable: false),
                    SuccessfulRequests = table.Column<long>(type: "bigint", nullable: false),
                    FailedRequests = table.Column<long>(type: "bigint", nullable: false),
                    ErrorRatePercent = table.Column<double>(type: "float", nullable: false),
                    RpsAvg = table.Column<double>(type: "float", nullable: false),
                    RpsPeak = table.Column<int>(type: "int", nullable: false),
                    MinLatencyMs = table.Column<double>(type: "float", nullable: false),
                    P50LatencyMs = table.Column<double>(type: "float", nullable: false),
                    P95LatencyMs = table.Column<double>(type: "float", nullable: false),
                    P99LatencyMs = table.Column<double>(type: "float", nullable: false),
                    MaxLatencyMs = table.Column<double>(type: "float", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RawReportJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminLoadTestRuns", x => x.Id);
                    table.CheckConstraint("CK_AdminLoadTestRuns_DurationSeconds_NonNegative", "[DurationSeconds] >= 0");
                    table.CheckConstraint("CK_AdminLoadTestRuns_Latency_NonNegative", "[MinLatencyMs] >= 0 AND [P50LatencyMs] >= 0 AND [P95LatencyMs] >= 0 AND [P99LatencyMs] >= 0 AND [MaxLatencyMs] >= 0");
                    table.CheckConstraint("CK_AdminLoadTestRuns_RequestCounts_NonNegative", "[TotalRequests] >= 0 AND [SuccessfulRequests] >= 0 AND [FailedRequests] >= 0");
                    table.CheckConstraint("CK_AdminLoadTestRuns_Rps_NonNegative", "[RpsAvg] >= 0 AND [RpsPeak] >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminLoadTestRuns_CreatedAt",
                table: "AdminLoadTestRuns",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AdminLoadTestRuns_ExternalRunId",
                table: "AdminLoadTestRuns",
                column: "ExternalRunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminLoadTestRuns_Scenario_StartedAtUtc",
                table: "AdminLoadTestRuns",
                columns: new[] { "Scenario", "StartedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminLoadTestRuns");
        }
    }
}
