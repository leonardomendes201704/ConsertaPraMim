using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceWarrantyClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceWarrantyClaims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceAppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisitAppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IssueDescription = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    ProviderResponseReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AdminEscalationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WarrantyWindowEndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProviderResponseDueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProviderRespondedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EscalatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceWarrantyClaims", x => x.Id);
                    table.CheckConstraint("CK_ServiceWarrantyClaims_ProviderResponseDueAtUtc_Valid", "[ProviderResponseDueAtUtc] >= [RequestedAtUtc]");
                    table.CheckConstraint("CK_ServiceWarrantyClaims_WarrantyWindowEndsAtUtc_Valid", "[WarrantyWindowEndsAtUtc] >= [RequestedAtUtc]");
                    table.ForeignKey(
                        name: "FK_ServiceWarrantyClaims_ServiceAppointments_RevisitAppointmentId",
                        column: x => x.RevisitAppointmentId,
                        principalTable: "ServiceAppointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ServiceWarrantyClaims_ServiceAppointments_ServiceAppointmentId",
                        column: x => x.ServiceAppointmentId,
                        principalTable: "ServiceAppointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceWarrantyClaims_ServiceRequests_ServiceRequestId",
                        column: x => x.ServiceRequestId,
                        principalTable: "ServiceRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceWarrantyClaims_Users_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceWarrantyClaims_Users_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceWarrantyClaims_ClientId_CreatedAt",
                table: "ServiceWarrantyClaims",
                columns: new[] { "ClientId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceWarrantyClaims_ProviderId_Status_ProviderResponseDueAtUtc",
                table: "ServiceWarrantyClaims",
                columns: new[] { "ProviderId", "Status", "ProviderResponseDueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceWarrantyClaims_RevisitAppointmentId",
                table: "ServiceWarrantyClaims",
                column: "RevisitAppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceWarrantyClaims_ServiceAppointmentId_Status_ProviderResponseDueAtUtc",
                table: "ServiceWarrantyClaims",
                columns: new[] { "ServiceAppointmentId", "Status", "ProviderResponseDueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceWarrantyClaims_ServiceRequestId_CreatedAt",
                table: "ServiceWarrantyClaims",
                columns: new[] { "ServiceRequestId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceWarrantyClaims");
        }
    }
}
