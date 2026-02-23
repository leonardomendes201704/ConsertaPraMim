using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientBaseLocationToUserProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientBaseCity",
                table: "Users",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ClientBaseLatitude",
                table: "Users",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ClientBaseLongitude",
                table: "Users",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientBaseStreet",
                table: "Users",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientBaseZipCode",
                table: "Users",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_ClientBaseCoordinates_Pair",
                table: "Users",
                sql: "([ClientBaseLatitude] IS NULL AND [ClientBaseLongitude] IS NULL) OR ([ClientBaseLatitude] IS NOT NULL AND [ClientBaseLongitude] IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_ClientBaseCoordinates_Pair",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ClientBaseCity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ClientBaseLatitude",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ClientBaseLongitude",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ClientBaseStreet",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ClientBaseZipCode",
                table: "Users");
        }
    }
}
