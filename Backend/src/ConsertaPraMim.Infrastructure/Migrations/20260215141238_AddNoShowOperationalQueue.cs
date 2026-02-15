using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNoShowOperationalQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceAppointmentNoShowQueueItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceAppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiskLevel = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    ReasonsCsv = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FirstDetectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastDetectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedByAdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolutionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceAppointmentNoShowQueueItems", x => x.Id);
                    table.CheckConstraint("CK_NoShowQueueItem_Score_Range", "[Score] BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_ServiceAppointmentNoShowQueueItems_ServiceAppointments_ServiceAppointmentId",
                        column: x => x.ServiceAppointmentId,
                        principalTable: "ServiceAppointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceAppointmentNoShowQueueItems_Users_ResolvedByAdminUserId",
                        column: x => x.ResolvedByAdminUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAppointmentNoShowQueueItems_ResolvedByAdminUserId",
                table: "ServiceAppointmentNoShowQueueItems",
                column: "ResolvedByAdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAppointmentNoShowQueueItems_ServiceAppointmentId",
                table: "ServiceAppointmentNoShowQueueItems",
                column: "ServiceAppointmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAppointmentNoShowQueueItems_Status_LastDetectedAtUtc",
                table: "ServiceAppointmentNoShowQueueItems",
                columns: new[] { "Status", "LastDetectedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceAppointmentNoShowQueueItems");
        }
    }
}
