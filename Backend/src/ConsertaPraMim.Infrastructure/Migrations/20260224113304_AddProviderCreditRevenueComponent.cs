using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderCreditRevenueComponent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RevenueComponent",
                table: "ProviderCreditLedgerEntries",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProviderCreditLedgerEntries_RevenueComponent_Valid",
                table: "ProviderCreditLedgerEntries",
                sql: "[RevenueComponent] IN (1,2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProviderCreditLedgerEntries_RevenueComponent_Valid",
                table: "ProviderCreditLedgerEntries");

            migrationBuilder.DropColumn(
                name: "RevenueComponent",
                table: "ProviderCreditLedgerEntries");
        }
    }
}
