using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceAppointmentCalendarSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceAppointmentCalendarSyncs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoogleEventId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SyncStatus = table.Column<int>(type: "int", nullable: false),
                    LastSyncAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(1200)", maxLength: 1200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceAppointmentCalendarSyncs", x => x.Id);
                    table.CheckConstraint("CK_ServiceAppointmentCalendarSyncs_SyncStatus_Valid", "[SyncStatus] IN (1,2,3,4)");
                    table.ForeignKey(
                        name: "FK_ServiceAppointmentCalendarSyncs_ServiceAppointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "ServiceAppointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAppointmentCalendarSyncs_AppointmentId",
                table: "ServiceAppointmentCalendarSyncs",
                column: "AppointmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAppointmentCalendarSyncs_GoogleEventId",
                table: "ServiceAppointmentCalendarSyncs",
                column: "GoogleEventId",
                unique: true,
                filter: "[GoogleEventId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAppointmentCalendarSyncs_SyncStatus_LastSyncAtUtc",
                table: "ServiceAppointmentCalendarSyncs",
                columns: new[] { "SyncStatus", "LastSyncAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceAppointmentCalendarSyncs");
        }
    }
}
