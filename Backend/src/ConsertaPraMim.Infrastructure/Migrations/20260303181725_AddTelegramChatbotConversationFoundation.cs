using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTelegramChatbotConversationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatbotConversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ChannelConversationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastInteractionAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastIntent = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    LastStep = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatbotConversations", x => x.Id);
                    table.CheckConstraint("CK_ChatbotConversations_Status_Valid", "[Status] IN (1,2)");
                    table.ForeignKey(
                        name: "FK_ChatbotConversations_Users_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChatbotActionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IntentName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: true),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1200)", maxLength: 1200, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatbotActionLogs", x => x.Id);
                    table.CheckConstraint("CK_ChatbotActionLogs_Status_Valid", "[Status] IN (1,2,3)");
                    table.ForeignKey(
                        name: "FK_ChatbotActionLogs_ChatbotConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "ChatbotConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatbotActionLogs_Users_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChatbotContextSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ContextJson = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: false),
                    PromptVersion = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    ModelName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    PromptTokens = table.Column<int>(type: "int", nullable: true),
                    CompletionTokens = table.Column<int>(type: "int", nullable: true),
                    TotalTokens = table.Column<int>(type: "int", nullable: true),
                    CapturedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatbotContextSnapshots", x => x.Id);
                    table.CheckConstraint("CK_ChatbotContextSnapshots_Tokens_NonNegative", "([PromptTokens] IS NULL OR [PromptTokens] >= 0) AND ([CompletionTokens] IS NULL OR [CompletionTokens] >= 0) AND ([TotalTokens] IS NULL OR [TotalTokens] >= 0)");
                    table.ForeignKey(
                        name: "FK_ChatbotContextSnapshots_ChatbotConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "ChatbotConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatbotContextSnapshots_Users_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChatbotMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ChannelMessageId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    IntentName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ModelName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    PromptTokens = table.Column<int>(type: "int", nullable: true),
                    CompletionTokens = table.Column<int>(type: "int", nullable: true),
                    TotalTokens = table.Column<int>(type: "int", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatbotMessages", x => x.Id);
                    table.CheckConstraint("CK_ChatbotMessages_Direction_Valid", "[Direction] IN (1,2,3)");
                    table.CheckConstraint("CK_ChatbotMessages_Tokens_NonNegative", "([PromptTokens] IS NULL OR [PromptTokens] >= 0) AND ([CompletionTokens] IS NULL OR [CompletionTokens] >= 0) AND ([TotalTokens] IS NULL OR [TotalTokens] >= 0)");
                    table.ForeignKey(
                        name: "FK_ChatbotMessages_ChatbotConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "ChatbotConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatbotMessages_Users_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatbotActionLogs_ClientId_OccurredAtUtc",
                table: "ChatbotActionLogs",
                columns: new[] { "ClientId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatbotActionLogs_ConversationId_OccurredAtUtc",
                table: "ChatbotActionLogs",
                columns: new[] { "ConversationId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatbotContextSnapshots_ClientId_CapturedAtUtc",
                table: "ChatbotContextSnapshots",
                columns: new[] { "ClientId", "CapturedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatbotContextSnapshots_ConversationId_CapturedAtUtc",
                table: "ChatbotContextSnapshots",
                columns: new[] { "ConversationId", "CapturedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatbotConversations_ClientId_Channel_ChannelConversationId",
                table: "ChatbotConversations",
                columns: new[] { "ClientId", "Channel", "ChannelConversationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatbotConversations_ClientId_Status_LastInteractionAtUtc",
                table: "ChatbotConversations",
                columns: new[] { "ClientId", "Status", "LastInteractionAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatbotMessages_ClientId_SentAtUtc",
                table: "ChatbotMessages",
                columns: new[] { "ClientId", "SentAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatbotMessages_ConversationId_SentAtUtc",
                table: "ChatbotMessages",
                columns: new[] { "ConversationId", "SentAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatbotActionLogs");

            migrationBuilder.DropTable(
                name: "ChatbotContextSnapshots");

            migrationBuilder.DropTable(
                name: "ChatbotMessages");

            migrationBuilder.DropTable(
                name: "ChatbotConversations");
        }
    }
}
