using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "BaseLatitude",
                table: "ProviderProfiles",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "BaseLongitude",
                table: "ProviderProfiles",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaseLatitude",
                table: "ProviderProfiles");

            migrationBuilder.DropColumn(
                name: "BaseLongitude",
                table: "ProviderProfiles");
        }
    }
}
