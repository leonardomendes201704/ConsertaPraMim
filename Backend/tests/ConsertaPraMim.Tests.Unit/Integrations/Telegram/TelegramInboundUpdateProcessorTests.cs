using ConsertaPraMim.Web.TelegramBridge.Models;
using ConsertaPraMim.Web.TelegramBridge.Options;
using ConsertaPraMim.Web.TelegramBridge.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Integrations.Telegram;

public sealed class TelegramInboundUpdateProcessorTests
{
    [Fact(DisplayName = "Telegram inbound update processor | Deve ignorar update sem mensagem")]
    public async Task ProcessAsync_DeveIgnorarUpdateSemMensagem()
    {
        var chatService = new Mock<ITelegramChatService>(MockBehavior.Strict);
        var leadAutomationClient = new Mock<ITelegramLeadAutomationClient>(MockBehavior.Strict);
        var automationClient = new Mock<ITelegramMessageAutomationClient>(MockBehavior.Strict);
        var botApiClient = new Mock<ITelegramBotApiClient>(MockBehavior.Strict);
        var handoffStateService = new Mock<ITelegramHumanHandoffStateService>(MockBehavior.Strict);
        var observability = new Mock<ITelegramChatbotObservabilityService>(MockBehavior.Strict);

        var sut = CreateSut(
            chatService.Object,
            leadAutomationClient.Object,
            automationClient.Object,
            botApiClient.Object,
            handoffStateService.Object,
            observability.Object);

        var result = await sut.ProcessAsync(new TelegramUpdate
        {
            UpdateId = 9001,
            Message = null
        }, "webhook", CancellationToken.None);

        Assert.False(result);
    }

