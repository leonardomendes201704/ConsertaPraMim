using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderTrustReviewTrail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RiskLevel",
                table: "ProviderProfiles",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TrustStatus",
                table: "ProviderProfiles",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "TrustStatusReason",
                table: "ProviderProfiles",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TrustStatusUpdatedAtUtc",
                table: "ProviderProfiles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProviderTrustReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousTrustStatus = table.Column<int>(type: "int", nullable: false),
                    NewTrustStatus = table.Column<int>(type: "int", nullable: false),
                    PreviousRiskLevel = table.Column<int>(type: "int", nullable: false),
                    NewRiskLevel = table.Column<int>(type: "int", nullable: false),
                    DecisionReason = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    EvidenceSummary = table.Column<string>(type: "nvarchar(1200)", maxLength: 1200, nullable: true),
                    ReviewedByAdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewedByAdminEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderTrustReviews", x => x.Id);
                    table.CheckConstraint("CK_ProviderTrustReviews_NewRiskLevel_Valid", "[NewRiskLevel] IN (1,2,3)");
                    table.CheckConstraint("CK_ProviderTrustReviews_NewTrustStatus_Valid", "[NewTrustStatus] IN (1,2,3)");
                    table.CheckConstraint("CK_ProviderTrustReviews_PreviousRiskLevel_Valid", "[PreviousRiskLevel] IN (1,2,3)");
                    table.CheckConstraint("CK_ProviderTrustReviews_PreviousTrustStatus_Valid", "[PreviousTrustStatus] IN (1,2,3)");
                    table.ForeignKey(
                        name: "FK_ProviderTrustReviews_ProviderProfiles_ProviderProfileId",
                        column: x => x.ProviderProfileId,
                        principalTable: "ProviderProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProviderTrustReviews_Users_ProviderUserId",
                        column: x => x.ProviderUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderProfiles_TrustStatus_RiskLevel_TrustStatusUpdatedAtUtc",
                table: "ProviderProfiles",
                columns: new[] { "TrustStatus", "RiskLevel", "TrustStatusUpdatedAtUtc" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProviderProfiles_RiskLevel_Valid",
                table: "ProviderProfiles",
                sql: "[RiskLevel] IN (1,2,3)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProviderProfiles_TrustStatus_Valid",
                table: "ProviderProfiles",
                sql: "[TrustStatus] IN (1,2,3)");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderTrustReviews_NewTrustStatus_NewRiskLevel_ReviewedAtUtc",
                table: "ProviderTrustReviews",
                columns: new[] { "NewTrustStatus", "NewRiskLevel", "ReviewedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderTrustReviews_ProviderProfileId",
                table: "ProviderTrustReviews",
                column: "ProviderProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderTrustReviews_ProviderUserId_ReviewedAtUtc",
                table: "ProviderTrustReviews",
                columns: new[] { "ProviderUserId", "ReviewedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProviderTrustReviews");

            migrationBuilder.DropIndex(
                name: "IX_ProviderProfiles_TrustStatus_RiskLevel_TrustStatusUpdatedAtUtc",
                table: "ProviderProfiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProviderProfiles_RiskLevel_Valid",
                table: "ProviderProfiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProviderProfiles_TrustStatus_Valid",
                table: "ProviderProfiles");

            migrationBuilder.DropColumn(
                name: "RiskLevel",
                table: "ProviderProfiles");

            migrationBuilder.DropColumn(
                name: "TrustStatus",
                table: "ProviderProfiles");

            migrationBuilder.DropColumn(
                name: "TrustStatusReason",
                table: "ProviderProfiles");

            migrationBuilder.DropColumn(
                name: "TrustStatusUpdatedAtUtc",
                table: "ProviderProfiles");
        }
    }
}
