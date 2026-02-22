using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdjustSqliteCompatibilityForTextAndSystemSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SystemSettings_Key_NotEmpty",
                table: "SystemSettings");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SystemSettings_Key_NotEmpty",
                table: "SystemSettings",
                sql: "COALESCE([Key], '') <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SystemSettings_Key_NotEmpty",
                table: "SystemSettings");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SystemSettings_Key_NotEmpty",
                table: "SystemSettings",
                sql: "LEN([Key]) > 0");
        }
    }
}
