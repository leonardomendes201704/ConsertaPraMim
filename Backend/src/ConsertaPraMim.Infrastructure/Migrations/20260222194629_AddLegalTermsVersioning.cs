using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLegalTermsVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LegalTermsDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Audience = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    HtmlContent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChangeSummary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalTermsDocuments", x => x.Id);
                    table.CheckConstraint("CK_LegalTermsDocuments_Version_Positive", "[Version] > 0");
                });

            migrationBuilder.CreateTable(
                name: "UserLegalTermsAcceptances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegalTermsDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Audience = table.Column<int>(type: "int", nullable: false),
                    TermsVersion = table.Column<int>(type: "int", nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLegalTermsAcceptances", x => x.Id);
                    table.CheckConstraint("CK_UserLegalTermsAcceptances_TermsVersion_Positive", "[TermsVersion] > 0");
                    table.ForeignKey(
                        name: "FK_UserLegalTermsAcceptances_LegalTermsDocuments_LegalTermsDocumentId",
                        column: x => x.LegalTermsDocumentId,
                        principalTable: "LegalTermsDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserLegalTermsAcceptances_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LegalTermsDocuments_Audience_IsPublished_PublishedAtUtc",
                table: "LegalTermsDocuments",
                columns: new[] { "Audience", "IsPublished", "PublishedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LegalTermsDocuments_Audience_Version",
                table: "LegalTermsDocuments",
                columns: new[] { "Audience", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLegalTermsAcceptances_Audience_TermsVersion_AcceptedAtUtc",
                table: "UserLegalTermsAcceptances",
                columns: new[] { "Audience", "TermsVersion", "AcceptedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserLegalTermsAcceptances_LegalTermsDocumentId",
                table: "UserLegalTermsAcceptances",
                column: "LegalTermsDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLegalTermsAcceptances_UserId_Audience_TermsVersion",
                table: "UserLegalTermsAcceptances",
                columns: new[] { "UserId", "Audience", "TermsVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserLegalTermsAcceptances");

            migrationBuilder.DropTable(
                name: "LegalTermsDocuments");
        }
    }
}
