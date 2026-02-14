using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceSchedulingFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProviderAvailabilityExceptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderAvailabilityExceptions", x => x.Id);
                    table.CheckConstraint("CK_ProviderAvailabilityExceptions_StartBeforeEnd", "[EndsAtUtc] > [StartsAtUtc]");
                    table.ForeignKey(
                        name: "FK_ProviderAvailabilityExceptions_Users_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProviderAvailabilityRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SlotDurationMinutes = table.Column<int>(type: "int", nullable: false, defaultValue: 30),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderAvailabilityRules", x => x.Id);
                    table.CheckConstraint("CK_ProviderAvailabilityRules_SlotDuration_Range", "[SlotDurationMinutes] BETWEEN 15 AND 240");
                    table.CheckConstraint("CK_ProviderAvailabilityRules_StartBeforeEnd", "[EndTime] > [StartTime]");
                    table.ForeignKey(
                        name: "FK_ProviderAvailabilityRules_Users_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceAppointments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WindowStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WindowEndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceAppointments", x => x.Id);
                    table.CheckConstraint("CK_ServiceAppointments_WindowStartBeforeEnd", "[WindowEndUtc] > [WindowStartUtc]");
                    table.ForeignKey(
                        name: "FK_ServiceAppointments_ServiceRequests_ServiceRequestId",
                        column: x => x.ServiceRequestId,
                        principalTable: "ServiceRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceAppointments_Users_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceAppointments_Users_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServiceAppointmentHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceAppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousStatus = table.Column<int>(type: "int", nullable: true),
                    NewStatus = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorRole = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceAppointmentHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceAppointmentHistories_ServiceAppointments_ServiceAppointmentId",
                        column: x => x.ServiceAppointmentId,
                        principalTable: "ServiceAppointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceAppointmentHistories_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderAvailabilityExceptions_ProviderId_StartsAtUtc_EndsAtUtc",
                table: "ProviderAvailabilityExceptions",
                columns: new[] { "ProviderId", "StartsAtUtc", "EndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderAvailabilityRules_ProviderId_DayOfWeek_StartTime_EndTime",
                table: "ProviderAvailabilityRules",
                columns: new[] { "ProviderId", "DayOfWeek", "StartTime", "EndTime" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAppointmentHistories_ActorUserId",
                table: "ServiceAppointmentHistories",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAppointmentHistories_ServiceAppointmentId_OccurredAtUtc",
                table: "ServiceAppointmentHistories",
                columns: new[] { "ServiceAppointmentId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAppointments_ClientId_WindowStartUtc_WindowEndUtc",
                table: "ServiceAppointments",
                columns: new[] { "ClientId", "WindowStartUtc", "WindowEndUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAppointments_ProviderId_Status_WindowStartUtc",
                table: "ServiceAppointments",
                columns: new[] { "ProviderId", "Status", "WindowStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAppointments_ProviderId_WindowStartUtc_WindowEndUtc",
                table: "ServiceAppointments",
                columns: new[] { "ProviderId", "WindowStartUtc", "WindowEndUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAppointments_ServiceRequestId",
                table: "ServiceAppointments",
                column: "ServiceRequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProviderAvailabilityExceptions");

            migrationBuilder.DropTable(
                name: "ProviderAvailabilityRules");

            migrationBuilder.DropTable(
                name: "ServiceAppointmentHistories");

            migrationBuilder.DropTable(
                name: "ServiceAppointments");
        }
    }
}
