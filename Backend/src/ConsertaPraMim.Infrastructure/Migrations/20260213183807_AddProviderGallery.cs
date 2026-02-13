using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderGallery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProviderGalleryAlbums",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    IsServiceAlbum = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderGalleryAlbums", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderGalleryAlbums_ServiceRequests_ServiceRequestId",
                        column: x => x.ServiceRequestId,
                        principalTable: "ServiceRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProviderGalleryAlbums_Users_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProviderGalleryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlbumId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FileUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    MediaKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Caption = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderGalleryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderGalleryItems_ProviderGalleryAlbums_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "ProviderGalleryAlbums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProviderGalleryItems_ServiceRequests_ServiceRequestId",
                        column: x => x.ServiceRequestId,
                        principalTable: "ServiceRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProviderGalleryItems_Users_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderGalleryAlbums_ProviderId_CreatedAt",
                table: "ProviderGalleryAlbums",
                columns: new[] { "ProviderId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderGalleryAlbums_ProviderId_ServiceRequestId",
                table: "ProviderGalleryAlbums",
                columns: new[] { "ProviderId", "ServiceRequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderGalleryAlbums_ServiceRequestId",
                table: "ProviderGalleryAlbums",
                column: "ServiceRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderGalleryItems_AlbumId",
                table: "ProviderGalleryItems",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderGalleryItems_ProviderId_CreatedAt",
                table: "ProviderGalleryItems",
                columns: new[] { "ProviderId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderGalleryItems_ServiceRequestId",
                table: "ProviderGalleryItems",
                column: "ServiceRequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProviderGalleryItems");

            migrationBuilder.DropTable(
                name: "ProviderGalleryAlbums");
        }
    }
}
