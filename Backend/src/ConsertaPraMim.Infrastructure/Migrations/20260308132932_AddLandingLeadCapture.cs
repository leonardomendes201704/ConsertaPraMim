using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLandingLeadCapture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LandingLeads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Origin = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    City = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    State = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Neighborhood = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ServiceCategory = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    RequestedService = table.Column<string>(type: "nvarchar(220)", maxLength: 220, nullable: true),
                    CompanyName = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: true),
                    CompanyDocument = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    YearsOfExperience = table.Column<int>(type: "int", nullable: true),
                    Message = table.Column<string>(type: "nvarchar(1600)", maxLength: 1600, nullable: true),
                    CurrentPageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReferrerUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Host = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Scheme = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Path = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    QueryString = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UtmSource = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: true),
                    UtmMedium = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: true),
                    UtmCampaign = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: true),
                    UtmTerm = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: true),
                    UtmContent = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ForwardedFor = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    AcceptLanguage = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    BrowserLanguage = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ScreenResolution = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    DevicePlatform = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    TimeZone = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandingLeads", x => x.Id);
                    table.CheckConstraint("CK_LandingLeads_Origin_Valid", "[Origin] IN (1,2)");
                    table.CheckConstraint("CK_LandingLeads_YearsOfExperience_Range", "[YearsOfExperience] IS NULL OR ([YearsOfExperience] >= 0 AND [YearsOfExperience] <= 60)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_LandingLeads_City_State_CreatedAt",
                table: "LandingLeads",
                columns: new[] { "City", "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LandingLeads_Origin_CreatedAt",
                table: "LandingLeads",
                columns: new[] { "Origin", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LandingLeads_UtmCampaign_CreatedAt",
                table: "LandingLeads",
                columns: new[] { "UtmCampaign", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LandingLeads");
        }
    }
}
