using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderCreditsLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProviderCreditWallets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LastMovementAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderCreditWallets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderCreditWallets_Users_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProviderCreditLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntryType = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BalanceBefore = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ReferenceType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EffectiveAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AdminEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderCreditLedgerEntries", x => x.Id);
                    table.CheckConstraint("CK_ProviderCreditLedgerEntries_Amount_Positive", "[Amount] > 0");
                    table.CheckConstraint("CK_ProviderCreditLedgerEntries_Balance_NonNegative", "[BalanceAfter] >= 0");
                    table.ForeignKey(
                        name: "FK_ProviderCreditLedgerEntries_ProviderCreditWallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "ProviderCreditWallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderCreditLedgerEntries_ExpiresAtUtc",
                table: "ProviderCreditLedgerEntries",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderCreditLedgerEntries_ProviderId_EffectiveAtUtc",
                table: "ProviderCreditLedgerEntries",
                columns: new[] { "ProviderId", "EffectiveAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderCreditLedgerEntries_ProviderId_EntryType_EffectiveAtUtc",
                table: "ProviderCreditLedgerEntries",
                columns: new[] { "ProviderId", "EntryType", "EffectiveAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderCreditLedgerEntries_WalletId",
                table: "ProviderCreditLedgerEntries",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderCreditWallets_ProviderId",
                table: "ProviderCreditWallets",
                column: "ProviderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProviderCreditLedgerEntries");

            migrationBuilder.DropTable(
                name: "ProviderCreditWallets");
        }
    }
}
