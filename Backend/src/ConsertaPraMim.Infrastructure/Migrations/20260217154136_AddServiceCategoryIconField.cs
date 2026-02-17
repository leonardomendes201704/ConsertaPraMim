using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceCategoryIconField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Icon",
                table: "ServiceCategoryDefinitions",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "build_circle");

            migrationBuilder.Sql("""
                UPDATE [ServiceCategoryDefinitions]
                SET [Icon] = CASE [LegacyCategory]
                    WHEN 0 THEN N'bolt'
                    WHEN 1 THEN N'water_drop'
                    WHEN 2 THEN N'memory'
                    WHEN 3 THEN N'kitchen'
                    WHEN 4 THEN N'foundation'
                    WHEN 5 THEN N'cleaning_services'
                    ELSE N'build_circle'
                END
                WHERE [Icon] = N'build_circle';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Icon",
                table: "ServiceCategoryDefinitions");
        }
    }
}
