using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AllowMultipleAppointmentsPerRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ServiceAppointments_ServiceRequestId",
                table: "ServiceAppointments");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAppointments_Request_Window",
                table: "ServiceAppointments",
                columns: new[] { "ServiceRequestId", "WindowStartUtc", "WindowEndUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAppointments_ServiceRequestId",
                table: "ServiceAppointments",
                column: "ServiceRequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ServiceAppointments_Request_Window",
                table: "ServiceAppointments");

            migrationBuilder.DropIndex(
                name: "IX_ServiceAppointments_ServiceRequestId",
                table: "ServiceAppointments");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAppointments_ServiceRequestId",
                table: "ServiceAppointments",
                column: "ServiceRequestId",
                unique: true);
        }
    }
}
