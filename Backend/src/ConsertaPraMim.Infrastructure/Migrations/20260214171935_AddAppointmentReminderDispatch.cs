using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentReminderDispatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppointmentReminderDispatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceAppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReminderOffsetMinutes = table.Column<int>(type: "int", nullable: false),
                    ScheduledForUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    EventKey = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: false),
                    ActionUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentReminderDispatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppointmentReminderDispatches_ServiceAppointments_ServiceAppointmentId",
                        column: x => x.ServiceAppointmentId,
                        principalTable: "ServiceAppointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppointmentReminderDispatches_Users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentReminderDispatches_EventKey",
                table: "AppointmentReminderDispatches",
                column: "EventKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentReminderDispatches_RecipientUserId",
                table: "AppointmentReminderDispatches",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentReminderDispatches_ServiceAppointmentId_Channel_ReminderOffsetMinutes",
                table: "AppointmentReminderDispatches",
                columns: new[] { "ServiceAppointmentId", "Channel", "ReminderOffsetMinutes" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentReminderDispatches_Status_NextAttemptAtUtc",
                table: "AppointmentReminderDispatches",
                columns: new[] { "Status", "NextAttemptAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppointmentReminderDispatches");
        }
    }
}
