using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceCompletionTermModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceCompletionTerms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceAppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AcceptedWithMethod = table.Column<int>(type: "int", nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    PayloadHashSha256 = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    AcceptancePinHashSha256 = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AcceptancePinExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcceptancePinFailedAttempts = table.Column<int>(type: "int", nullable: false),
                    AcceptedSignatureName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    AcceptedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ContestReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ContestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EscalatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceCompletionTerms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceCompletionTerms_ServiceAppointments_ServiceAppointmentId",
                        column: x => x.ServiceAppointmentId,
                        principalTable: "ServiceAppointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceCompletionTerms_ServiceRequests_ServiceRequestId",
                        column: x => x.ServiceRequestId,
                        principalTable: "ServiceRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceCompletionTerms_Users_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceCompletionTerms_Users_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceCompletionTerms_ClientId_Status_CreatedAt",
                table: "ServiceCompletionTerms",
                columns: new[] { "ClientId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceCompletionTerms_ProviderId_Status_CreatedAt",
                table: "ServiceCompletionTerms",
                columns: new[] { "ProviderId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceCompletionTerms_ServiceAppointmentId",
                table: "ServiceCompletionTerms",
                column: "ServiceAppointmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceCompletionTerms_ServiceRequestId_CreatedAt",
                table: "ServiceCompletionTerms",
                columns: new[] { "ServiceRequestId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceCompletionTerms");
        }
    }
}
