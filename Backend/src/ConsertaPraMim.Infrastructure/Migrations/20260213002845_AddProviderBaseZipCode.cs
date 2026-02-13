using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderBaseZipCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BaseZipCode",
                table: "ProviderProfiles",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaseZipCode",
                table: "ProviderProfiles");
        }
    }
}