    [Fact(DisplayName = "Telegram inbound update processor | Deve persistir update e espelhar quando mirror estiver habilitado")]
    public async Task ProcessAsync_DevePersistirUpdateEEspelharQuandoMirrorEstiverHabilitado()
    {
        var update = new TelegramUpdate
        {
            UpdateId = 9002,
            Message = new TelegramMessage
            {
                MessageId = 321,
                DateUnix = 1_773_512_400,
                Text = "Preciso de ajuda com meu chuveiro.",
                Chat = new TelegramChat
                {
                    Id = 5513997114422,
                    FirstName = "Ricardo",
                    LastName = "Almeida"
                },
                From = new TelegramUser
                {
                    Id = 5513997114422,
                    FirstName = "Ricardo",
                    LastName = "Almeida"
                }
            }
        };

        var storedMessage = new ChatMessageDto(
            Id: "telegram:5513997114422:321",
            ChatId: 5513997114422,
            IsOutgoing: false,
            SenderDisplayName: "Ricardo Almeida",
            Text: "Preciso de ajuda com meu chuveiro.",
            SentAtUtc: new DateTimeOffset(2026, 3, 14, 18, 0, 0, TimeSpan.Zero),
            Attachments: []);

        var chatService = new Mock<ITelegramChatService>(MockBehavior.Strict);
        var leadAutomationClient = new Mock<ITelegramLeadAutomationClient>(MockBehavior.Strict);
        var automationClient = new Mock<ITelegramMessageAutomationClient>(MockBehavior.Strict);
        var botApiClient = new Mock<ITelegramBotApiClient>(MockBehavior.Strict);
        var handoffStateService = new Mock<ITelegramHumanHandoffStateService>(MockBehavior.Strict);
        var observability = new Mock<ITelegramChatbotObservabilityService>(MockBehavior.Strict);

        observability
            .Setup(service => service.RecordInboundMessage(0));
        chatService
            .Setup(service => service.ReceiveFromTelegramAsync(update.Message, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedMessage);
        leadAutomationClient
            .Setup(client => client.UpsertLeadAsync(
                It.Is<TelegramLeadAutomationUpsertRequest>(request =>
                    request.BoardType == "clientes" &&
                    request.ChannelConversationId == "5513997114422" &&
                    request.TelegramChatId == 5513997114422 &&
                    request.UserName == "Ricardo Almeida" &&
                    string.IsNullOrWhiteSpace(request.UserPhone) &&
                    string.IsNullOrWhiteSpace(request.UserEmail) &&
                    request.StatusNote == "Contato inicial recebido pelo bot Telegram."),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramLeadAutomationUpsertResult
            {
                Success = true,
                HttpStatusCode = StatusCodes.Status200OK,
                LeadId = 81,
                Created = true,
                BoardType = "clientes",
                Message = "Lead criado via automacao do bot Telegram."
            });
        handoffStateService
            .Setup(service => service.IsActive(5513997114422))
            .Returns(false);
        botApiClient
            .SetupGet(client => client.IsConfigured)
            .Returns(true);
        automationClient
            .Setup(client => client.MirrorInboundMessageAsync(
                It.Is<TelegramInboundMessageAutomationRequest>(request =>
                    request.ChatbotConversationId.HasValue &&
                    request.TelegramChatId == 5513997114422 &&
                    request.ChannelConversationId == "5513997114422" &&
                    request.ChannelMessageId == "telegram:5513997114422:321" &&
                    request.MessageText == "Preciso de ajuda com meu chuveiro."),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramInboundMessageAutomationResult
            {
                Success = true,
                HttpStatusCode = StatusCodes.Status200OK,
                LeadId = 81,
                QueueStatus = "queued",
                Message = "Mensagem enfileirada."
            });
        botApiClient
            .Setup(client => client.SendMessageAsync(
                5513997114422,
                It.Is<string>(text => text.Contains("Recebi sua mensagem", StringComparison.Ordinal) && text.Contains("compartilhe seu telefone", StringComparison.Ordinal)),
                It.Is<IReadOnlyList<StoredLocalFile>>(files => files.Count == 0),
                It.IsAny<CancellationToken>(),
                It.Is<TelegramMessageSendOptions?>(options => options is not null && options.RequestContactButton && !options.RemoveReplyKeyboard)))
            .Returns(Task.CompletedTask);

        var sut = CreateSut(
            chatService.Object,
            leadAutomationClient.Object,
            automationClient.Object,
            botApiClient.Object,
            handoffStateService.Object,
            observability.Object,
            new TelegramAutomationOptions
            {
                Enabled = true,
                ClientsAutomationEnabled = true,
                MirrorMessagesEnabled = true,
                SharedSecret = "segredo-compartilhado",
                CpmFullBaseUrl = "https://www.consertapramim.com",
                RequestTimeoutSeconds = 15
            });

        var result = await sut.ProcessAsync(update, "polling", CancellationToken.None);

        Assert.True(result);
        chatService.VerifyAll();
        automationClient.VerifyAll();
        leadAutomationClient.VerifyAll();
        botApiClient.VerifyAll();
        handoffStateService.VerifyAll();
        observability.VerifyAll();
    }

    [Fact(DisplayName = "Telegram inbound update processor | Deve capturar telefone nativo e enriquecer o mesmo lead")]
    public async Task ProcessAsync_DeveCapturarTelefoneNativoEEnriquecerLeadExistente()
    {
        var update = new TelegramUpdate
        {
            UpdateId = 9004,
            Message = new TelegramMessage
            {
                MessageId = 777,
                DateUnix = 1_773_512_550,
                Chat = new TelegramChat
                {
                    Id = 5513997114422,
                    FirstName = "Ricardo"
                },
                From = new TelegramUser
                {
                    Id = 5513997114422,
                    FirstName = "Ricardo"
                },
                Contact = new TelegramContact
                {
                    PhoneNumber = "+55 (13) 99711-4422",
                    FirstName = "Ricardo",
                    UserId = 5513997114422
                }
            }
        };

        var storedMessage = new ChatMessageDto(
            Id: "telegram:5513997114422:777",
            ChatId: 5513997114422,
            IsOutgoing: false,
            SenderDisplayName: "Ricardo",
            Text: "Contato compartilhado pelo Telegram.",
            SentAtUtc: new DateTimeOffset(2026, 3, 14, 18, 10, 0, TimeSpan.Zero),
            Attachments: []);

        var chatService = new Mock<ITelegramChatService>(MockBehavior.Strict);
        var leadAutomationClient = new Mock<ITelegramLeadAutomationClient>(MockBehavior.Strict);
        var automationClient = new Mock<ITelegramMessageAutomationClient>(MockBehavior.Strict);
        var botApiClient = new Mock<ITelegramBotApiClient>(MockBehavior.Strict);
        var handoffStateService = new Mock<ITelegramHumanHandoffStateService>(MockBehavior.Strict);
        var observability = new Mock<ITelegramChatbotObservabilityService>(MockBehavior.Strict);

        observability
            .Setup(service => service.RecordInboundMessage(0));
        chatService
            .Setup(service => service.ReceiveFromTelegramAsync(update.Message, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedMessage);
        leadAutomationClient
            .Setup(client => client.UpsertLeadAsync(
                It.Is<TelegramLeadAutomationUpsertRequest>(request =>
                    request.BoardType == "clientes" &&
                    request.TelegramChatId == 5513997114422 &&
                    request.UserPhone == "+5513997114422" &&
                    string.IsNullOrWhiteSpace(request.UserEmail)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramLeadAutomationUpsertResult
            {
                Success = true,
                HttpStatusCode = StatusCodes.Status200OK,
                LeadId = 81,
                Created = false,
                BoardType = "clientes",
                Message = "Lead atualizado via automacao do bot Telegram."
            });
        automationClient
            .Setup(client => client.MirrorInboundMessageAsync(
                It.Is<TelegramInboundMessageAutomationRequest>(request =>
                    request.ChatbotConversationId.HasValue &&
                    request.TelegramChatId == 5513997114422 &&
                    request.MessageText == "Contato compartilhado pelo Telegram."),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramInboundMessageAutomationResult
            {
                Success = true,
                HttpStatusCode = StatusCodes.Status202Accepted,
                LeadId = 81,
                QueueStatus = "queued",
                Message = "Mensagem enfileirada."
            });
        botApiClient
            .SetupGet(client => client.IsConfigured)
            .Returns(true);
        handoffStateService
            .Setup(service => service.IsActive(5513997114422))
            .Returns(false);
        botApiClient
            .Setup(client => client.SendMessageAsync(
                5513997114422,
                It.Is<string>(text => text.Contains("Recebi seu telefone", StringComparison.Ordinal)),
                It.Is<IReadOnlyList<StoredLocalFile>>(files => files.Count == 0),
                It.IsAny<CancellationToken>(),
                It.Is<TelegramMessageSendOptions?>(options => options is not null && options.RemoveReplyKeyboard)))
            .Returns(Task.CompletedTask);

        var sut = CreateSut(
            chatService.Object,
            leadAutomationClient.Object,
            automationClient.Object,
            botApiClient.Object,
            handoffStateService.Object,
            observability.Object,
            new TelegramAutomationOptions
            {
                Enabled = true,
                ClientsAutomationEnabled = true,
                MirrorMessagesEnabled = true,
                SharedSecret = "segredo-compartilhado",
                CpmFullBaseUrl = "https://www.consertapramim.com",
                RequestTimeoutSeconds = 15
            });

        var result = await sut.ProcessAsync(update, "webhook", CancellationToken.None);

        Assert.True(result);
        chatService.VerifyAll();
        leadAutomationClient.VerifyAll();
        automationClient.VerifyAll();
        botApiClient.VerifyAll();
        handoffStateService.VerifyAll();
        observability.VerifyAll();
    }

    [Fact(DisplayName = "Telegram inbound update processor | Deve classificar onboarding de prestador no board de prestadores")]
    public async Task ProcessAsync_DeveClassificarPrestadorNoBoardCorreto()
    {
        var update = new TelegramUpdate
        {
            UpdateId = 9003,
            Message = new TelegramMessage
            {
                MessageId = 654,
                DateUnix = 1_773_512_500,
                Text = "Quero me cadastrar como prestador parceiro da plataforma.",
                Chat = new TelegramChat
                {
                    Id = 5513997000001,
                    FirstName = "Marcio"
                },
                From = new TelegramUser
                {
                    Id = 5513997000001,
                    FirstName = "Marcio"
                }
            }
        };

        var storedMessage = new ChatMessageDto(
            Id: "telegram:5513997000001:654",
            ChatId: 5513997000001,
            IsOutgoing: false,
            SenderDisplayName: "Marcio",
            Text: "Quero me cadastrar como prestador parceiro da plataforma.",
            SentAtUtc: new DateTimeOffset(2026, 3, 14, 18, 5, 0, TimeSpan.Zero),
            Attachments: []);

        var chatService = new Mock<ITelegramChatService>(MockBehavior.Strict);
        var leadAutomationClient = new Mock<ITelegramLeadAutomationClient>(MockBehavior.Strict);
        var automationClient = new Mock<ITelegramMessageAutomationClient>(MockBehavior.Strict);
        var botApiClient = new Mock<ITelegramBotApiClient>(MockBehavior.Strict);
        var handoffStateService = new Mock<ITelegramHumanHandoffStateService>(MockBehavior.Strict);
        var observability = new Mock<ITelegramChatbotObservabilityService>(MockBehavior.Strict);

        observability
            .Setup(service => service.RecordInboundMessage(0));
        chatService
            .Setup(service => service.ReceiveFromTelegramAsync(update.Message, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedMessage);
        leadAutomationClient
            .Setup(client => client.UpsertLeadAsync(
                It.Is<TelegramLeadAutomationUpsertRequest>(request =>
                    request.BoardType == "prestadores" &&
                    request.TelegramChatId == 5513997000001),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramLeadAutomationUpsertResult
            {
                Success = true,
                HttpStatusCode = StatusCodes.Status200OK,
                LeadId = 99,
                Created = false,
                BoardType = "prestadores",
                Message = "Lead atualizado via automacao do bot Telegram."
            });
        automationClient
            .Setup(client => client.MirrorInboundMessageAsync(
                It.Is<TelegramInboundMessageAutomationRequest>(request =>
                    request.ChatbotConversationId.HasValue &&
                    request.TelegramChatId == 5513997000001 &&
                    request.ChannelMessageId == "telegram:5513997000001:654"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramInboundMessageAutomationResult
            {
                Success = true,
                HttpStatusCode = StatusCodes.Status202Accepted,
                LeadId = 99,
                QueueStatus = "queued",
                Message = "Mensagem enfileirada."
            });

        var sut = CreateSut(
            chatService.Object,
            leadAutomationClient.Object,
            automationClient.Object,
            botApiClient.Object,
            handoffStateService.Object,
            observability.Object,
            new TelegramAutomationOptions
            {
                Enabled = true,
                ProvidersAutomationEnabled = true,
                MirrorMessagesEnabled = true,
                SharedSecret = "segredo-compartilhado",
                CpmFullBaseUrl = "https://www.consertapramim.com",
                RequestTimeoutSeconds = 15
            });

        var result = await sut.ProcessAsync(update, "polling", CancellationToken.None);

        Assert.True(result);
        chatService.VerifyAll();
        leadAutomationClient.VerifyAll();
        automationClient.VerifyAll();
        observability.VerifyAll();
    }

    private static TelegramInboundUpdateProcessor CreateSut(
        ITelegramChatService chatService,
        ITelegramLeadAutomationClient leadAutomationClient,
        ITelegramMessageAutomationClient automationClient,
        ITelegramBotApiClient botApiClient,
        ITelegramHumanHandoffStateService humanHandoffStateService,
        ITelegramChatbotObservabilityService observabilityService,
        TelegramAutomationOptions? options = null)
    {
        return new TelegramInboundUpdateProcessor(
            chatService,
            Options.Create(options ?? new TelegramAutomationOptions
            {
                Enabled = false,
                ClientsAutomationEnabled = true,
                MirrorMessagesEnabled = false,
                SharedSecret = "segredo-compartilhado",
                CpmFullBaseUrl = "https://www.consertapramim.com",
                RequestTimeoutSeconds = 15
            }),
            leadAutomationClient,
            automationClient,
            botApiClient,
            humanHandoffStateService,
            observabilityService,
            NullLogger<TelegramInboundUpdateProcessor>.Instance);
    }
}
