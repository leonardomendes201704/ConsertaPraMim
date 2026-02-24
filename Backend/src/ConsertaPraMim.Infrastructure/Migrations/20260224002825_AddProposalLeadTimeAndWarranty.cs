using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProposalLeadTimeAndWarranty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EstimatedLeadTimeHours",
                table: "Proposals",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WarrantyDays",
                table: "Proposals",
                type: "int",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Proposals_EstimatedLeadTimeHours_Range",
                table: "Proposals",
                sql: "[EstimatedLeadTimeHours] IS NULL OR ([EstimatedLeadTimeHours] >= 1 AND [EstimatedLeadTimeHours] <= 720)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Proposals_WarrantyDays_Range",
                table: "Proposals",
                sql: "[WarrantyDays] IS NULL OR ([WarrantyDays] >= 0 AND [WarrantyDays] <= 3650)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Proposals_EstimatedLeadTimeHours_Range",
                table: "Proposals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Proposals_WarrantyDays_Range",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "EstimatedLeadTimeHours",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "WarrantyDays",
                table: "Proposals");
        }
    }
}
