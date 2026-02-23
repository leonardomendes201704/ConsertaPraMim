using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientPfPjAndProviderClientPreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClientPjType",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClientProfileType",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ClientPreference",
                table: "ProviderProfiles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_ClientProfileType_Valid",
                table: "Users",
                sql: "[ClientProfileType] IN (1,2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProviderProfiles_ClientPreference_Valid",
                table: "ProviderProfiles",
                sql: "[ClientPreference] IN (0,1,2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_ClientProfileType_Valid",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProviderProfiles_ClientPreference_Valid",
                table: "ProviderProfiles");

            migrationBuilder.DropColumn(
                name: "ClientPjType",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ClientProfileType",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ClientPreference",
                table: "ProviderProfiles");
        }
    }
}
