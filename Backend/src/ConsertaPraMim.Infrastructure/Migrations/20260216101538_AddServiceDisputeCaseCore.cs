using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceDisputeCaseCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceDisputeCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceAppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpenedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpenedByRole = table.Column<int>(type: "int", nullable: false),
                    CounterpartyUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CounterpartyRole = table.Column<int>(type: "int", nullable: false),
                    OwnedByAdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    WaitingForRole = table.Column<int>(type: "int", nullable: true),
                    ReasonCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SlaDueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastInteractionAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolutionSummary = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceDisputeCases", x => x.Id);
                    table.CheckConstraint("CK_ServiceDisputeCases_SlaDueAtUtc_Valid", "[SlaDueAtUtc] >= [OpenedAtUtc]");
                    table.ForeignKey(
                        name: "FK_ServiceDisputeCases_ServiceAppointments_ServiceAppointmentId",
                        column: x => x.ServiceAppointmentId,
                        principalTable: "ServiceAppointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceDisputeCases_ServiceRequests_ServiceRequestId",
                        column: x => x.ServiceRequestId,
                        principalTable: "ServiceRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceDisputeCases_Users_CounterpartyUserId",
                        column: x => x.CounterpartyUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceDisputeCases_Users_OpenedByUserId",
                        column: x => x.OpenedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceDisputeCases_Users_OwnedByAdminUserId",
                        column: x => x.OwnedByAdminUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ServiceDisputeCaseAuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceDisputeCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorRole = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceDisputeCaseAuditEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceDisputeCaseAuditEntries_ServiceDisputeCases_ServiceDisputeCaseId",
                        column: x => x.ServiceDisputeCaseId,
                        principalTable: "ServiceDisputeCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceDisputeCaseAuditEntries_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ServiceDisputeCaseMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceDisputeCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AuthorRole = table.Column<int>(type: "int", nullable: false),
                    MessageType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    MessageText = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    IsInternal = table.Column<bool>(type: "bit", nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceDisputeCaseMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceDisputeCaseMessages_ServiceDisputeCases_ServiceDisputeCaseId",
                        column: x => x.ServiceDisputeCaseId,
                        principalTable: "ServiceDisputeCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceDisputeCaseMessages_Users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ServiceDisputeCaseAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceDisputeCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceDisputeCaseMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileUrl = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    MediaKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceDisputeCaseAttachments", x => x.Id);
                    table.CheckConstraint("CK_ServiceDisputeCaseAttachments_SizeBytes_NonNegative", "[SizeBytes] >= 0");
                    table.ForeignKey(
                        name: "FK_ServiceDisputeCaseAttachments_ServiceDisputeCaseMessages_ServiceDisputeCaseMessageId",
                        column: x => x.ServiceDisputeCaseMessageId,
                        principalTable: "ServiceDisputeCaseMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ServiceDisputeCaseAttachments_ServiceDisputeCases_ServiceDisputeCaseId",
                        column: x => x.ServiceDisputeCaseId,
                        principalTable: "ServiceDisputeCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceDisputeCaseAttachments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDisputeCaseAttachments_ServiceDisputeCaseId_CreatedAt",
                table: "ServiceDisputeCaseAttachments",
                columns: new[] { "ServiceDisputeCaseId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDisputeCaseAttachments_ServiceDisputeCaseMessageId_CreatedAt",
                table: "ServiceDisputeCaseAttachments",
                columns: new[] { "ServiceDisputeCaseMessageId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDisputeCaseAttachments_UploadedByUserId",
                table: "ServiceDisputeCaseAttachments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDisputeCaseAuditEntries_ActorUserId",
                table: "ServiceDisputeCaseAuditEntries",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDisputeCaseAuditEntries_ServiceDisputeCaseId_CreatedAt",
                table: "ServiceDisputeCaseAuditEntries",
                columns: new[] { "ServiceDisputeCaseId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDisputeCaseMessages_AuthorUserId",
                table: "ServiceDisputeCaseMessages",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDisputeCaseMessages_ServiceDisputeCaseId_CreatedAt",
                table: "ServiceDisputeCaseMessages",
                columns: new[] { "ServiceDisputeCaseId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDisputeCases_CounterpartyUserId",
                table: "ServiceDisputeCases",
                column: "CounterpartyUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDisputeCases_OpenedByUserId",
                table: "ServiceDisputeCases",
                column: "OpenedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDisputeCases_OwnedByAdminUserId",
                table: "ServiceDisputeCases",
                column: "OwnedByAdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDisputeCases_Priority_SlaDueAtUtc_Status",
                table: "ServiceDisputeCases",
                columns: new[] { "Priority", "SlaDueAtUtc", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDisputeCases_ServiceAppointmentId_Status_CreatedAt",
                table: "ServiceDisputeCases",
                columns: new[] { "ServiceAppointmentId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDisputeCases_ServiceRequestId_Status_CreatedAt",
                table: "ServiceDisputeCases",
                columns: new[] { "ServiceRequestId", "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceDisputeCaseAttachments");

            migrationBuilder.DropTable(
                name: "ServiceDisputeCaseAuditEntries");

            migrationBuilder.DropTable(
                name: "ServiceDisputeCaseMessages");

            migrationBuilder.DropTable(
                name: "ServiceDisputeCases");
        }
    }
}
