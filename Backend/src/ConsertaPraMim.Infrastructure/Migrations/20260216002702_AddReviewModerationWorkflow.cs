using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewModerationWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ModeratedAtUtc",
                table: "Reviews",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModeratedByAdminId",
                table: "Reviews",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModerationReason",
                table: "Reviews",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModerationStatus",
                table: "Reviews",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ReportReason",
                table: "Reviews",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReportedAtUtc",
                table: "Reviews",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReportedByUserId",
                table: "Reviews",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ModerationStatus_ReportedAtUtc",
                table: "Reviews",
                columns: new[] { "ModerationStatus", "ReportedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_ModerationStatus_ReportedAtUtc",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ModeratedAtUtc",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ModeratedByAdminId",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ModerationReason",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ModerationStatus",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ReportReason",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ReportedAtUtc",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ReportedByUserId",
                table: "Reviews");
        }
    }
}
