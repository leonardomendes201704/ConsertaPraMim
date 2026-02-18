using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMobilePushDevicesAndFirebaseDispatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MobilePushDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Platform = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AppKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DeviceModel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OsVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AppVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastRegisteredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastDeliveredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastFailureAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastFailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MobilePushDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MobilePushDevices_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MobilePushDevices_Token_AppKind",
                table: "MobilePushDevices",
                columns: new[] { "Token", "AppKind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MobilePushDevices_UserId_DeviceId_AppKind",
                table: "MobilePushDevices",
                columns: new[] { "UserId", "DeviceId", "AppKind" });

            migrationBuilder.CreateIndex(
                name: "IX_MobilePushDevices_UserId_IsActive_AppKind",
                table: "MobilePushDevices",
                columns: new[] { "UserId", "IsActive", "AppKind" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MobilePushDevices");
        }
    }
}
