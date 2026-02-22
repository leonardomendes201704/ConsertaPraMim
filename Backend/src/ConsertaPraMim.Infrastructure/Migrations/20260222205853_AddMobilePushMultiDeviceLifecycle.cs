using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMobilePushMultiDeviceLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InstallationId",
                table: "MobilePushDevices",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeenAtUtc",
                table: "MobilePushDevices",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "RevokedAtUtc",
                table: "MobilePushDevices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                table: "MobilePushDevices",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            if (ActiveProvider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql(
                    "UPDATE [MobilePushDevices] SET [InstallationId] = CONCAT('legacy-', CONVERT(varchar(36), [Id])) WHERE [InstallationId] IS NULL OR LTRIM(RTRIM([InstallationId])) = '';");
                migrationBuilder.Sql(
                    "UPDATE [MobilePushDevices] SET [LastSeenAtUtc] = ISNULL([LastRegisteredAtUtc], SYSUTCDATETIME()) WHERE [LastSeenAtUtc] = '0001-01-01T00:00:00.0000000';");
            }
            else if (ActiveProvider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql(
                    "UPDATE \"MobilePushDevices\" SET \"InstallationId\" = 'legacy-' || \"Id\" WHERE \"InstallationId\" IS NULL OR TRIM(\"InstallationId\") = '';");
                migrationBuilder.Sql(
                    "UPDATE \"MobilePushDevices\" SET \"LastSeenAtUtc\" = COALESCE(\"LastRegisteredAtUtc\", CURRENT_TIMESTAMP) WHERE \"LastSeenAtUtc\" = '0001-01-01 00:00:00';");
            }

            migrationBuilder.AlterColumn<string>(
                name: "InstallationId",
                table: "MobilePushDevices",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MobilePushDevices_AppKind_InstallationId",
                table: "MobilePushDevices",
                columns: new[] { "AppKind", "InstallationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MobilePushDevices_AppKind_InstallationId",
                table: "MobilePushDevices");

            migrationBuilder.DropColumn(
                name: "InstallationId",
                table: "MobilePushDevices");

            migrationBuilder.DropColumn(
                name: "LastSeenAtUtc",
                table: "MobilePushDevices");

            migrationBuilder.DropColumn(
                name: "RevokedAtUtc",
                table: "MobilePushDevices");

            migrationBuilder.DropColumn(
                name: "TimeZone",
                table: "MobilePushDevices");
        }
    }
}
