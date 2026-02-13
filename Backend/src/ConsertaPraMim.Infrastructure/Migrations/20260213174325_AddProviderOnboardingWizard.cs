using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderOnboardingWizard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DocumentsSubmittedAt",
                table: "ProviderProfiles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOnboardingCompleted",
                table: "ProviderProfiles",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OnboardingCompletedAt",
                table: "ProviderProfiles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OnboardingStartedAt",
                table: "ProviderProfiles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OnboardingStatus",
                table: "ProviderProfiles",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlanSelectedAt",
                table: "ProviderProfiles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProviderOnboardingDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MimeType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    FileUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileHashSha256 = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderOnboardingDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderOnboardingDocuments_ProviderProfiles_ProviderProfileId",
                        column: x => x.ProviderProfileId,
                        principalTable: "ProviderProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderOnboardingDocuments_ProviderProfileId_DocumentType",
                table: "ProviderOnboardingDocuments",
                columns: new[] { "ProviderProfileId", "DocumentType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProviderOnboardingDocuments");

            migrationBuilder.DropColumn(
                name: "DocumentsSubmittedAt",
                table: "ProviderProfiles");

            migrationBuilder.DropColumn(
                name: "IsOnboardingCompleted",
                table: "ProviderProfiles");

            migrationBuilder.DropColumn(
                name: "OnboardingCompletedAt",
                table: "ProviderProfiles");

            migrationBuilder.DropColumn(
                name: "OnboardingStartedAt",
                table: "ProviderProfiles");

            migrationBuilder.DropColumn(
                name: "OnboardingStatus",
                table: "ProviderProfiles");

            migrationBuilder.DropColumn(
                name: "PlanSelectedAt",
                table: "ProviderProfiles");
        }
    }
}
