using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLandingAnalyticsTelemetryAndGeoIp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SessionId",
                table: "LandingLeads",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeoCity",
                table: "LandingAccessEvents",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeoCountry",
                table: "LandingAccessEvents",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeoCountryCode",
                table: "LandingAccessEvents",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeoLookupStatus",
                table: "LandingAccessEvents",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeoProvider",
                table: "LandingAccessEvents",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeoRegion",
                table: "LandingAccessEvents",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeoRegionCode",
                table: "LandingAccessEvents",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SessionId",
                table: "LandingAccessEvents",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LandingTelemetryEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VisitorId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SessionId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CurrentUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Path = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    Host = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Scheme = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    InitialLeadOrigin = table.Column<int>(type: "int", nullable: true),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActiveSeconds = table.Column<int>(type: "int", nullable: true),
                    ScrollDepthPercent = table.Column<int>(type: "int", nullable: true),
                    ClickXPercent = table.Column<double>(type: "float", nullable: true),
                    ClickYPercent = table.Column<double>(type: "float", nullable: true),
                    HeatmapRow = table.Column<int>(type: "int", nullable: true),
                    HeatmapColumn = table.Column<int>(type: "int", nullable: true),
                    ElementKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    ElementLabel = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: true),
                    ElementHref = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BrowserLanguage = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ViewportWidth = table.Column<int>(type: "int", nullable: true),
                    ViewportHeight = table.Column<int>(type: "int", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ForwardedFor = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    AcceptLanguage = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandingTelemetryEvents", x => x.Id);
                    table.CheckConstraint("CK_LandingTelemetryEvents_ActiveSeconds_Range", "[ActiveSeconds] IS NULL OR ([ActiveSeconds] >= 0 AND [ActiveSeconds] <= 300)");
                    table.CheckConstraint("CK_LandingTelemetryEvents_ClickXPercent_Range", "[ClickXPercent] IS NULL OR ([ClickXPercent] >= 0 AND [ClickXPercent] <= 100)");
                    table.CheckConstraint("CK_LandingTelemetryEvents_ClickYPercent_Range", "[ClickYPercent] IS NULL OR ([ClickYPercent] >= 0 AND [ClickYPercent] <= 100)");
                    table.CheckConstraint("CK_LandingTelemetryEvents_EventType_Valid", "[EventType] IN (1,2,3,4,5)");
                    table.CheckConstraint("CK_LandingTelemetryEvents_ScrollDepth_Range", "[ScrollDepthPercent] IS NULL OR ([ScrollDepthPercent] >= 0 AND [ScrollDepthPercent] <= 100)");
                    table.CheckConstraint("CK_LandingTelemetryEvents_SessionId_NotEmpty", "LEN([SessionId]) > 0");
                    table.CheckConstraint("CK_LandingTelemetryEvents_VisitorId_NotEmpty", "LEN([VisitorId]) > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_LandingLeads_SessionId_CreatedAt",
                table: "LandingLeads",
                columns: new[] { "SessionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LandingAccessEvents_GeoCity_CreatedAt",
                table: "LandingAccessEvents",
                columns: new[] { "GeoCity", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LandingAccessEvents_GeoCountryCode_CreatedAt",
                table: "LandingAccessEvents",
                columns: new[] { "GeoCountryCode", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LandingAccessEvents_GeoRegionCode_CreatedAt",
                table: "LandingAccessEvents",
                columns: new[] { "GeoRegionCode", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LandingAccessEvents_SessionId_CreatedAt",
                table: "LandingAccessEvents",
                columns: new[] { "SessionId", "CreatedAt" });

            migrationBuilder.Sql(
                """
                UPDATE [LandingAccessEvents]
                SET [SessionId] = CONCAT(N'legacy-', CONVERT(nvarchar(36), [Id]))
                WHERE [SessionId] IS NULL OR LTRIM(RTRIM([SessionId])) = N'';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "SessionId",
                table: "LandingAccessEvents",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_LandingAccessEvents_SessionId_NotEmpty",
                table: "LandingAccessEvents",
                sql: "LEN([SessionId]) > 0");

            migrationBuilder.CreateIndex(
                name: "IX_LandingTelemetryEvents_EventType_OccurredAtUtc",
                table: "LandingTelemetryEvents",
                columns: new[] { "EventType", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LandingTelemetryEvents_HeatmapRow_HeatmapColumn_OccurredAtUtc",
                table: "LandingTelemetryEvents",
                columns: new[] { "HeatmapRow", "HeatmapColumn", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LandingTelemetryEvents_OccurredAtUtc",
                table: "LandingTelemetryEvents",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_LandingTelemetryEvents_SessionId_OccurredAtUtc",
                table: "LandingTelemetryEvents",
                columns: new[] { "SessionId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LandingTelemetryEvents");

            migrationBuilder.DropIndex(
                name: "IX_LandingLeads_SessionId_CreatedAt",
                table: "LandingLeads");

            migrationBuilder.DropIndex(
                name: "IX_LandingAccessEvents_GeoCity_CreatedAt",
                table: "LandingAccessEvents");

            migrationBuilder.DropIndex(
                name: "IX_LandingAccessEvents_GeoCountryCode_CreatedAt",
                table: "LandingAccessEvents");

            migrationBuilder.DropIndex(
                name: "IX_LandingAccessEvents_GeoRegionCode_CreatedAt",
                table: "LandingAccessEvents");

            migrationBuilder.DropIndex(
                name: "IX_LandingAccessEvents_SessionId_CreatedAt",
                table: "LandingAccessEvents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LandingAccessEvents_SessionId_NotEmpty",
                table: "LandingAccessEvents");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "LandingLeads");

            migrationBuilder.DropColumn(
                name: "GeoCity",
                table: "LandingAccessEvents");

            migrationBuilder.DropColumn(
                name: "GeoCountry",
                table: "LandingAccessEvents");

            migrationBuilder.DropColumn(
                name: "GeoCountryCode",
                table: "LandingAccessEvents");

            migrationBuilder.DropColumn(
                name: "GeoLookupStatus",
                table: "LandingAccessEvents");

            migrationBuilder.DropColumn(
                name: "GeoProvider",
                table: "LandingAccessEvents");

            migrationBuilder.DropColumn(
                name: "GeoRegion",
                table: "LandingAccessEvents");

            migrationBuilder.DropColumn(
                name: "GeoRegionCode",
                table: "LandingAccessEvents");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "LandingAccessEvents");
        }
    }
}
