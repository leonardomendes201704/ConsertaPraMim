using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGalleryOperationalEvidenceMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EvidencePhase",
                table: "ProviderGalleryItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ServiceAppointmentId",
                table: "ProviderGalleryItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderGalleryItems_ServiceAppointmentId",
                table: "ProviderGalleryItems",
                column: "ServiceAppointmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProviderGalleryItems_ServiceAppointments_ServiceAppointmentId",
                table: "ProviderGalleryItems",
                column: "ServiceAppointmentId",
                principalTable: "ServiceAppointments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProviderGalleryItems_ServiceAppointments_ServiceAppointmentId",
                table: "ProviderGalleryItems");

            migrationBuilder.DropIndex(
                name: "IX_ProviderGalleryItems_ServiceAppointmentId",
                table: "ProviderGalleryItems");

            migrationBuilder.DropColumn(
                name: "EvidencePhase",
                table: "ProviderGalleryItems");

            migrationBuilder.DropColumn(
                name: "ServiceAppointmentId",
                table: "ProviderGalleryItems");
        }
    }
}
