using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApiMonitoringRequestContextCapture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QueryStringJson",
                table: "ApiRequestLogs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestHeadersJson",
                table: "ApiRequestLogs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RouteValuesJson",
                table: "ApiRequestLogs",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QueryStringJson",
                table: "ApiRequestLogs");

            migrationBuilder.DropColumn(
                name: "RequestHeadersJson",
                table: "ApiRequestLogs");

            migrationBuilder.DropColumn(
                name: "RouteValuesJson",
                table: "ApiRequestLogs");
        }
    }
}
