using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLandingAccessEventsAnalytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VisitorId",
                table: "LandingLeads",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LandingAccessEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VisitorId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CurrentUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Path = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    Host = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Scheme = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    InitialLeadOrigin = table.Column<int>(type: "int", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ForwardedFor = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    AcceptLanguage = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RefererUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandingAccessEvents", x => x.Id);
                    table.CheckConstraint("CK_LandingAccessEvents_InitialLeadOrigin_Valid", "[InitialLeadOrigin] IS NULL OR [InitialLeadOrigin] IN (1,2)");
                    table.CheckConstraint("CK_LandingAccessEvents_VisitorId_NotEmpty", "LEN([VisitorId]) > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_LandingLeads_VisitorId_CreatedAt",
                table: "LandingLeads",
                columns: new[] { "VisitorId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LandingAccessEvents_CreatedAt",
                table: "LandingAccessEvents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LandingAccessEvents_InitialLeadOrigin_CreatedAt",
                table: "LandingAccessEvents",
                columns: new[] { "InitialLeadOrigin", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LandingAccessEvents_Path_CreatedAt",
                table: "LandingAccessEvents",
                columns: new[] { "Path", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LandingAccessEvents_VisitorId_CreatedAt",
                table: "LandingAccessEvents",
                columns: new[] { "VisitorId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LandingAccessEvents");

            migrationBuilder.DropIndex(
                name: "IX_LandingLeads_VisitorId_CreatedAt",
                table: "LandingLeads");

            migrationBuilder.DropColumn(
                name: "VisitorId",
                table: "LandingLeads");
        }
    }
}
