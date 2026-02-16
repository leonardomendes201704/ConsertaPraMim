using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlignServiceCompletionTermCascadePath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceCompletionTerms_ServiceRequests_ServiceRequestId",
                table: "ServiceCompletionTerms");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceCompletionTerms_ServiceRequests_ServiceRequestId",
                table: "ServiceCompletionTerms",
                column: "ServiceRequestId",
                principalTable: "ServiceRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceCompletionTerms_ServiceRequests_ServiceRequestId",
                table: "ServiceCompletionTerms");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceCompletionTerms_ServiceRequests_ServiceRequestId",
                table: "ServiceCompletionTerms",
                column: "ServiceRequestId",
                principalTable: "ServiceRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
