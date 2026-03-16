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
        var schedulingClient = new Mock<ITelegramJourneySchedulingClient>(MockBehavior.Strict);
        var botApiClient = new Mock<ITelegramBotApiClient>(MockBehavior.Strict);
        var handoffStateService = new Mock<ITelegramHumanHandoffStateService>(MockBehavior.Strict);
        var observability = new Mock<ITelegramChatbotObservabilityService>(MockBehavior.Strict);

        var sut = CreateSut(
            chatService.Object,
            leadAutomationClient.Object,
            automationClient.Object,
            schedulingClient.Object,
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
                Text = "Preciso de ajuda urgente com meu chuveiro em Santos.",
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
            Text: "Preciso de ajuda urgente com meu chuveiro em Santos.",
            SentAtUtc: new DateTimeOffset(2026, 3, 14, 18, 0, 0, TimeSpan.Zero),
            Attachments: []);

        var chatService = new Mock<ITelegramChatService>(MockBehavior.Strict);
        var leadAutomationClient = new Mock<ITelegramLeadAutomationClient>(MockBehavior.Strict);
        var automationClient = new Mock<ITelegramMessageAutomationClient>(MockBehavior.Strict);
        var schedulingClient = new Mock<ITelegramJourneySchedulingClient>(MockBehavior.Strict);
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
                    request.ServiceCategory == "Eletricista" &&
                    request.City == "Santos" &&
                    string.IsNullOrWhiteSpace(request.UserPhone) &&
                    string.IsNullOrWhiteSpace(request.UserEmail) &&
                    request.StatusNote.Contains("cidade Santos", StringComparison.Ordinal) &&
                    request.StatusNote.Contains("categoria Eletricista", StringComparison.Ordinal) &&
                    request.StatusNote.Contains("Atendimento urgente", StringComparison.Ordinal)),
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
                    request.MessageText == "Preciso de ajuda urgente com meu chuveiro em Santos."),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramInboundMessageAutomationResult
            {
                Success = true,
                HttpStatusCode = StatusCodes.Status200OK,
                LeadId = 81,
                QueueStatus = "queued",
                Message = "Mensagem enfileirada."
            });
        schedulingClient
            .Setup(client => client.ProcessTurnAsync(
                It.Is<TelegramJourneySchedulingTurnRequest>(request =>
                    request.ChatbotConversationId != Guid.Empty &&
                    request.TelegramChatId == 5513997114422 &&
                    request.ChannelConversationId == "5513997114422" &&
                    request.MessageText == "Preciso de ajuda urgente com meu chuveiro em Santos."),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TelegramJourneySchedulingTurnResult.NoOp());
        botApiClient
            .Setup(client => client.SendMessageAsync(
                5513997114422,
                It.Is<string>(text =>
                    text.Contains("Recebi sua mensagem", StringComparison.Ordinal) &&
                    text.Contains("compartilhe seu telefone", StringComparison.Ordinal) &&
                    text.Contains("sua cidade", StringComparison.Ordinal) &&
                    text.Contains("tipo de servico", StringComparison.Ordinal)),
                It.Is<IReadOnlyList<StoredLocalFile>>(files => files.Count == 0),
                It.IsAny<CancellationToken>(),
                It.Is<TelegramMessageSendOptions?>(options => options is not null && options.RequestContactButton && !options.RemoveReplyKeyboard)))
            .Returns(Task.CompletedTask);

        var sut = CreateSut(
            chatService.Object,
            leadAutomationClient.Object,
            automationClient.Object,
            schedulingClient.Object,
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
        schedulingClient.VerifyAll();
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
        var schedulingClient = new Mock<ITelegramJourneySchedulingClient>(MockBehavior.Strict);
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
                HasPhone = true,
                QualificationStatus = "dados_pendentes",
                MissingRequiredFields = ["Categoria", "Cidade", "Contexto do problema"],
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
        schedulingClient
            .Setup(client => client.ProcessTurnAsync(
                It.Is<TelegramJourneySchedulingTurnRequest>(request =>
                    request.ChatbotConversationId != Guid.Empty &&
                    request.TelegramChatId == 5513997114422),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TelegramJourneySchedulingTurnResult.NoOp());
        botApiClient
            .SetupGet(client => client.IsConfigured)
            .Returns(true);
        handoffStateService
            .Setup(service => service.IsActive(5513997114422))
            .Returns(false);
        botApiClient
            .Setup(client => client.SendMessageAsync(
                5513997114422,
                It.Is<string>(text =>
                    text.Contains("Recebi seu telefone", StringComparison.Ordinal) &&
                    text.Contains("tipo de servico", StringComparison.Ordinal) &&
                    !text.Contains("sua cidade", StringComparison.Ordinal)),
                It.Is<IReadOnlyList<StoredLocalFile>>(files => files.Count == 0),
                It.IsAny<CancellationToken>(),
                It.Is<TelegramMessageSendOptions?>(options => options is not null && options.RemoveReplyKeyboard)))
            .Returns(Task.CompletedTask);

        var sut = CreateSut(
            chatService.Object,
            leadAutomationClient.Object,
            automationClient.Object,
            schedulingClient.Object,
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
        schedulingClient.VerifyAll();
        botApiClient.VerifyAll();
        handoffStateService.VerifyAll();
        observability.VerifyAll();
    }

    [Fact(DisplayName = "Telegram inbound update processor | Deve continuar qualificacao quando o telefone ja estiver persistido")]
    public async Task ProcessAsync_DeveResponderQuandoContatoJaEstiverPersistido()
    {
        var update = new TelegramUpdate
        {
            UpdateId = 9010,
            Message = new TelegramMessage
            {
                MessageId = 778,
                DateUnix = 1_773_512_560,
                Text = "Praia Grande",
                Chat = new TelegramChat
                {
                    Id = 5513996891738,
                    FirstName = "Mendes"
                },
                From = new TelegramUser
                {
                    Id = 5513996891738,
                    FirstName = "Mendes"
                }
            }
        };

        var storedMessage = new ChatMessageDto(
            Id: "telegram:5513996891738:778",
            ChatId: 5513996891738,
            IsOutgoing: false,
            SenderDisplayName: "Mendes",
            Text: "Praia Grande",
            SentAtUtc: new DateTimeOffset(2026, 3, 16, 18, 20, 0, TimeSpan.Zero),
            Attachments: []);

        var chatService = new Mock<ITelegramChatService>(MockBehavior.Strict);
        var leadAutomationClient = new Mock<ITelegramLeadAutomationClient>(MockBehavior.Strict);
        var automationClient = new Mock<ITelegramMessageAutomationClient>(MockBehavior.Strict);
        var schedulingClient = new Mock<ITelegramJourneySchedulingClient>(MockBehavior.Strict);
        var botApiClient = new Mock<ITelegramBotApiClient>(MockBehavior.Strict);
        var handoffStateService = new Mock<ITelegramHumanHandoffStateService>(MockBehavior.Strict);
        var observability = new Mock<ITelegramChatbotObservabilityService>(MockBehavior.Strict);

        observability.Setup(service => service.RecordInboundMessage(0));
        chatService
            .Setup(service => service.ReceiveFromTelegramAsync(update.Message, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedMessage);
        leadAutomationClient
            .Setup(client => client.UpsertLeadAsync(
                It.Is<TelegramLeadAutomationUpsertRequest>(request =>
                    request.BoardType == "clientes" &&
                    request.City == "Praia Grande" &&
                    request.ServiceCategory == string.Empty),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramLeadAutomationUpsertResult
            {
                Success = true,
                HttpStatusCode = StatusCodes.Status200OK,
                LeadId = 81,
                Created = false,
                BoardType = "clientes",
                HasPhone = true,
                HasCity = true,
                QualificationStatus = "dados_pendentes",
                MissingRequiredFields = ["Categoria", "CEP", "Logradouro ou bairro", "Contexto do problema"],
                Message = "Lead atualizado via automacao do bot Telegram."
            });
        automationClient
            .Setup(client => client.MirrorInboundMessageAsync(
                It.IsAny<TelegramInboundMessageAutomationRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramInboundMessageAutomationResult
            {
                Success = true,
                HttpStatusCode = StatusCodes.Status202Accepted,
                LeadId = 81,
                QueueStatus = "queued",
                Message = "Mensagem enfileirada."
            });
        schedulingClient
            .Setup(client => client.ProcessTurnAsync(
                It.IsAny<TelegramJourneySchedulingTurnRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TelegramJourneySchedulingTurnResult.NoOp());
        botApiClient.SetupGet(client => client.IsConfigured).Returns(true);
        handoffStateService.Setup(service => service.IsActive(5513996891738)).Returns(false);
        botApiClient
            .Setup(client => client.SendMessageAsync(
                5513996891738,
                It.Is<string>(text =>
                    text.Contains("tipo de servico", StringComparison.Ordinal) &&
                    !text.Contains("seu CEP", StringComparison.Ordinal) &&
                    !text.Contains("bairro ou logradouro", StringComparison.Ordinal) &&
                    !text.Contains("o que voce precisa resolver", StringComparison.Ordinal)),
                It.Is<IReadOnlyList<StoredLocalFile>>(files => files.Count == 0),
                It.IsAny<CancellationToken>(),
                It.Is<TelegramMessageSendOptions?>(options => options is not null && options.RemoveReplyKeyboard)))
            .Returns(Task.CompletedTask);

        var sut = CreateSut(
            chatService.Object,
            leadAutomationClient.Object,
            automationClient.Object,
            schedulingClient.Object,
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
        schedulingClient.VerifyAll();
        botApiClient.VerifyAll();
        handoffStateService.VerifyAll();
        observability.VerifyAll();
    }

    [Fact(DisplayName = "Telegram inbound update processor | Nao deve resetar para o prompt inicial apos capturar e-mail")]
    public async Task ProcessAsync_NaoDeveResetarParaPromptInicialAposCapturarEmail()
    {
        var update = new TelegramUpdate
        {
            UpdateId = 9011,
            Message = new TelegramMessage
            {
                MessageId = 779,
                DateUnix = 1_773_512_580,
                Text = "encontrosnocaminho@gmail.com",
                Chat = new TelegramChat
                {
                    Id = 5513996891738,
                    FirstName = "Mendes"
                },
                From = new TelegramUser
                {
                    Id = 5513996891738,
                    FirstName = "Mendes"
                }
            }
        };

        var storedMessage = new ChatMessageDto(
            Id: "telegram:5513996891738:779",
            ChatId: 5513996891738,
            IsOutgoing: false,
            SenderDisplayName: "Mendes",
            Text: "encontrosnocaminho@gmail.com",
            SentAtUtc: new DateTimeOffset(2026, 3, 16, 18, 25, 0, TimeSpan.Zero),
            Attachments: []);

        var chatService = new Mock<ITelegramChatService>(MockBehavior.Strict);
        var leadAutomationClient = new Mock<ITelegramLeadAutomationClient>(MockBehavior.Strict);
        var automationClient = new Mock<ITelegramMessageAutomationClient>(MockBehavior.Strict);
        var schedulingClient = new Mock<ITelegramJourneySchedulingClient>(MockBehavior.Strict);
        var botApiClient = new Mock<ITelegramBotApiClient>(MockBehavior.Strict);
        var handoffStateService = new Mock<ITelegramHumanHandoffStateService>(MockBehavior.Strict);
        var observability = new Mock<ITelegramChatbotObservabilityService>(MockBehavior.Strict);

        observability.Setup(service => service.RecordInboundMessage(0));
        chatService
            .Setup(service => service.ReceiveFromTelegramAsync(update.Message, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedMessage);
        leadAutomationClient
            .Setup(client => client.UpsertLeadAsync(
                It.Is<TelegramLeadAutomationUpsertRequest>(request =>
                    request.BoardType == "clientes" &&
                    request.UserEmail == "encontrosnocaminho@gmail.com" &&
                    string.IsNullOrWhiteSpace(request.UserPhone)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramLeadAutomationUpsertResult
            {
                Success = true,
                HttpStatusCode = StatusCodes.Status200OK,
                LeadId = 81,
                Created = false,
                BoardType = "clientes",
                HasPhone = true,
                HasEmail = true,
                HasCity = true,
                HasServiceCategory = true,
                QualificationStatus = "qualificacao_validada",
                MissingRequiredFields = [],
                Message = "Lead atualizado via automacao do bot Telegram."
            });
        automationClient
            .Setup(client => client.MirrorInboundMessageAsync(
                It.IsAny<TelegramInboundMessageAutomationRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramInboundMessageAutomationResult
            {
                Success = true,
                HttpStatusCode = StatusCodes.Status202Accepted,
                LeadId = 81,
                QueueStatus = "queued",
                Message = "Mensagem enfileirada."
            });
        schedulingClient
            .Setup(client => client.ProcessTurnAsync(
                It.IsAny<TelegramJourneySchedulingTurnRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TelegramJourneySchedulingTurnResult.NoOp());
        botApiClient.SetupGet(client => client.IsConfigured).Returns(true);
        handoffStateService.Setup(service => service.IsActive(5513996891738)).Returns(false);
        botApiClient
            .Setup(client => client.SendMessageAsync(
                5513996891738,
                It.Is<string>(text =>
                    text.Contains("Recebi seu e-mail", StringComparison.Ordinal) &&
                    !text.Contains("compartilhe seu telefone", StringComparison.Ordinal)),
                It.Is<IReadOnlyList<StoredLocalFile>>(files => files.Count == 0),
                It.IsAny<CancellationToken>(),
                It.Is<TelegramMessageSendOptions?>(options => options is not null && options.RemoveReplyKeyboard)))
            .Returns(Task.CompletedTask);

        var sut = CreateSut(
            chatService.Object,
            leadAutomationClient.Object,
            automationClient.Object,
            schedulingClient.Object,
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
        schedulingClient.VerifyAll();
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
                Text = "Sou eletricista em Praia Grande e quero me cadastrar como prestador parceiro da plataforma.",
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
            Text: "Sou eletricista em Praia Grande e quero me cadastrar como prestador parceiro da plataforma.",
            SentAtUtc: new DateTimeOffset(2026, 3, 14, 18, 5, 0, TimeSpan.Zero),
            Attachments: []);

        var chatService = new Mock<ITelegramChatService>(MockBehavior.Strict);
        var leadAutomationClient = new Mock<ITelegramLeadAutomationClient>(MockBehavior.Strict);
        var automationClient = new Mock<ITelegramMessageAutomationClient>(MockBehavior.Strict);
        var schedulingClient = new Mock<ITelegramJourneySchedulingClient>(MockBehavior.Strict);
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
                    request.TelegramChatId == 5513997000001 &&
                    request.ServiceCategory == "Eletricista" &&
                    request.City == "Praia Grande" &&
                    request.StatusNote.Contains("categoria tecnica Eletricista", StringComparison.Ordinal) &&
                    request.StatusNote.Contains("Cadastro como prestador", StringComparison.Ordinal)),
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
        botApiClient
            .SetupGet(client => client.IsConfigured)
            .Returns(false);

        var sut = CreateSut(
            chatService.Object,
            leadAutomationClient.Object,
            automationClient.Object,
            schedulingClient.Object,
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
        schedulingClient.VerifyNoOtherCalls();
        observability.VerifyAll();
    }

    [Fact(DisplayName = "Telegram inbound update processor | Nao deve responder automaticamente quando handoff humano estiver ativo")]
    public async Task ProcessAsync_NaoDeveResponderAutomaticamente_QuandoHandoffEstiverAtivo()
    {
        var update = new TelegramUpdate
        {
            UpdateId = 9005,
            Message = new TelegramMessage
            {
                MessageId = 888,
                DateUnix = 1_773_512_650,
                Text = "Ainda preciso de ajuda com esse atendimento.",
                Chat = new TelegramChat
                {
                    Id = 5513997114422,
                    FirstName = "Ricardo"
                },
                From = new TelegramUser
                {
                    Id = 5513997114422,
                    FirstName = "Ricardo"
                }
            }
        };

        var storedMessage = new ChatMessageDto(
            Id: "telegram:5513997114422:888",
            ChatId: 5513997114422,
            IsOutgoing: false,
            SenderDisplayName: "Ricardo",
            Text: "Ainda preciso de ajuda com esse atendimento.",
            SentAtUtc: new DateTimeOffset(2026, 3, 15, 13, 0, 0, TimeSpan.Zero),
            Attachments: []);

        var chatService = new Mock<ITelegramChatService>(MockBehavior.Strict);
        var leadAutomationClient = new Mock<ITelegramLeadAutomationClient>(MockBehavior.Strict);
        var automationClient = new Mock<ITelegramMessageAutomationClient>(MockBehavior.Strict);
        var schedulingClient = new Mock<ITelegramJourneySchedulingClient>(MockBehavior.Strict);
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
                It.IsAny<TelegramLeadAutomationUpsertRequest>(),
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
                It.Is<TelegramInboundMessageAutomationRequest>(request => request.TelegramChatId == 5513997114422),
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
            .Returns(true);

        var sut = CreateSut(
            chatService.Object,
            leadAutomationClient.Object,
            automationClient.Object,
            schedulingClient.Object,
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
        schedulingClient.VerifyNoOtherCalls();
        handoffStateService.VerifyAll();
        observability.VerifyAll();
        botApiClient.VerifyGet(client => client.IsConfigured, Times.Once);
        botApiClient.Verify(
            client => client.SendMessageAsync(
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<StoredLocalFile>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<TelegramMessageSendOptions?>()),
            Times.Never);
    }

    [Fact(DisplayName = "Telegram inbound update processor | Deve priorizar resposta de autoagendamento quando turno for tratado")]
    public async Task ProcessAsync_DevePriorizarRespostaDeAutoagendamentoQuandoTurnoForTratado()
    {
        var update = new TelegramUpdate
        {
            UpdateId = 9006,
            Message = new TelegramMessage
            {
                MessageId = 999,
                DateUnix = 1_773_512_700,
                Text = "Agendar visita",
                Chat = new TelegramChat
                {
                    Id = 5513997114422,
                    FirstName = "Ricardo"
                },
                From = new TelegramUser
                {
                    Id = 5513997114422,
                    FirstName = "Ricardo"
                }
            }
        };

        var storedMessage = new ChatMessageDto(
            Id: "telegram:5513997114422:999",
            ChatId: 5513997114422,
            IsOutgoing: false,
            SenderDisplayName: "Ricardo",
            Text: "Agendar visita",
            SentAtUtc: new DateTimeOffset(2026, 3, 15, 18, 15, 0, TimeSpan.Zero),
            Attachments: []);

        var chatService = new Mock<ITelegramChatService>(MockBehavior.Strict);
        var leadAutomationClient = new Mock<ITelegramLeadAutomationClient>(MockBehavior.Strict);
        var automationClient = new Mock<ITelegramMessageAutomationClient>(MockBehavior.Strict);
        var schedulingClient = new Mock<ITelegramJourneySchedulingClient>(MockBehavior.Strict);
        var botApiClient = new Mock<ITelegramBotApiClient>(MockBehavior.Strict);
        var handoffStateService = new Mock<ITelegramHumanHandoffStateService>(MockBehavior.Strict);
        var observability = new Mock<ITelegramChatbotObservabilityService>(MockBehavior.Strict);

        observability.Setup(service => service.RecordInboundMessage(0));
        observability.Setup(service => service.RecordBusinessEvent("scheduling_attempt", false));
        chatService
            .Setup(service => service.ReceiveFromTelegramAsync(update.Message, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedMessage);
        leadAutomationClient
            .Setup(client => client.UpsertLeadAsync(
                It.IsAny<TelegramLeadAutomationUpsertRequest>(),
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
                It.IsAny<TelegramInboundMessageAutomationRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramInboundMessageAutomationResult
            {
                Success = true,
                HttpStatusCode = StatusCodes.Status202Accepted,
                LeadId = 81,
                QueueStatus = "queued",
                Message = "Mensagem enfileirada."
            });
        schedulingClient
            .Setup(client => client.ProcessTurnAsync(
                It.Is<TelegramJourneySchedulingTurnRequest>(request =>
                    request.ChatbotConversationId != Guid.Empty &&
                    request.TelegramChatId == 5513997114422 &&
                    request.MessageText == "Agendar visita"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramJourneySchedulingTurnResult
            {
                Success = true,
                Handled = true,
                HttpStatusCode = StatusCodes.Status200OK,
                LeadId = 81,
                JourneyId = 17,
                CurrentState = "janela_sugerida",
                SchedulingStatus = "janela_sugerida",
                ReplyText = "Encontrei estas janelas para atendimento: 1. Hoje, 15/03, 10:00 as 12:00",
                RemoveReplyKeyboard = true
            });
        botApiClient.SetupGet(client => client.IsConfigured).Returns(true);
        handoffStateService.Setup(service => service.IsActive(5513997114422)).Returns(false);
        botApiClient
            .Setup(client => client.SendMessageAsync(
                5513997114422,
                It.Is<string>(text => text.Contains("Encontrei estas janelas", StringComparison.Ordinal)),
                It.Is<IReadOnlyList<StoredLocalFile>>(files => files.Count == 0),
                It.IsAny<CancellationToken>(),
                It.Is<TelegramMessageSendOptions?>(options => options is not null && options.RemoveReplyKeyboard)))
            .Returns(Task.CompletedTask);

        var sut = CreateSut(
            chatService.Object,
            leadAutomationClient.Object,
            automationClient.Object,
            schedulingClient.Object,
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
        schedulingClient.VerifyAll();
        botApiClient.VerifyAll();
        handoffStateService.VerifyAll();
        observability.VerifyAll();
    }

    private static TelegramInboundUpdateProcessor CreateSut(
        ITelegramChatService chatService,
        ITelegramLeadAutomationClient leadAutomationClient,
        ITelegramMessageAutomationClient automationClient,
        ITelegramJourneySchedulingClient journeySchedulingClient,
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
            journeySchedulingClient,
            botApiClient,
            humanHandoffStateService,
            observabilityService,
            NullLogger<TelegramInboundUpdateProcessor>.Instance);
    }
}
