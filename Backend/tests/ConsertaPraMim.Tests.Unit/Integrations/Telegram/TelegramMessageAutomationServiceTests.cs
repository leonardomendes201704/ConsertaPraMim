using AppMobileCPM.Integrations.Chatwoot;
using AppMobileCPM.Integrations.Telegram;
using AppMobileCPM.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Integrations.Telegram;

public sealed class TelegramMessageAutomationServiceTests
{
    [Fact(DisplayName = "Telegram Automation | Enqueue inbound | Deve reaproveitar idempotencia quando mensagem ja estiver na fila")]
    public async Task EnqueueInboundMessageAsync_DeveRetornarDuplicateQuandoMensagemJaExistirNaFila()
    {
        var chatbotConversationId = Guid.NewGuid();
        var kanbanService = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        var leadSyncService = new Mock<IChatwootLeadSyncService>(MockBehavior.Strict);
        var chatwootApiClient = new Mock<IChatwootApiClient>(MockBehavior.Strict);
        var queueService = new Mock<ITelegramDeliveryQueueService>(MockBehavior.Strict);
        var bridgeClient = new Mock<ITelegramBridgeDeliveryClient>(MockBehavior.Strict);

        kanbanService
            .Setup(service => service.FindLeadIdByTelegramChatbotConversationId(chatbotConversationId))
            .Returns(81);
        kanbanService
            .Setup(service => service.GetLeadDetails(81))
            .Returns(CreateLead(81, source: "Telegram", conversationId: 902, telegramChatId: 5513997114422));
        queueService
            .Setup(service => service.Enqueue(
                81,
                TelegramDeliveryDirections.TelegramToChatwoot,
                "msg-telegram-001",
                It.IsAny<string>(),
                902,
                5513997114422,
                It.Is<string>(message => message.Contains("espelhamento", StringComparison.OrdinalIgnoreCase)),
                true))
            .Returns(new AdminKanbanTelegramDeliveryQueueItemRecord
            {
                Id = 50,
                LeadId = 81,
                Direction = TelegramDeliveryDirections.TelegramToChatwoot,
                DeliveryKey = "msg-telegram-001",
                Status = TelegramDeliveryQueueStatuses.Queued,
                AttemptCount = 1,
                MaxAttempts = 10,
                NextAttemptAt = DateTime.UtcNow,
                IsDuplicate = true
            });

        var sut = CreateSut(
            kanbanService.Object,
            leadSyncService.Object,
            chatwootApiClient.Object,
            queueService.Object,
            bridgeClient.Object);

        var result = await sut.EnqueueInboundMessageAsync(
            new TelegramInboundMessageAutomationRequest
            {
                ChatbotConversationId = chatbotConversationId,
                ChannelConversationId = "chat-telegram-5513997114422",
                ChannelMessageId = "msg-telegram-001",
                TelegramChatId = 5513997114422,
                SenderDisplayName = "Ricardo Almeida",
                MessageText = "Preciso de ajuda com a lampada da cozinha.",
                SentAtUtc = new DateTime(2026, 3, 14, 18, 30, 0, DateTimeKind.Utc)
            },
            "segredo-compartilhado");

        Assert.True(result.Success);
        Assert.Equal(StatusCodes.Status200OK, result.HttpStatusCode);
        Assert.NotNull(result.Payload);
        Assert.True(result.Payload!.Duplicate);
        Assert.Equal("duplicate", result.Payload.QueueStatus);
        Assert.Contains("ja registrada", result.Payload.Message, StringComparison.OrdinalIgnoreCase);
        kanbanService.VerifyAll();
        queueService.VerifyAll();
    }

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
                    request.HandoffReasonCode == TelegramHandoffPolicy.ChatwootFirstHumanReplyReasonCode &&
                    request.HandoffReasonLabel == TelegramHandoffPolicy.ChatwootFirstHumanReplyReasonLabel &&
                    request.HandoffSource == TelegramHandoffPolicy.ChatwootOutboundSource &&
                    request.HandoffActivatedAtUtc.HasValue &&
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
                    request.HumanHandoffStatus == TelegramHandoffPolicy.ActiveStatus &&
                    request.HumanHandoffReason == TelegramHandoffPolicy.ChatwootFirstHumanReplyReasonLabel &&
                    request.HumanHandoffUpdatedAt.HasValue &&
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

