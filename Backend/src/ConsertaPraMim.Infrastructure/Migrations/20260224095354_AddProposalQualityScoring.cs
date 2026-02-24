using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProposalQualityScoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Proposals_RequestId",
                table: "Proposals");

            migrationBuilder.AddColumn<DateTime>(
                name: "QualityCalculatedAtUtc",
                table: "Proposals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QualityClarityScore",
                table: "Proposals",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QualityCommercialScore",
                table: "Proposals",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QualityCompletenessScore",
                table: "Proposals",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QualityHistoryScore",
                table: "Proposals",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QualityScore",
                table: "Proposals",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_RequestId_QualityScore_CreatedAt",
                table: "Proposals",
                columns: new[] { "RequestId", "QualityScore", "CreatedAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Proposals_QualityScores_Range",
                table: "Proposals",
                sql: "([QualityScore] IS NULL OR ([QualityScore] >= 0 AND [QualityScore] <= 100)) AND ([QualityCompletenessScore] IS NULL OR ([QualityCompletenessScore] >= 0 AND [QualityCompletenessScore] <= 100)) AND ([QualityClarityScore] IS NULL OR ([QualityClarityScore] >= 0 AND [QualityClarityScore] <= 100)) AND ([QualityHistoryScore] IS NULL OR ([QualityHistoryScore] >= 0 AND [QualityHistoryScore] <= 100)) AND ([QualityCommercialScore] IS NULL OR ([QualityCommercialScore] >= 0 AND [QualityCommercialScore] <= 100))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Proposals_RequestId_QualityScore_CreatedAt",
                table: "Proposals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Proposals_QualityScores_Range",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "QualityCalculatedAtUtc",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "QualityClarityScore",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "QualityCommercialScore",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "QualityCompletenessScore",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "QualityHistoryScore",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "QualityScore",
                table: "Proposals");

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_RequestId",
                table: "Proposals",
                column: "RequestId");
        }
    }
}
