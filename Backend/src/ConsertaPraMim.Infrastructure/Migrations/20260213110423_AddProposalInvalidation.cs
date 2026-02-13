using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProposalInvalidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "InvalidatedAt",
                table: "Proposals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InvalidatedByAdminId",
                table: "Proposals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvalidationReason",
                table: "Proposals",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsInvalidated",
                table: "Proposals",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InvalidatedAt",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "InvalidatedByAdminId",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "InvalidationReason",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "IsInvalidated",
                table: "Proposals");
        }
    }
}
