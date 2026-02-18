using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApiMonitoringTelemetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiEndpointMetricsDaily",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BucketDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    EndpointTemplate = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    RequestCount = table.Column<long>(type: "bigint", nullable: false),
                    ErrorCount = table.Column<long>(type: "bigint", nullable: false),
                    WarningCount = table.Column<long>(type: "bigint", nullable: false),
                    TotalDurationMs = table.Column<long>(type: "bigint", nullable: false),
                    MinDurationMs = table.Column<int>(type: "int", nullable: false),
                    MaxDurationMs = table.Column<int>(type: "int", nullable: false),
                    P50DurationMs = table.Column<int>(type: "int", nullable: false),
                    P95DurationMs = table.Column<int>(type: "int", nullable: false),
                    P99DurationMs = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiEndpointMetricsDaily", x => x.Id);
                    table.CheckConstraint("CK_ApiEndpointMetricDaily_Duration_NonNegative", "[TotalDurationMs] >= 0 AND [MinDurationMs] >= 0 AND [MaxDurationMs] >= 0");
                    table.CheckConstraint("CK_ApiEndpointMetricDaily_ErrorCount_NonNegative", "[ErrorCount] >= 0");
                    table.CheckConstraint("CK_ApiEndpointMetricDaily_RequestCount_NonNegative", "[RequestCount] >= 0");
                    table.CheckConstraint("CK_ApiEndpointMetricDaily_WarningCount_NonNegative", "[WarningCount] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "ApiEndpointMetricsHourly",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BucketStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    EndpointTemplate = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    RequestCount = table.Column<long>(type: "bigint", nullable: false),
                    ErrorCount = table.Column<long>(type: "bigint", nullable: false),
                    WarningCount = table.Column<long>(type: "bigint", nullable: false),
                    TotalDurationMs = table.Column<long>(type: "bigint", nullable: false),
                    MinDurationMs = table.Column<int>(type: "int", nullable: false),
                    MaxDurationMs = table.Column<int>(type: "int", nullable: false),
                    P50DurationMs = table.Column<int>(type: "int", nullable: false),
                    P95DurationMs = table.Column<int>(type: "int", nullable: false),
                    P99DurationMs = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiEndpointMetricsHourly", x => x.Id);
                    table.CheckConstraint("CK_ApiEndpointMetricHourly_Duration_NonNegative", "[TotalDurationMs] >= 0 AND [MinDurationMs] >= 0 AND [MaxDurationMs] >= 0");
                    table.CheckConstraint("CK_ApiEndpointMetricHourly_ErrorCount_NonNegative", "[ErrorCount] >= 0");
                    table.CheckConstraint("CK_ApiEndpointMetricHourly_RequestCount_NonNegative", "[RequestCount] >= 0");
                    table.CheckConstraint("CK_ApiEndpointMetricHourly_WarningCount_NonNegative", "[WarningCount] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "ApiErrorCatalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ErrorKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ErrorType = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    NormalizedMessage = table.Column<string>(type: "nvarchar(1600)", maxLength: 1600, nullable: false),
                    FirstSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiErrorCatalog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApiRequestLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    TraceId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Method = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    EndpointTemplate = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Path = table.Column<string>(type: "nvarchar(700)", maxLength: 700, nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    IsError = table.Column<bool>(type: "bit", nullable: false),
                    WarningCount = table.Column<int>(type: "int", nullable: false),
                    WarningCodesJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ErrorType = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true),
                    NormalizedErrorMessage = table.Column<string>(type: "nvarchar(1600)", maxLength: 1600, nullable: true),
                    NormalizedErrorKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    IpHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    RequestSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    ResponseSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    Scheme = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Host = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiRequestLogs", x => x.Id);
                    table.CheckConstraint("CK_ApiRequestLogs_DurationMs_NonNegative", "[DurationMs] >= 0");
                    table.CheckConstraint("CK_ApiRequestLogs_StatusCode_Valid", "[StatusCode] >= 100 AND [StatusCode] <= 599");
                    table.CheckConstraint("CK_ApiRequestLogs_WarningCount_NonNegative", "[WarningCount] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "ApiErrorOccurrencesHourly",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ErrorCatalogId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BucketStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    EndpointTemplate = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    OccurrenceCount = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiErrorOccurrencesHourly", x => x.Id);
                    table.CheckConstraint("CK_ApiErrorOccurrenceHourly_OccurrenceCount_NonNegative", "[OccurrenceCount] >= 0");
                    table.ForeignKey(
                        name: "FK_ApiErrorOccurrencesHourly_ApiErrorCatalog_ErrorCatalogId",
                        column: x => x.ErrorCatalogId,
                        principalTable: "ApiErrorCatalog",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiEndpointMetricsDaily_BucketDateUtc_Method_EndpointTemplate_StatusCode_Severity_TenantId",
                table: "ApiEndpointMetricsDaily",
                columns: new[] { "BucketDateUtc", "Method", "EndpointTemplate", "StatusCode", "Severity", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiEndpointMetricsDaily_EndpointTemplate_Method_BucketDateUtc",
                table: "ApiEndpointMetricsDaily",
                columns: new[] { "EndpointTemplate", "Method", "BucketDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiEndpointMetricsHourly_BucketStartUtc_Method_EndpointTemplate_StatusCode_Severity_TenantId",
                table: "ApiEndpointMetricsHourly",
                columns: new[] { "BucketStartUtc", "Method", "EndpointTemplate", "StatusCode", "Severity", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiEndpointMetricsHourly_EndpointTemplate_Method_BucketStartUtc",
                table: "ApiEndpointMetricsHourly",
                columns: new[] { "EndpointTemplate", "Method", "BucketStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiErrorCatalog_ErrorKey",
                table: "ApiErrorCatalog",
                column: "ErrorKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiErrorCatalog_LastSeenUtc",
                table: "ApiErrorCatalog",
                column: "LastSeenUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ApiErrorOccurrencesHourly_BucketStartUtc_ErrorCatalogId_Method_EndpointTemplate_StatusCode_Severity_TenantId",
                table: "ApiErrorOccurrencesHourly",
                columns: new[] { "BucketStartUtc", "ErrorCatalogId", "Method", "EndpointTemplate", "StatusCode", "Severity", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiErrorOccurrencesHourly_EndpointTemplate_Method_BucketStartUtc",
                table: "ApiErrorOccurrencesHourly",
                columns: new[] { "EndpointTemplate", "Method", "BucketStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiErrorOccurrencesHourly_ErrorCatalogId",
                table: "ApiErrorOccurrencesHourly",
                column: "ErrorCatalogId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiRequestLogs_CorrelationId",
                table: "ApiRequestLogs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiRequestLogs_EndpointTemplate_Method_TimestampUtc",
                table: "ApiRequestLogs",
                columns: new[] { "EndpointTemplate", "Method", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiRequestLogs_NormalizedErrorKey_TimestampUtc",
                table: "ApiRequestLogs",
                columns: new[] { "NormalizedErrorKey", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiRequestLogs_Severity_TimestampUtc",
                table: "ApiRequestLogs",
                columns: new[] { "Severity", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiRequestLogs_StatusCode_TimestampUtc",
                table: "ApiRequestLogs",
                columns: new[] { "StatusCode", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiRequestLogs_TenantId_TimestampUtc",
                table: "ApiRequestLogs",
                columns: new[] { "TenantId", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiRequestLogs_TimestampUtc",
                table: "ApiRequestLogs",
                column: "TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ApiRequestLogs_UserId_TimestampUtc",
                table: "ApiRequestLogs",
                columns: new[] { "UserId", "TimestampUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiEndpointMetricsDaily");

            migrationBuilder.DropTable(
                name: "ApiEndpointMetricsHourly");

            migrationBuilder.DropTable(
                name: "ApiErrorOccurrencesHourly");

            migrationBuilder.DropTable(
                name: "ApiRequestLogs");

            migrationBuilder.DropTable(
                name: "ApiErrorCatalog");
        }
    }
}