    [Fact(DisplayName = "Telegram Automation | Deve reativar handoff quando o bot ja foi retomado antes de nova resposta humana")]
    public async Task TryEnqueueOutboundMessageFromChatwootAsync_DeveReativarHandoffQuandoLeadEstiverComBotRetomado()
    {
        var kanbanService = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        var leadSyncService = new Mock<IChatwootLeadSyncService>(MockBehavior.Strict);
        var chatwootApiClient = new Mock<IChatwootApiClient>(MockBehavior.Strict);
        var queueService = new Mock<ITelegramDeliveryQueueService>(MockBehavior.Strict);
        var bridgeClient = new Mock<ITelegramBridgeDeliveryClient>(MockBehavior.Strict);
        var lead = CreateLead(
            84,
            source: "Telegram",
            conversationId: 905,
            telegramChatId: 5513997004321,
            handoffStatus: TelegramHandoffPolicy.BotResumedStatus);

        queueService
            .Setup(service => service.Enqueue(
                84,
                TelegramDeliveryDirections.ChatwootToTelegram,
                It.IsAny<string>(),
                It.Is<string>(payloadJson =>
                    payloadJson.Contains("\"ActivateHumanHandoff\":true", StringComparison.OrdinalIgnoreCase)),
                905,
                5513997004321,
                It.IsAny<string>(),
                true))
            .Returns(new AdminKanbanTelegramDeliveryQueueItemRecord
            {
                Id = 62,
                LeadId = 84,
                Direction = TelegramDeliveryDirections.ChatwootToTelegram,
                DeliveryKey = "chatwoot:888",
                Status = TelegramDeliveryQueueStatuses.Queued,
                AttemptCount = 0,
                MaxAttempts = 10,
                NextAttemptAt = DateTime.UtcNow
            });

        var sut = CreateSut(
            kanbanService.Object,
            leadSyncService.Object,
            chatwootApiClient.Object,
            queueService.Object,
            bridgeClient.Object);

        var result = await sut.TryEnqueueOutboundMessageFromChatwootAsync(
            lead,
            888,
            "Agora um humano vai voltar a falar com voce.",
            "Atendente CPM",
            new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc));

        Assert.True(result);
        queueService.VerifyAll();
    }

    [Fact(DisplayName = "Telegram Automation | Outbound fallback idempotencia | Deve gerar a mesma chave quando nao houver ChatwootMessageId")]
    public async Task TryEnqueueOutboundMessageFromChatwootAsync_DeveGerarMesmaDeliveryKeyQuandoNaoHouverMessageId()
    {
        var kanbanService = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        var leadSyncService = new Mock<IChatwootLeadSyncService>(MockBehavior.Strict);
        var chatwootApiClient = new Mock<IChatwootApiClient>(MockBehavior.Strict);
        var queueService = new Mock<ITelegramDeliveryQueueService>(MockBehavior.Strict);
        var bridgeClient = new Mock<ITelegramBridgeDeliveryClient>(MockBehavior.Strict);
        var keys = new List<string>();
        var enqueueCount = 0;
        var lead = CreateLead(83, source: "Telegram", conversationId: 904, telegramChatId: 5513997001234);

        queueService
            .Setup(service => service.Enqueue(
                83,
                TelegramDeliveryDirections.ChatwootToTelegram,
                It.IsAny<string>(),
                It.IsAny<string>(),
                904,
                5513997001234,
                It.Is<string>(message => message.Contains("Chatwoot", StringComparison.OrdinalIgnoreCase)),
                true))
            .Returns((int _, string _, string deliveryKey, string _, long? _, long? _, string _, bool _) =>
            {
                enqueueCount++;
                keys.Add(deliveryKey);
                return new AdminKanbanTelegramDeliveryQueueItemRecord
                {
                    Id = enqueueCount == 1 ? 60 : 61,
                    LeadId = 83,
                    Direction = TelegramDeliveryDirections.ChatwootToTelegram,
                    DeliveryKey = deliveryKey,
                    Status = TelegramDeliveryQueueStatuses.Queued,
                    AttemptCount = 1,
                    MaxAttempts = 10,
                    NextAttemptAt = DateTime.UtcNow,
                    IsDuplicate = enqueueCount > 1
                };
            });

        var sut = CreateSut(
            kanbanService.Object,
            leadSyncService.Object,
            chatwootApiClient.Object,
            queueService.Object,
            bridgeClient.Object);

        var occurredAt = new DateTime(2026, 3, 14, 18, 45, 0, DateTimeKind.Utc);
        var first = await sut.TryEnqueueOutboundMessageFromChatwootAsync(
            lead,
            null,
            "Oi! Vou assumir seu atendimento agora.",
            "Atendente CPM",
            occurredAt);
        var second = await sut.TryEnqueueOutboundMessageFromChatwootAsync(
            lead,
            null,
            "Oi! Vou assumir seu atendimento agora.",
            "Atendente CPM",
            occurredAt);

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(2, keys.Count);
        Assert.Equal(keys[0], keys[1]);
        queueService.VerifyAll();
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
        long telegramChatId,
        string handoffStatus = "") =>
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
                TelegramChatId = telegramChatId,
                HumanHandoffStatus = handoffStatus
            },
            History = []
        };
}
