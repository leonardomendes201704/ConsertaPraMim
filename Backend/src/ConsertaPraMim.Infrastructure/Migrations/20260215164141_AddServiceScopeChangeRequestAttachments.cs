using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceScopeChangeRequestAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceScopeChangeRequestAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceScopeChangeRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_ServiceScopeChangeRequestAttachments", x => x.Id);
                    table.CheckConstraint("CK_ServiceScopeChangeRequestAttachments_SizeBytes_NonNegative", "[SizeBytes] >= 0");
                    table.ForeignKey(
                        name: "FK_ServiceScopeChangeRequestAttachments_ServiceScopeChangeRequests_ServiceScopeChangeRequestId",
                        column: x => x.ServiceScopeChangeRequestId,
                        principalTable: "ServiceScopeChangeRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceScopeChangeRequestAttachments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceScopeChangeRequestAttachments_ServiceScopeChangeRequestId_CreatedAt",
                table: "ServiceScopeChangeRequestAttachments",
                columns: new[] { "ServiceScopeChangeRequestId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceScopeChangeRequestAttachments_UploadedByUserId_CreatedAt",
                table: "ServiceScopeChangeRequestAttachments",
                columns: new[] { "UploadedByUserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceScopeChangeRequestAttachments");
        }
    }
}
