using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProposalComparisonInteractionTelemetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProposalComparisonInteractions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProposalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SortBy = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ExperimentGroup = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalComparisonInteractions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalComparisonInteractions_ClientUserId_RequestId_CreatedAt",
                table: "ProposalComparisonInteractions",
                columns: new[] { "ClientUserId", "RequestId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalComparisonInteractions_ExperimentGroup_EventType_CreatedAt",
                table: "ProposalComparisonInteractions",
                columns: new[] { "ExperimentGroup", "EventType", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProposalComparisonInteractions");
        }
    }
}
