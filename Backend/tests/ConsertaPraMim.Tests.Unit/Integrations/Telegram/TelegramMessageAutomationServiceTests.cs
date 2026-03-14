using AppMobileCPM.Integrations.Chatwoot;
using AppMobileCPM.Integrations.Telegram;
using AppMobileCPM.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Integrations.Telegram;

public sealed class TelegramMessageAutomationServiceTests
{
    [Fact(DisplayName = "Telegram Automation | Deve espelhar mensagem Telegram como incoming no Chatwoot")]
    public async Task ProcessQueueItemAsync_DeveCriarMensagemIncomingNoChatwootParaMensagemTelegram()
    {
        var kanbanService = new Mock<IAdminKanbanService>();
        var leadSyncService = new Mock<IChatwootLeadSyncService>(MockBehavior.Strict);
        var chatwootApiClient = new Mock<IChatwootApiClient>();
        var queueService = new Mock<ITelegramDeliveryQueueService>(MockBehavior.Strict);
        var bridgeClient = new Mock<ITelegramBridgeDeliveryClient>(MockBehavior.Strict);

        kanbanService
            .Setup(service => service.GetLeadDetails(81))
            .Returns(CreateLead(81, source: "Telegram", conversationId: 902, telegramChatId: 5513997114422));
        chatwootApiClient
            .Setup(client => client.CreateMessageAsync(
                902,
                It.Is<ChatwootCreateMessageRequest>(request =>
                    request.MessageType == "incoming" &&
                    !request.Private &&
                    request.Content.Contains("Preciso de ajuda", StringComparison.Ordinal) &&
                    request.Content.Contains("foto-problema.jpg", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatwootMessageSummary
            {
                Id = 7001,
                Private = false
            });
        kanbanService
            .Setup(service => service.TouchTelegramLeadLink(
                81,
                It.Is<AdminKanbanTelegramLinkTouchRequest>(request =>
                    request.LastTelegramMessageSyncedAt.HasValue &&
                    !request.HumanHandoffStartedAt.HasValue)))
            .Returns(true);
        kanbanService
            .Setup(service => service.AddHistoryEvent(
                81,
                "telegram_message_synced_to_chatwoot",
                It.Is<string>(message => message.Contains("conversa #902", StringComparison.Ordinal))))
            .Returns(true);

        var sut = CreateSut(
            kanbanService.Object,
            leadSyncService.Object,
            chatwootApiClient.Object,
            queueService.Object,
            bridgeClient.Object);

        var item = new AdminKanbanTelegramDeliveryQueueItemRecord
        {
            Id = 1,
            LeadId = 81,
            Direction = TelegramDeliveryDirections.TelegramToChatwoot,
            DeliveryKey = "telegram:5513997114422:123",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new TelegramToChatwootDeliveryPayload
            {
                LeadId = 81,
                ChannelConversationId = "5513997114422",
                ChannelMessageId = "telegram:5513997114422:123",
                TelegramChatId = 5513997114422,
                SenderDisplayName = "Ricardo Almeida",
                MessageText = "Preciso de ajuda com minha instalacao.",
                SentAtUtc = new DateTime(2026, 3, 14, 15, 30, 0, DateTimeKind.Utc),
                Attachments =
                [
                    new TelegramInboundAttachmentDto
                    {
                        FileName = "foto-problema.jpg",
                        MediaKind = "image",
                        Url = "/uploads/telegram-bridge/foto-problema.jpg"
                    }
                ]
            }),
            Status = TelegramDeliveryQueueStatuses.Processing,
            AttemptCount = 1,
            MaxAttempts = 10,
            NextAttemptAt = DateTime.UtcNow
        };

        var result = await sut.ProcessQueueItemAsync(item);

        Assert.True(result.Succeeded);
        Assert.Contains("Chatwoot", result.Message, StringComparison.OrdinalIgnoreCase);
        kanbanService.VerifyAll();
        chatwootApiClient.VerifyAll();
    }

    [Fact(DisplayName = "Telegram Automation | Deve entregar mensagem humana do Chatwoot ao Telegram e ativar handoff")]
    public async Task ProcessQueueItemAsync_DeveEnviarMensagemHumanaDoChatwootParaTelegramERegistrarHandoff()
    {
        var kanbanService = new Mock<IAdminKanbanService>();
        var leadSyncService = new Mock<IChatwootLeadSyncService>(MockBehavior.Strict);
        var chatwootApiClient = new Mock<IChatwootApiClient>(MockBehavior.Strict);
        var queueService = new Mock<ITelegramDeliveryQueueService>(MockBehavior.Strict);
        var bridgeClient = new Mock<ITelegramBridgeDeliveryClient>();

        kanbanService
            .Setup(service => service.GetLeadDetails(82))
            .Returns(CreateLead(82, source: "Telegram", conversationId: 903, telegramChatId: 5513987654321));
        bridgeClient
            .Setup(client => client.SendHumanReplyAsync(
                It.Is<TelegramBridgeHumanReplyRequest>(request =>
                    request.LeadId == 82 &&
                    request.TelegramChatId == 5513987654321 &&
                    request.ChatwootConversationId == 903 &&
                    request.ChatwootMessageId == 777 &&
                    request.ActivateHumanHandoff &&
                    request.MessageText == "Oi! Vou assumir seu atendimento agora."),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramBridgeHumanReplyResult
            {
                Success = true,
                HttpStatusCode = StatusCodes.Status200OK,
                Message = "Mensagem humana enviada ao Telegram.",
                HumanHandoffActivated = true
            });
        kanbanService
            .Setup(service => service.TouchTelegramLeadLink(
                82,
                It.Is<AdminKanbanTelegramLinkTouchRequest>(request =>
                    request.HumanHandoffStartedAt.HasValue &&
                    request.LastChatwootMessageSyncedAt.HasValue)))
            .Returns(true);
        kanbanService
            .Setup(service => service.AddHistoryEvent(
                82,
                "chatwoot_handoff_humano_iniciado",
                It.Is<string>(message => message.Contains("Telegram", StringComparison.Ordinal))))
            .Returns(true);
        kanbanService
            .Setup(service => service.AddHistoryEvent(
                82,
                "chatwoot_message_synced_to_telegram",
                It.Is<string>(message => message.Contains("5513987654321", StringComparison.Ordinal))))
            .Returns(true);

        var sut = CreateSut(
            kanbanService.Object,
            leadSyncService.Object,
            chatwootApiClient.Object,
            queueService.Object,
            bridgeClient.Object);

        var item = new AdminKanbanTelegramDeliveryQueueItemRecord
        {
            Id = 2,
            LeadId = 82,
            Direction = TelegramDeliveryDirections.ChatwootToTelegram,
            DeliveryKey = "chatwoot:777",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new ChatwootToTelegramDeliveryPayload
            {
                LeadId = 82,
                ChatwootConversationId = 903,
                ChatwootMessageId = 777,
                TelegramChatId = 5513987654321,
                SenderName = "Atendente CPM",
                MessageText = "Oi! Vou assumir seu atendimento agora.",
                OccurredAtUtc = new DateTime(2026, 3, 14, 15, 40, 0, DateTimeKind.Utc),
                ActivateHumanHandoff = true
            }),
            Status = TelegramDeliveryQueueStatuses.Processing,
            AttemptCount = 1,
            MaxAttempts = 10,
            NextAttemptAt = DateTime.UtcNow
        };

        var result = await sut.ProcessQueueItemAsync(item);

        Assert.True(result.Succeeded);
        Assert.Contains("Telegram", result.Message, StringComparison.OrdinalIgnoreCase);
        bridgeClient.VerifyAll();
        kanbanService.VerifyAll();
    }

    private static TelegramMessageAutomationService CreateSut(
        IAdminKanbanService kanbanService,
        IChatwootLeadSyncService leadSyncService,
        IChatwootApiClient chatwootApiClient,
        ITelegramDeliveryQueueService queueService,
        ITelegramBridgeDeliveryClient bridgeClient)
    {
        return new TelegramMessageAutomationService(
            kanbanService,
            leadSyncService,
            chatwootApiClient,
            queueService,
            bridgeClient,
            Options.Create(new TelegramAutomationOptions
            {
                Enabled = true,
                ClientsAutomationEnabled = true,
                ProvidersAutomationEnabled = true,
                MirrorMessagesEnabled = true,
                RequireHumanHandoffForOutbound = true,
                SharedSecret = "segredo-compartilhado",
                TelegramBridgeBaseUrl = "https://bridge.exemplo.com",
                RequestTimeoutSeconds = 15,
                DeliveryWorkerEnabled = true,
                DeliveryWorkerIntervalSeconds = 20,
                DeliveryWorkerBatchSize = 20,
                DeliveryQueueMaxAttempts = 10
            }),
            NullLogger<TelegramMessageAutomationService>.Instance);
    }

    private static AdminKanbanLeadDetailsRecord CreateLead(
        int leadId,
        string source,
        long conversationId,
        long telegramChatId) =>
        new()
        {
            Id = leadId,
            StageId = 1,
            StageName = "Novo lead",
            BoardType = AdminKanbanBoardTypes.Clients,
            Name = "Ricardo Almeida",
            Phone = string.Empty,
            Email = "ricardo@email.com",
            ServiceCategory = "Eletricista",
            PostalCode = "11701-200",
            City = "Praia Grande",
            Source = source,
            Priority = "normal",
            StatusNote = string.Empty,
            InternalNotes = string.Empty,
            CreatedAt = new DateTime(2026, 3, 14, 14, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 3, 14, 14, 5, 0, DateTimeKind.Utc),
            Chatwoot = new AdminKanbanLeadChatwootSyncRecord
            {
                ContactId = 100,
                ConversationId = conversationId,
                InboxId = 1,
                SyncStatus = ChatwootSyncStatuses.Synced
            },
            Telegram = new AdminKanbanLeadTelegramLinkRecord
            {
                TelegramChatId = telegramChatId
            },
            History = []
        };
}
