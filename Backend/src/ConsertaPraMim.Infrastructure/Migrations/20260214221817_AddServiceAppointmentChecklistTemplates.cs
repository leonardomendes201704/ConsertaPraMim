using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceAppointmentChecklistTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceChecklistTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceChecklistTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceChecklistTemplates_ServiceCategoryDefinitions_CategoryDefinitionId",
                        column: x => x.CategoryDefinitionId,
                        principalTable: "ServiceCategoryDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServiceChecklistTemplateItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    HelpText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    RequiresEvidence = table.Column<bool>(type: "bit", nullable: false),
                    AllowNote = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceChecklistTemplateItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceChecklistTemplateItems_ServiceChecklistTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "ServiceChecklistTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceAppointmentChecklistHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceAppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousIsChecked = table.Column<bool>(type: "bit", nullable: true),
                    NewIsChecked = table.Column<bool>(type: "bit", nullable: false),
                    PreviousNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    NewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PreviousEvidenceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NewEvidenceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorRole = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceAppointmentChecklistHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceAppointmentChecklistHistories_ServiceAppointments_ServiceAppointmentId",
                        column: x => x.ServiceAppointmentId,
                        principalTable: "ServiceAppointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceAppointmentChecklistHistories_ServiceChecklistTemplateItems_TemplateItemId",
                        column: x => x.TemplateItemId,
                        principalTable: "ServiceChecklistTemplateItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceAppointmentChecklistHistories_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServiceAppointmentChecklistResponses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceAppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsChecked = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EvidenceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EvidenceFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    EvidenceContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EvidenceSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    CheckedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CheckedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceAppointmentChecklistResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceAppointmentChecklistResponses_ServiceAppointments_ServiceAppointmentId",
                        column: x => x.ServiceAppointmentId,
                        principalTable: "ServiceAppointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceAppointmentChecklistResponses_ServiceChecklistTemplateItems_TemplateItemId",
                        column: x => x.TemplateItemId,
                        principalTable: "ServiceChecklistTemplateItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceAppointmentChecklistResponses_Users_CheckedByUserId",
                        column: x => x.CheckedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAppointmentChecklistHistories_ActorUserId",
                table: "ServiceAppointmentChecklistHistories",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAppointmentChecklistHistories_ServiceAppointmentId_OccurredAtUtc",
                table: "ServiceAppointmentChecklistHistories",
                columns: new[] { "ServiceAppointmentId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAppointmentChecklistHistories_TemplateItemId",
                table: "ServiceAppointmentChecklistHistories",
                column: "TemplateItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAppointmentChecklistResponses_CheckedByUserId",
                table: "ServiceAppointmentChecklistResponses",
                column: "CheckedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAppointmentChecklistResponses_ServiceAppointmentId_TemplateItemId",
                table: "ServiceAppointmentChecklistResponses",
                columns: new[] { "ServiceAppointmentId", "TemplateItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAppointmentChecklistResponses_TemplateItemId",
                table: "ServiceAppointmentChecklistResponses",
                column: "TemplateItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceChecklistTemplateItems_TemplateId_SortOrder",
                table: "ServiceChecklistTemplateItems",
                columns: new[] { "TemplateId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceChecklistTemplates_CategoryDefinitionId",
                table: "ServiceChecklistTemplates",
                column: "CategoryDefinitionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceChecklistTemplates_CategoryDefinitionId_IsActive",
                table: "ServiceChecklistTemplates",
                columns: new[] { "CategoryDefinitionId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceAppointmentChecklistHistories");

            migrationBuilder.DropTable(
                name: "ServiceAppointmentChecklistResponses");

            migrationBuilder.DropTable(
                name: "ServiceChecklistTemplateItems");

            migrationBuilder.DropTable(
                name: "ServiceChecklistTemplates");
        }
    }
}
