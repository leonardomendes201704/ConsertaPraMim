using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceRequestCommercialVersionState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CommercialBaseValue",
                table: "ServiceRequests",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommercialCurrentValue",
                table: "ServiceRequests",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CommercialState",
                table: "ServiceRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "CommercialUpdatedAtUtc",
                table: "ServiceRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CommercialVersion",
                table: "ServiceRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_CommercialState_CommercialUpdatedAtUtc",
                table: "ServiceRequests",
                columns: new[] { "CommercialState", "CommercialUpdatedAtUtc" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ServiceRequests_CommercialBaseValue_NonNegative",
                table: "ServiceRequests",
                sql: "[CommercialBaseValue] IS NULL OR [CommercialBaseValue] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ServiceRequests_CommercialCurrentValue_GteBase",
                table: "ServiceRequests",
                sql: "[CommercialBaseValue] IS NULL OR [CommercialCurrentValue] IS NULL OR [CommercialCurrentValue] >= [CommercialBaseValue]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ServiceRequests_CommercialCurrentValue_NonNegative",
                table: "ServiceRequests",
                sql: "[CommercialCurrentValue] IS NULL OR [CommercialCurrentValue] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ServiceRequests_CommercialVersion_NonNegative",
                table: "ServiceRequests",
                sql: "[CommercialVersion] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ServiceRequests_CommercialState_CommercialUpdatedAtUtc",
                table: "ServiceRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ServiceRequests_CommercialBaseValue_NonNegative",
                table: "ServiceRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ServiceRequests_CommercialCurrentValue_GteBase",
                table: "ServiceRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ServiceRequests_CommercialCurrentValue_NonNegative",
                table: "ServiceRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ServiceRequests_CommercialVersion_NonNegative",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "CommercialBaseValue",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "CommercialCurrentValue",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "CommercialState",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "CommercialUpdatedAtUtc",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "CommercialVersion",
                table: "ServiceRequests");
        }
    }
}
