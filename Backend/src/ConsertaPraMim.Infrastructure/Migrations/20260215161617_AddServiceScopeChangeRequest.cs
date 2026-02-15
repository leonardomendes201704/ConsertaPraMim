using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceScopeChangeRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceScopeChangeRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceAppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AdditionalScopeDescription = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    IncrementalValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClientRespondedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClientResponseReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PreviousVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceScopeChangeRequests", x => x.Id);
                    table.CheckConstraint("CK_ServiceScopeChangeRequests_IncrementalValue_NonNegative", "[IncrementalValue] >= 0");
                    table.CheckConstraint("CK_ServiceScopeChangeRequests_Version_Positive", "[Version] > 0");
                    table.ForeignKey(
                        name: "FK_ServiceScopeChangeRequests_ServiceAppointments_ServiceAppointmentId",
                        column: x => x.ServiceAppointmentId,
                        principalTable: "ServiceAppointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceScopeChangeRequests_ServiceRequests_ServiceRequestId",
                        column: x => x.ServiceRequestId,
                        principalTable: "ServiceRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceScopeChangeRequests_ServiceScopeChangeRequests_PreviousVersionId",
                        column: x => x.PreviousVersionId,
                        principalTable: "ServiceScopeChangeRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceScopeChangeRequests_Users_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceScopeChangeRequests_PreviousVersionId",
                table: "ServiceScopeChangeRequests",
                column: "PreviousVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceScopeChangeRequests_ProviderId_Status_RequestedAtUtc",
                table: "ServiceScopeChangeRequests",
                columns: new[] { "ProviderId", "Status", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceScopeChangeRequests_ServiceAppointmentId_Status_RequestedAtUtc",
                table: "ServiceScopeChangeRequests",
                columns: new[] { "ServiceAppointmentId", "Status", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceScopeChangeRequests_ServiceRequestId_Version",
                table: "ServiceScopeChangeRequests",
                columns: new[] { "ServiceRequestId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceScopeChangeRequests");
        }
    }
}
