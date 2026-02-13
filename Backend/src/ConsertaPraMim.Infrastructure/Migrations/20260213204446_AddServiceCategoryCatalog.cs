using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceCategoryCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CategoryDefinitionId",
                table: "ServiceRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ServiceCategoryDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    LegacyCategory = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceCategoryDefinitions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_CategoryDefinitionId",
                table: "ServiceRequests",
                column: "CategoryDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceCategoryDefinitions_IsActive",
                table: "ServiceCategoryDefinitions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceCategoryDefinitions_Slug",
                table: "ServiceCategoryDefinitions",
                column: "Slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceRequests_ServiceCategoryDefinitions_CategoryDefinitionId",
                table: "ServiceRequests",
                column: "CategoryDefinitionId",
                principalTable: "ServiceCategoryDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceRequests_ServiceCategoryDefinitions_CategoryDefinitionId",
                table: "ServiceRequests");

            migrationBuilder.DropTable(
                name: "ServiceCategoryDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_ServiceRequests_CategoryDefinitionId",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "CategoryDefinitionId",
                table: "ServiceRequests");
        }
    }
}
