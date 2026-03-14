using ConsertaPraMim.Web.TelegramBridge.Models;
using ConsertaPraMim.Web.TelegramBridge.Options;
using ConsertaPraMim.Web.TelegramBridge.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class TelegramChatbotOrchestratorTests
{
    [Fact(DisplayName = "Telegram IA orchestrator | Fallback | Deve responder fallback e registrar acao de falha")]
    public async Task GenerateAssistantReplyAsync_ShouldReturnFallbackAndRegisterFailedAction_WhenGatewayFails()
    {
        var conversationId = Guid.NewGuid();

        var gatewayMock = new Mock<ITelegramAiGateway>();
        gatewayMock
            .Setup(gateway => gateway.GenerateReplyAsync(It.IsAny<TelegramAiGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramAiGatewayResult(
                Success: false,
                ErrorCode: "openai_unavailable",
                ErrorMessage: "OpenAI indisponivel",
                AttemptCount: 3,
                LatencyMilliseconds: 250));

        var apiClientMock = BuildApiClientMock(conversationId);
        apiClientMock
            .Setup(client => client.RegisterActionAsync(
                It.IsAny<string>(),
                conversationId,
                "openai_generate_reply",
                3,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();

        var options = Options.Create(new TelegramBridgeAiOptions
        {
            Enabled = true,
            Provider = "OpenAI",
            Model = "gpt-4.1-mini",
            ApiKey = "test-key",
            FallbackMessage = "Fallback seguro"
        });

        using var memoryCache = new MemoryCache(new MemoryCacheOptions());

        var orchestrator = new TelegramChatbotOrchestrator(
            gatewayMock.Object,
            apiClientMock.Object,
            options,
            memoryCache,
            Mock.Of<ILogger<TelegramChatbotOrchestrator>>(),
            new TelegramServiceRequestTriageEngine(),
            new TelegramSchedulingNaturalLanguageParser());

        var clientMessage = BuildClientMessage(conversationId: 77L, text: "Meu ar esta com erro CH26", messageId: "m-1");

        var reply = await orchestrator.GenerateAssistantReplyAsync(
            "api-token",
            77L,
            clientMessage,
            "Atendimento Cliente",
            CancellationToken.None);

        Assert.NotNull(reply);
        Assert.True(reply!.UsedFallback);
        Assert.Equal("Fallback seguro", reply.MessageText);
        Assert.Equal("unknown", reply.Intent);

        apiClientMock.Verify();
    }

    [Fact(DisplayName = "Telegram IA orchestrator | Cache | Deve reutilizar resposta sem nova chamada ao gateway")]
    public async Task GenerateAssistantReplyAsync_ShouldUseCache_WhenSameMessageWithinTtl()
    {
        var conversationId = Guid.NewGuid();

        var gatewayMock = new Mock<ITelegramAiGateway>();
        gatewayMock
            .Setup(gateway => gateway.GenerateReplyAsync(It.IsAny<TelegramAiGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramAiGatewayResult(
                Success: true,
                OutputText: "{\"messageToClient\":\"Entendi. Qual a marca e o modelo?\",\"intent\":\"triage_problem\",\"nextStep\":\"collect_equipment_details\",\"confidence\":0.92,\"entities\":{\"codigo_erro\":\"CH26\"}}",
                InputTokens: 120,
                OutputTokens: 60,
                TotalTokens: 180,
                AttemptCount: 1,
                LatencyMilliseconds: 120));

        var apiClientMock = BuildApiClientMock(conversationId);

        var options = Options.Create(new TelegramBridgeAiOptions
        {
            Enabled = true,
            Provider = "OpenAI",
            Model = "gpt-4.1-mini",
            ApiKey = "test-key",
            CacheTtlSeconds = 90
        });

        using var memoryCache = new MemoryCache(new MemoryCacheOptions());

        var orchestrator = new TelegramChatbotOrchestrator(
            gatewayMock.Object,
            apiClientMock.Object,
            options,
            memoryCache,
            Mock.Of<ILogger<TelegramChatbotOrchestrator>>(),
            new TelegramServiceRequestTriageEngine(),
            new TelegramSchedulingNaturalLanguageParser());

        var clientMessage = BuildClientMessage(conversationId: 88L, text: "Meu ar esta com erro CH26", messageId: "m-2");

        var firstReply = await orchestrator.GenerateAssistantReplyAsync(
            "api-token",
            88L,
            clientMessage,
            "Atendimento Cliente",
            CancellationToken.None);

        var secondReply = await orchestrator.GenerateAssistantReplyAsync(
            "api-token",
            88L,
            clientMessage,
            "Atendimento Cliente",
            CancellationToken.None);

        Assert.NotNull(firstReply);
        Assert.NotNull(secondReply);
        Assert.False(firstReply!.UsedCache);
        Assert.True(secondReply!.UsedCache);
        Assert.Equal("open_service_request", firstReply.Intent);
        Assert.Equal("open_service_request", secondReply.Intent);

        gatewayMock.Verify(
            gateway => gateway.GenerateReplyAsync(It.IsAny<TelegramAiGatewayRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);

        apiClientMock.Verify(
            client => client.GetConversationHistoryAsync(
                It.IsAny<string>(),
                conversationId,
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "Telegram IA orchestrator | Open service request | Deve abrir pedido quando triagem estiver completa")]
    public async Task GenerateAssistantReplyAsync_ShouldCreateServiceRequest_WhenTriageIsComplete()
    {
        var conversationId = Guid.NewGuid();
        var createdRequestId = Guid.NewGuid();

        var gatewayMock = new Mock<ITelegramAiGateway>();
        gatewayMock
            .Setup(gateway => gateway.GenerateReplyAsync(It.IsAny<TelegramAiGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramAiGatewayResult(
                Success: true,
                OutputText: "{\"messageToClient\":\"Perfeito, vou registrar.\",\"intent\":\"open_service_request\",\"nextStep\":\"open_request\",\"confidence\":0.95,\"entities\":{\"category\":\"ar condicionado\",\"problemDescription\":\"Ar condicionado LG com erro CH26\",\"zipCode\":\"04567000\",\"city\":\"Sao Paulo\"}}",
                InputTokens: 150,
                OutputTokens: 80,
                TotalTokens: 230,
                AttemptCount: 1,
                LatencyMilliseconds: 110));

        var apiClientMock = BuildApiClientMock(conversationId);
        apiClientMock
            .Setup(client => client.CreateServiceRequestAsync(
                It.IsAny<string>(),
                It.Is<TelegramServiceRequestCreatePayload>(payload =>
                    payload.CategoryValue == 4 &&
                    payload.CategoryName == "Appliances" &&
                    payload.Zip == "04567-000"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramCreatedServiceRequestDto(createdRequestId))
            .Verifiable();

        var options = Options.Create(new TelegramBridgeAiOptions
        {
            Enabled = true,
            Provider = "OpenAI",
            Model = "gpt-4.1-mini",
            ApiKey = "test-key"
        });

        using var memoryCache = new MemoryCache(new MemoryCacheOptions());

        var orchestrator = new TelegramChatbotOrchestrator(
            gatewayMock.Object,
            apiClientMock.Object,
            options,
            memoryCache,
            Mock.Of<ILogger<TelegramChatbotOrchestrator>>(),
            new TelegramServiceRequestTriageEngine(),
            new TelegramSchedulingNaturalLanguageParser());

        var clientMessage = BuildClientMessage(
            conversationId: 99L,
            text: "Meu ar condicionado LG deu CH26. Meu CEP e 04567-000.",
            messageId: "m-open");

        var reply = await orchestrator.GenerateAssistantReplyAsync(
            "api-token",
            99L,
            clientMessage,
            "Atendimento Cliente",
            CancellationToken.None);

        Assert.NotNull(reply);
        Assert.Equal("schedule_visits", reply!.Intent);
        Assert.Equal("collect_visit_windows", reply.NextStep);
        Assert.Contains("me diga os dias e o periodo", reply.MessageText, StringComparison.OrdinalIgnoreCase);

        apiClientMock.Verify();
    }

    [Fact(DisplayName = "Telegram IA orchestrator | Automacao CPM Full | Deve acionar lead do funil apos criar pedido")]
    public async Task GenerateAssistantReplyAsync_ShouldTriggerTelegramLeadAutomation_WhenServiceRequestIsCreated()
    {
        var conversationId = Guid.NewGuid();
        var createdRequestId = Guid.NewGuid();
        var clientId = Guid.NewGuid();

        var gatewayMock = new Mock<ITelegramAiGateway>();
        gatewayMock
            .Setup(gateway => gateway.GenerateReplyAsync(It.IsAny<TelegramAiGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramAiGatewayResult(
                Success: true,
                OutputText: "{\"messageToClient\":\"Perfeito, vou registrar.\",\"intent\":\"open_service_request\",\"nextStep\":\"open_request\",\"confidence\":0.95,\"entities\":{\"category\":\"eletricista\",\"problemDescription\":\"Curto na cozinha\",\"zipCode\":\"11701200\",\"city\":\"Praia Grande\"}}",
                InputTokens: 120,
                OutputTokens: 70,
                TotalTokens: 190,
                AttemptCount: 1,
                LatencyMilliseconds: 95));

        var apiClientMock = BuildApiClientMock(conversationId);
        apiClientMock
            .Setup(client => client.CreateServiceRequestAsync(
                It.IsAny<string>(),
                It.IsAny<TelegramServiceRequestCreatePayload>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramCreatedServiceRequestDto(createdRequestId));

        var automationClientMock = new Mock<ITelegramLeadAutomationClient>();
        automationClientMock
            .Setup(client => client.UpsertClientLeadAsync(
                It.Is<TelegramLeadAutomationUpsertRequest>(request =>
                    request.BoardType == "clientes" &&
                    request.ChatbotConversationId == conversationId &&
                    request.ClientId == clientId &&
                    request.ClientEmail == "cliente.telegram@teste.com" &&
                    request.ServiceRequestId == createdRequestId &&
                    request.ServiceCategory.Contains("eletric", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramLeadAutomationUpsertResult
            {
                Success = true,
                HttpStatusCode = StatusCodes.Status200OK,
                LeadId = 42,
                Created = true,
                BoardType = "clientes",
                Message = "Lead criado via automacao Telegram.",
                ChatwootStatus = "synced",
                ChatwootMessage = "Lead sincronizado no Chatwoot."
            })
            .Verifiable();

        var options = Options.Create(new TelegramBridgeAiOptions
        {
            Enabled = true,
            Provider = "OpenAI",
            Model = "gpt-4.1-mini",
            ApiKey = "test-key"
        });
        var automationOptions = Options.Create(new TelegramAutomationOptions
        {
            Enabled = true,
            ClientsAutomationEnabled = true,
            CpmFullBaseUrl = "https://www.consertapramim.com",
            SharedSecret = "segredo"
        });

        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var orchestrator = new TelegramChatbotOrchestrator(
            gatewayMock.Object,
            apiClientMock.Object,
            options,
            memoryCache,
            Mock.Of<ILogger<TelegramChatbotOrchestrator>>(),
            new TelegramServiceRequestTriageEngine(),
            new TelegramSchedulingNaturalLanguageParser(),
            automationClientMock.Object,
            automationOptions);

        var clientMessage = BuildClientMessage(305L, "Preciso de eletricista para curto na cozinha em Praia Grande.", "m-open-automation");

        var reply = await orchestrator.GenerateAssistantReplyAsync(
            "api-token",
            305L,
            clientMessage,
            "Atendimento Cliente",
            CancellationToken.None,
            clientId,
            "cliente.telegram@teste.com");

        Assert.NotNull(reply);
        Assert.Equal("schedule_visits", reply!.Intent);
        automationClientMock.Verify();
    }

    [Fact(DisplayName = "Telegram IA orchestrator | Query orders | Deve listar pedidos em linguagem natural")]
    public async Task GenerateAssistantReplyAsync_ShouldListOrders_WhenClientAsksForOrders()
    {
        var conversationId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var gatewayMock = new Mock<ITelegramAiGateway>();
        gatewayMock
            .Setup(gateway => gateway.GenerateReplyAsync(It.IsAny<TelegramAiGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramAiGatewayResult(
                Success: true,
                OutputText: "{\"messageToClient\":\"Claro.\",\"intent\":\"unknown\",\"nextStep\":\"follow_up\",\"confidence\":0.7,\"entities\":{}}",
                InputTokens: 80,
                OutputTokens: 30,
                TotalTokens: 110,
                AttemptCount: 1,
                LatencyMilliseconds: 70));

        var apiClientMock = BuildApiClientMock(conversationId);
        apiClientMock
            .Setup(client => client.GetClientOrdersAsync(
                It.IsAny<string>(),
                0,
                3,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramChatbotOrdersResultDto(
                Success: true,
                Orders:
                [
                    new TelegramChatbotOrderSummaryDto(
                        ServiceRequestId: requestId,
                        Protocol: "eb14e91c",
                        Status: "Scheduled",
                        Category: "Hidraulica",
                        Description: "Torneira pingando",
                        City: "Praia Grande",
                        CreatedAtUtc: DateTime.UtcNow,
                        ProposalsCount: 2,
                        AcceptedProposalsCount: 1,
                        AppointmentsCount: 1)
                ],
                TotalCount: 1,
                Skip: 0,
                Take: 3,
                HasMore: false));

        var options = Options.Create(new TelegramBridgeAiOptions
        {
            Enabled = true,
            Provider = "OpenAI",
            Model = "gpt-4.1-mini",
            ApiKey = "test-key"
        });

        using var memoryCache = new MemoryCache(new MemoryCacheOptions());

        var orchestrator = new TelegramChatbotOrchestrator(
            gatewayMock.Object,
            apiClientMock.Object,
            options,
            memoryCache,
            Mock.Of<ILogger<TelegramChatbotOrchestrator>>(),
            new TelegramServiceRequestTriageEngine(),
            new TelegramSchedulingNaturalLanguageParser());

        var clientMessage = BuildClientMessage(200L, "quais pedidos eu tenho?", "m-q-orders");

        var reply = await orchestrator.GenerateAssistantReplyAsync(
            "api-token",
            200L,
            clientMessage,
            "Atendimento Cliente",
            CancellationToken.None);

        Assert.NotNull(reply);
        Assert.Equal("list_orders", reply!.Intent);
        Assert.Contains("#eb14e91c", reply.MessageText, StringComparison.OrdinalIgnoreCase);

        apiClientMock.Verify(client => client.GetClientOrdersAsync(
            It.IsAny<string>(),
            0,
            3,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Telegram IA orchestrator | Query status | Deve usar pedido atual em contexto")]
    public async Task GenerateAssistantReplyAsync_ShouldGetOrderStatus_UsingContextReference()
    {
        var conversationId = Guid.NewGuid();
        var serviceRequestId = Guid.NewGuid();

        var gatewayMock = new Mock<ITelegramAiGateway>();
        gatewayMock
            .Setup(gateway => gateway.GenerateReplyAsync(It.IsAny<TelegramAiGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramAiGatewayResult(
                Success: true,
                OutputText: "{\"messageToClient\":\"Claro.\",\"intent\":\"get_order_status\",\"nextStep\":\"follow_up\",\"confidence\":0.8,\"entities\":{}}",
                InputTokens: 90,
                OutputTokens: 35,
                TotalTokens: 125,
                AttemptCount: 1,
                LatencyMilliseconds: 60));

        var history = new TelegramChatbotConversationHistoryDto(
            Conversation: new TelegramChatbotConversationDto(
                Id: conversationId,
                ClientId: Guid.NewGuid(),
                Channel: "telegram",
                ChannelConversationId: "chat-id",
                Status: 1,
                StartedAtUtc: DateTime.UtcNow,
                LastInteractionAtUtc: DateTime.UtcNow,
                LastIntent: "list_orders",
                LastStep: "query_orders_listed",
                MetadataJson: null),
            Messages: [],
            ContextSnapshots:
            [
                new TelegramChatbotContextSnapshotDto(
                    Id: Guid.NewGuid(),
                    ConversationId: conversationId,
                    ClientId: Guid.NewGuid(),
                    SnapshotType: "query_reference_state",
                    ContextJson: $"{{\"currentServiceRequestId\":\"{serviceRequestId:D}\",\"currentProtocol\":\"eb14e91c\",\"lastListedOrders\":[{{\"serviceRequestId\":\"{serviceRequestId:D}\",\"protocol\":\"eb14e91c\"}}],\"lastQueryIntent\":\"list_orders\",\"lastOrdersSkip\":0,\"lastOrdersTake\":3,\"lastAppointmentsSkip\":0,\"lastAppointmentsTake\":3,\"updatedAtUtc\":\"{DateTime.UtcNow:O}\"}}",
                    PromptVersion: "v1",
                    ModelName: "gpt-4.1-mini",
                    PromptTokens: null,
                    CompletionTokens: null,
                    TotalTokens: null,
                    CapturedAtUtc: DateTime.UtcNow.AddMinutes(-1))
            ],
            ActionLogs: []);

        var apiClientMock = BuildApiClientMock(conversationId, history);
        apiClientMock
            .Setup(client => client.GetOrderStatusAsync(
                It.IsAny<string>(),
                serviceRequestId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramChatbotOrderStatusResultDto(
                Success: true,
                ServiceRequestId: serviceRequestId,
                Protocol: "eb14e91c",
                Status: "Scheduled",
                ProposalsCount: 2,
                AcceptedProposalsCount: 1,
                AppointmentsCount: 1));

        var options = Options.Create(new TelegramBridgeAiOptions
        {
            Enabled = true,
            Provider = "OpenAI",
            Model = "gpt-4.1-mini",
            ApiKey = "test-key"
        });

        using var memoryCache = new MemoryCache(new MemoryCacheOptions());

        var orchestrator = new TelegramChatbotOrchestrator(
            gatewayMock.Object,
            apiClientMock.Object,
            options,
            memoryCache,
            Mock.Of<ILogger<TelegramChatbotOrchestrator>>(),
            new TelegramServiceRequestTriageEngine(),
            new TelegramSchedulingNaturalLanguageParser());

        var clientMessage = BuildClientMessage(201L, "como esta meu pedido?", "m-q-status");

        var reply = await orchestrator.GenerateAssistantReplyAsync(
            "api-token",
            201L,
            clientMessage,
            "Atendimento Cliente",
            CancellationToken.None);

        Assert.NotNull(reply);
        Assert.Equal("get_order_status", reply!.Intent);
        Assert.Contains("#eb14e91c", reply.MessageText, StringComparison.OrdinalIgnoreCase);

        apiClientMock.Verify(client => client.GetOrderStatusAsync(
            It.IsAny<string>(),
            serviceRequestId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Telegram IA orchestrator | Query details | Deve resolver protocolo do historico recente")]
    public async Task GenerateAssistantReplyAsync_ShouldGetOrderDetails_FromProtocolReference()
    {
        var conversationId = Guid.NewGuid();
        var serviceRequestId = Guid.NewGuid();

        var gatewayMock = new Mock<ITelegramAiGateway>();
        gatewayMock
            .Setup(gateway => gateway.GenerateReplyAsync(It.IsAny<TelegramAiGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramAiGatewayResult(
                Success: true,
                OutputText: "{\"messageToClient\":\"Claro.\",\"intent\":\"unknown\",\"nextStep\":\"follow_up\",\"confidence\":0.8,\"entities\":{}}",
                InputTokens: 70,
                OutputTokens: 40,
                TotalTokens: 110,
                AttemptCount: 1,
                LatencyMilliseconds: 60));

        var history = new TelegramChatbotConversationHistoryDto(
            Conversation: new TelegramChatbotConversationDto(
                Id: conversationId,
                ClientId: Guid.NewGuid(),
                Channel: "telegram",
                ChannelConversationId: "chat-id",
                Status: 1,
                StartedAtUtc: DateTime.UtcNow,
                LastInteractionAtUtc: DateTime.UtcNow,
                LastIntent: "list_orders",
                LastStep: "query_orders_listed",
                MetadataJson: null),
            Messages: [],
            ContextSnapshots:
            [
                new TelegramChatbotContextSnapshotDto(
                    Id: Guid.NewGuid(),
                    ConversationId: conversationId,
                    ClientId: Guid.NewGuid(),
                    SnapshotType: "query_reference_state",
                    ContextJson: $"{{\"currentServiceRequestId\":\"{serviceRequestId:D}\",\"currentProtocol\":\"eb14e91c\",\"lastListedOrders\":[{{\"serviceRequestId\":\"{serviceRequestId:D}\",\"protocol\":\"eb14e91c\"}}],\"lastQueryIntent\":\"list_orders\",\"lastOrdersSkip\":0,\"lastOrdersTake\":3,\"lastAppointmentsSkip\":0,\"lastAppointmentsTake\":3,\"updatedAtUtc\":\"{DateTime.UtcNow:O}\"}}",
                    PromptVersion: "v1",
                    ModelName: "gpt-4.1-mini",
                    PromptTokens: null,
                    CompletionTokens: null,
                    TotalTokens: null,
                    CapturedAtUtc: DateTime.UtcNow.AddMinutes(-1))
            ],
            ActionLogs: []);

        var apiClientMock = BuildApiClientMock(conversationId, history);
        apiClientMock
            .Setup(client => client.GetOrderDetailsAsync(
                It.IsAny<string>(),
                serviceRequestId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramChatbotOrderDetailsResultDto(
                Success: true,
                ServiceRequestId: serviceRequestId,
                Details: new TelegramChatbotOrderDetailsDto(
                    ServiceRequestId: serviceRequestId,
                    Protocol: "eb14e91c",
                    Status: "Scheduled",
                    Category: "Hidraulica",
                    Description: "Torneira pingando muito",
                    Street: "Rua A",
                    City: "Praia Grande",
                    Zip: "11704150",
                    CreatedAtUtc: DateTime.UtcNow,
                    Proposals: [],
                    Appointments: [])));

        var options = Options.Create(new TelegramBridgeAiOptions
        {
            Enabled = true,
            Provider = "OpenAI",
            Model = "gpt-4.1-mini",
            ApiKey = "test-key"
        });

        using var memoryCache = new MemoryCache(new MemoryCacheOptions());

        var orchestrator = new TelegramChatbotOrchestrator(
            gatewayMock.Object,
            apiClientMock.Object,
            options,
            memoryCache,
            Mock.Of<ILogger<TelegramChatbotOrchestrator>>(),
            new TelegramServiceRequestTriageEngine(),
            new TelegramSchedulingNaturalLanguageParser());

        var clientMessage = BuildClientMessage(202L, "me mostra detalhes do protocolo #eb14e91c", "m-q-details");

        var reply = await orchestrator.GenerateAssistantReplyAsync(
            "api-token",
            202L,
            clientMessage,
            "Atendimento Cliente",
            CancellationToken.None);

        Assert.NotNull(reply);
        Assert.Equal("get_order_details", reply!.Intent);
        Assert.Contains("Detalhes do pedido #eb14e91c", reply.MessageText, StringComparison.OrdinalIgnoreCase);

        apiClientMock.Verify(client => client.GetOrderDetailsAsync(
            It.IsAny<string>(),
            serviceRequestId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Telegram IA orchestrator | Query appointments | Deve responder amigavel quando agenda vazia")]
    public async Task GenerateAssistantReplyAsync_ShouldReturnFriendlyMessage_WhenNoAppointmentsFound()
    {
        var conversationId = Guid.NewGuid();

        var gatewayMock = new Mock<ITelegramAiGateway>();
        gatewayMock
            .Setup(gateway => gateway.GenerateReplyAsync(It.IsAny<TelegramAiGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramAiGatewayResult(
                Success: true,
                OutputText: "{\"messageToClient\":\"Claro.\",\"intent\":\"list_appointments\",\"nextStep\":\"follow_up\",\"confidence\":0.8,\"entities\":{}}",
                InputTokens: 60,
                OutputTokens: 30,
                TotalTokens: 90,
                AttemptCount: 1,
                LatencyMilliseconds: 50));

        var apiClientMock = BuildApiClientMock(conversationId);
        apiClientMock
            .Setup(client => client.GetClientAppointmentsAsync(
                It.IsAny<string>(),
                null,
                null,
                0,
                3,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramChatbotAppointmentsResultDto(
                Success: true,
                Appointments: [],
                TotalCount: 0,
                Skip: 0,
                Take: 3,
                HasMore: false));

        var options = Options.Create(new TelegramBridgeAiOptions
        {
            Enabled = true,
            Provider = "OpenAI",
            Model = "gpt-4.1-mini",
            ApiKey = "test-key"
        });

        using var memoryCache = new MemoryCache(new MemoryCacheOptions());

        var orchestrator = new TelegramChatbotOrchestrator(
            gatewayMock.Object,
            apiClientMock.Object,
            options,
            memoryCache,
            Mock.Of<ILogger<TelegramChatbotOrchestrator>>(),
            new TelegramServiceRequestTriageEngine(),
            new TelegramSchedulingNaturalLanguageParser());

        var clientMessage = BuildClientMessage(203L, "quais agendamentos tenho?", "m-q-appts");

        var reply = await orchestrator.GenerateAssistantReplyAsync(
            "api-token",
            203L,
            clientMessage,
            "Atendimento Cliente",
            CancellationToken.None);

        Assert.NotNull(reply);
        Assert.Equal("list_appointments", reply!.Intent);
        Assert.Contains("nao tem agendamentos", reply.MessageText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Telegram IA orchestrator | Query pagination | Deve usar contexto para buscar mais pedidos")]
    public async Task GenerateAssistantReplyAsync_ShouldPaginateOrders_WhenClientAsksForMore()
    {
        var conversationId = Guid.NewGuid();

        var gatewayMock = new Mock<ITelegramAiGateway>();
        gatewayMock
            .Setup(gateway => gateway.GenerateReplyAsync(It.IsAny<TelegramAiGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramAiGatewayResult(
                Success: true,
                OutputText: "{\"messageToClient\":\"Claro.\",\"intent\":\"unknown\",\"nextStep\":\"follow_up\",\"confidence\":0.7,\"entities\":{}}",
                InputTokens: 60,
                OutputTokens: 20,
                TotalTokens: 80,
                AttemptCount: 1,
                LatencyMilliseconds: 40));

        var history = new TelegramChatbotConversationHistoryDto(
            Conversation: new TelegramChatbotConversationDto(
                Id: conversationId,
                ClientId: Guid.NewGuid(),
                Channel: "telegram",
                ChannelConversationId: "chat-id",
                Status: 1,
                StartedAtUtc: DateTime.UtcNow,
                LastInteractionAtUtc: DateTime.UtcNow,
                LastIntent: "list_orders",
                LastStep: "query_orders_listed",
                MetadataJson: null),
            Messages: [],
            ContextSnapshots:
            [
                new TelegramChatbotContextSnapshotDto(
                    Id: Guid.NewGuid(),
                    ConversationId: conversationId,
                    ClientId: Guid.NewGuid(),
                    SnapshotType: "query_reference_state",
                    ContextJson: $"{{\"currentServiceRequestId\":null,\"currentProtocol\":null,\"lastListedOrders\":[],\"lastQueryIntent\":\"list_orders\",\"lastOrdersSkip\":0,\"lastOrdersTake\":3,\"lastAppointmentsSkip\":0,\"lastAppointmentsTake\":3,\"updatedAtUtc\":\"{DateTime.UtcNow:O}\"}}",
                    PromptVersion: "v1",
                    ModelName: "gpt-4.1-mini",
                    PromptTokens: null,
                    CompletionTokens: null,
                    TotalTokens: null,
                    CapturedAtUtc: DateTime.UtcNow.AddMinutes(-1))
            ],
            ActionLogs: []);

        var apiClientMock = BuildApiClientMock(conversationId, history);
        apiClientMock
            .Setup(client => client.GetClientOrdersAsync(
                It.IsAny<string>(),
                3,
                3,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramChatbotOrdersResultDto(
                Success: true,
                Orders: [],
                TotalCount: 3,
                Skip: 3,
                Take: 3,
                HasMore: false));

        var options = Options.Create(new TelegramBridgeAiOptions
        {
            Enabled = true,
            Provider = "OpenAI",
            Model = "gpt-4.1-mini",
            ApiKey = "test-key"
        });

        using var memoryCache = new MemoryCache(new MemoryCacheOptions());

        var orchestrator = new TelegramChatbotOrchestrator(
            gatewayMock.Object,
            apiClientMock.Object,
            options,
            memoryCache,
            Mock.Of<ILogger<TelegramChatbotOrchestrator>>(),
            new TelegramServiceRequestTriageEngine(),
            new TelegramSchedulingNaturalLanguageParser());

        var clientMessage = BuildClientMessage(204L, "mostrar mais pedidos", "m-q-more");

        var reply = await orchestrator.GenerateAssistantReplyAsync(
            "api-token",
            204L,
            clientMessage,
            "Atendimento Cliente",
            CancellationToken.None);

        Assert.NotNull(reply);
        Assert.Equal("list_orders", reply!.Intent);
        Assert.Contains("Nao encontrei mais pedidos", reply.MessageText, StringComparison.OrdinalIgnoreCase);

        apiClientMock.Verify(client => client.GetClientOrdersAsync(
            It.IsAny<string>(),
            3,
            3,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Telegram IA orchestrator | Schedule visits | Deve agendar lote a partir de linguagem natural")]
    public async Task GenerateAssistantReplyAsync_ShouldScheduleBatchVisits_WhenMessageContainsNaturalWindows()
    {
        var conversationId = Guid.NewGuid();
        var serviceRequestId = Guid.NewGuid();
        var providerA = Guid.NewGuid();
        var providerB = Guid.NewGuid();

        var gatewayMock = new Mock<ITelegramAiGateway>();
        gatewayMock
            .Setup(gateway => gateway.GenerateReplyAsync(It.IsAny<TelegramAiGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramAiGatewayResult(
                Success: true,
                OutputText: $"{{\"messageToClient\":\"Perfeito, vou agendar.\",\"intent\":\"schedule_visits\",\"nextStep\":\"collect_visit_windows\",\"confidence\":0.92,\"entities\":{{\"serviceRequestId\":\"{serviceRequestId:D}\"}}}}",
                InputTokens: 90,
                OutputTokens: 50,
                TotalTokens: 140,
                AttemptCount: 1,
                LatencyMilliseconds: 80));

        var history = new TelegramChatbotConversationHistoryDto(
            Conversation: new TelegramChatbotConversationDto(
                Id: conversationId,
                ClientId: Guid.NewGuid(),
                Channel: "telegram",
                ChannelConversationId: "chat-id",
                Status: 1,
                StartedAtUtc: DateTime.UtcNow,
                LastInteractionAtUtc: DateTime.UtcNow,
                LastIntent: "open_service_request",
                LastStep: "service_request_created",
                MetadataJson: null),
            Messages: [],
            ContextSnapshots:
            [
                new TelegramChatbotContextSnapshotDto(
                    Id: Guid.NewGuid(),
                    ConversationId: conversationId,
                    ClientId: Guid.NewGuid(),
                    SnapshotType: "service_request_triage_state",
                    ContextJson: $"{{\"state\":{{\"serviceRequestId\":\"{serviceRequestId:D}\",\"categoryRaw\":\"ar condicionado\",\"categoryEnum\":\"Appliances\",\"problemDescription\":\"erro CH26\",\"zipCode\":\"04567-000\"}}}}",
                    PromptVersion: "v1",
                    ModelName: "gpt-4.1-mini",
                    PromptTokens: null,
                    CompletionTokens: null,
                    TotalTokens: null,
                    CapturedAtUtc: DateTime.UtcNow.AddMinutes(-1))
            ],
            ActionLogs: []);

        var apiClientMock = BuildApiClientMock(conversationId, history);
        apiClientMock
            .Setup(client => client.GetEligibleProvidersAsync(
                It.IsAny<string>(),
                serviceRequestId,
                5,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramChatbotEligibleProvidersResultDto(
                Success: true,
                ServiceRequestId: serviceRequestId,
                Providers:
                [
                    new TelegramChatbotEligibleProviderDto(providerA, "Tecnico A", 1.2, 4.8, 120, 15, [4]),
                    new TelegramChatbotEligibleProviderDto(providerB, "Tecnico B", 2.1, 4.7, 98, 20, [4])
                ]));

        apiClientMock
            .Setup(client => client.GetProviderAvailableSlotsAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string token, Guid providerId, DateTime fromUtc, DateTime toUtc, int? durationMinutes, CancellationToken cancellationToken) =>
                (IReadOnlyList<TelegramServiceAppointmentSlotDto>)
                [
                    new TelegramServiceAppointmentSlotDto(fromUtc, toUtc)
                ]);

        apiClientMock
            .Setup(client => client.ScheduleVisitsBatchAsync(
                It.IsAny<string>(),
                serviceRequestId,
                It.Is<IReadOnlyList<TelegramChatbotBatchScheduleVisitRequestDto>>(visits =>
                    visits.Count == 2 &&
                    visits[0].ProviderId == providerA &&
                    visits[1].ProviderId == providerB),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramChatbotBatchScheduleResultDto(
                Success: true,
                ServiceRequestId: serviceRequestId,
                Results:
                [
                    new TelegramChatbotBatchScheduleVisitResultDto(
                        ProviderId: providerA,
                        WindowStartUtc: DateTime.UtcNow.AddDays(1),
                        WindowEndUtc: DateTime.UtcNow.AddDays(1).AddHours(2),
                        Success: true,
                        AppointmentId: Guid.NewGuid()),
                    new TelegramChatbotBatchScheduleVisitResultDto(
                        ProviderId: providerB,
                        WindowStartUtc: DateTime.UtcNow.AddDays(3),
                        WindowEndUtc: DateTime.UtcNow.AddDays(3).AddHours(2),
                        Success: true,
                        AppointmentId: Guid.NewGuid())
                ]));

        var options = Options.Create(new TelegramBridgeAiOptions
        {
            Enabled = true,
            Provider = "OpenAI",
            Model = "gpt-4.1-mini",
            ApiKey = "test-key"
        });

        using var memoryCache = new MemoryCache(new MemoryCacheOptions());

        var orchestrator = new TelegramChatbotOrchestrator(
            gatewayMock.Object,
            apiClientMock.Object,
            options,
            memoryCache,
            Mock.Of<ILogger<TelegramChatbotOrchestrator>>(),
            new TelegramServiceRequestTriageEngine(),
            new TelegramSchedulingNaturalLanguageParser());

        var clientMessage = BuildClientMessage(
            conversationId: 123L,
            text: "Sim, pode ser com 2 prestadores na quarta e na sexta feira, no periodo da manha.",
            messageId: "m-schedule");

        var reply = await orchestrator.GenerateAssistantReplyAsync(
            "api-token",
            123L,
            clientMessage,
            "Atendimento Cliente",
            CancellationToken.None);

        Assert.NotNull(reply);
        Assert.Equal("schedule_visits", reply!.Intent);
        Assert.Equal("visits_scheduled", reply.NextStep);
        Assert.Contains("Agendamentos solicitados com sucesso", reply.MessageText, StringComparison.OrdinalIgnoreCase);

        apiClientMock.Verify(client => client.ScheduleVisitsBatchAsync(
            It.IsAny<string>(),
            serviceRequestId,
            It.IsAny<IReadOnlyList<TelegramChatbotBatchScheduleVisitRequestDto>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Telegram IA orchestrator | Scheduling status | Deve informar ausencia de agendamentos e listar prestadores")]
    public async Task GenerateAssistantReplyAsync_ShouldAnswerSchedulingStatusAndSuggestProviders_WhenNoBatchExists()
    {
        var conversationId = Guid.NewGuid();
        var serviceRequestId = Guid.NewGuid();
        var providerId = Guid.NewGuid();

        var gatewayMock = new Mock<ITelegramAiGateway>();
        gatewayMock
            .Setup(gateway => gateway.GenerateReplyAsync(It.IsAny<TelegramAiGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramAiGatewayResult(
                Success: true,
                OutputText: "{\"messageToClient\":\"Claro, vou verificar.\",\"intent\":\"unknown\",\"nextStep\":\"follow_up\",\"confidence\":0.8,\"entities\":{}}",
                InputTokens: 80,
                OutputTokens: 40,
                TotalTokens: 120,
                AttemptCount: 1,
                LatencyMilliseconds: 60));

        var history = new TelegramChatbotConversationHistoryDto(
            Conversation: new TelegramChatbotConversationDto(
                Id: conversationId,
                ClientId: Guid.NewGuid(),
                Channel: "telegram",
                ChannelConversationId: "chat-id",
                Status: 1,
                StartedAtUtc: DateTime.UtcNow,
                LastInteractionAtUtc: DateTime.UtcNow,
                LastIntent: "open_service_request",
                LastStep: "service_request_created",
                MetadataJson: null),
            Messages: [],
            ContextSnapshots:
            [
                new TelegramChatbotContextSnapshotDto(
                    Id: Guid.NewGuid(),
                    ConversationId: conversationId,
                    ClientId: Guid.NewGuid(),
                    SnapshotType: "service_request_triage_state",
                    ContextJson: $"{{\"state\":{{\"serviceRequestId\":\"{serviceRequestId:D}\",\"categoryRaw\":\"ar condicionado\",\"categoryEnum\":\"Appliances\",\"problemDescription\":\"erro CH26\",\"zipCode\":\"04567-000\"}}}}",
                    PromptVersion: "v1",
                    ModelName: "gpt-4.1-mini",
                    PromptTokens: null,
                    CompletionTokens: null,
                    TotalTokens: null,
                    CapturedAtUtc: DateTime.UtcNow.AddMinutes(-3))
            ],
            ActionLogs: []);

        var apiClientMock = BuildApiClientMock(conversationId, history);
        apiClientMock
            .Setup(client => client.GetEligibleProvidersAsync(
                It.IsAny<string>(),
                serviceRequestId,
                5,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramChatbotEligibleProvidersResultDto(
                Success: true,
                ServiceRequestId: serviceRequestId,
                Providers:
                [
                    new TelegramChatbotEligibleProviderDto(providerId, "Tecnico Status", 1.3, 4.9, 44, 20, [4])
                ]));

        var options = Options.Create(new TelegramBridgeAiOptions
        {
            Enabled = true,
            Provider = "OpenAI",
            Model = "gpt-4.1-mini",
            ApiKey = "test-key"
        });

        using var memoryCache = new MemoryCache(new MemoryCacheOptions());

        var orchestrator = new TelegramChatbotOrchestrator(
            gatewayMock.Object,
            apiClientMock.Object,
            options,
            memoryCache,
            Mock.Of<ILogger<TelegramChatbotOrchestrator>>(),
            new TelegramServiceRequestTriageEngine(),
            new TelegramSchedulingNaturalLanguageParser());

        var clientMessage = BuildClientMessage(
            conversationId: 150L,
            text: "Ja foi agendado?",
            messageId: "m-status");

        var reply = await orchestrator.GenerateAssistantReplyAsync(
            "api-token",
            150L,
            clientMessage,
            "Atendimento Cliente",
            CancellationToken.None);

        Assert.NotNull(reply);
        Assert.Equal("schedule_visits", reply!.Intent);
        Assert.Equal("schedule_status_unavailable", reply.NextStep);
        Assert.Contains("Ainda nao tenho visitas agendadas", reply.MessageText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Encontrei estes prestadores", reply.MessageText, StringComparison.OrdinalIgnoreCase);

        apiClientMock.Verify(client => client.GetEligibleProvidersAsync(
            It.IsAny<string>(),
            serviceRequestId,
            5,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Telegram IA orchestrator | Scheduling guardrail | Deve bloquear confirmacao sem agendamento persistido")]
    public async Task GenerateAssistantReplyAsync_ShouldApplyGuardrail_WhenAiClaimsSchedulingWithoutPersistence()
    {
        var conversationId = Guid.NewGuid();
        var serviceRequestId = Guid.NewGuid();

        var gatewayMock = new Mock<ITelegramAiGateway>();
        gatewayMock
            .Setup(gateway => gateway.GenerateReplyAsync(It.IsAny<TelegramAiGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramAiGatewayResult(
                Success: true,
                OutputText: "{\"messageToClient\":\"Agendei sua visita para segunda-feira de manha.\",\"intent\":\"schedule_visits\",\"nextStep\":\"finalize_scheduling\",\"confidence\":0.9,\"entities\":{}}",
                InputTokens: 90,
                OutputTokens: 30,
                TotalTokens: 120,
                AttemptCount: 1,
                LatencyMilliseconds: 70));

        var history = new TelegramChatbotConversationHistoryDto(
            Conversation: new TelegramChatbotConversationDto(
                Id: conversationId,
                ClientId: Guid.NewGuid(),
                Channel: "telegram",
                ChannelConversationId: "chat-id",
                Status: 1,
                StartedAtUtc: DateTime.UtcNow,
                LastInteractionAtUtc: DateTime.UtcNow,
                LastIntent: "schedule_visits",
                LastStep: "collect_visit_windows",
                MetadataJson: null),
            Messages: [],
            ContextSnapshots:
            [
                new TelegramChatbotContextSnapshotDto(
                    Id: Guid.NewGuid(),
                    ConversationId: conversationId,
                    ClientId: Guid.NewGuid(),
                    SnapshotType: "service_request_triage_state",
                    ContextJson: $"{{\"state\":{{\"serviceRequestId\":\"{serviceRequestId:D}\",\"categoryRaw\":\"hidraulica\",\"categoryEnum\":\"Plumbing\",\"problemDescription\":\"torneira pingando\",\"zipCode\":\"11704-150\"}}}}",
                    PromptVersion: "v1",
                    ModelName: "gpt-4.1-mini",
                    PromptTokens: null,
                    CompletionTokens: null,
                    TotalTokens: null,
                    CapturedAtUtc: DateTime.UtcNow.AddMinutes(-2))
            ],
            ActionLogs: []);

        var apiClientMock = BuildApiClientMock(conversationId, history);

        var options = Options.Create(new TelegramBridgeAiOptions
        {
            Enabled = true,
            Provider = "OpenAI",
            Model = "gpt-4.1-mini",
            ApiKey = "test-key"
        });

        using var memoryCache = new MemoryCache(new MemoryCacheOptions());

        var orchestrator = new TelegramChatbotOrchestrator(
            gatewayMock.Object,
            apiClientMock.Object,
            options,
            memoryCache,
            Mock.Of<ILogger<TelegramChatbotOrchestrator>>(),
            new TelegramServiceRequestTriageEngine(),
            new TelegramSchedulingNaturalLanguageParser());

        var clientMessage = BuildClientMessage(
            conversationId: 180L,
            text: "sim",
            messageId: "m-guardrail");

        var reply = await orchestrator.GenerateAssistantReplyAsync(
            "api-token",
            180L,
            clientMessage,
            "Atendimento Cliente",
            CancellationToken.None);

        Assert.NotNull(reply);
        Assert.Equal("schedule_visits", reply!.Intent);
        Assert.Equal("awaiting_provider_confirmation", reply.NextStep);
        Assert.Contains("precisa de uma acao do prestador", reply.MessageText, StringComparison.OrdinalIgnoreCase);

        apiClientMock.Verify(client => client.GetEligibleProvidersAsync(
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "Telegram IA orchestrator | Scheduling guardrail | Deve bloquear confirmacao quando lookup de status falha sem persistencia")]
    public async Task GenerateAssistantReplyAsync_ShouldApplyGuardrail_WhenStatusLookupFailsAndAiClaimsScheduling()
    {
        var conversationId = Guid.NewGuid();
        var serviceRequestId = Guid.NewGuid();

        var gatewayMock = new Mock<ITelegramAiGateway>();
        gatewayMock
            .Setup(gateway => gateway.GenerateReplyAsync(It.IsAny<TelegramAiGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramAiGatewayResult(
                Success: true,
                OutputText: "{\"messageToClient\":\"Sim, suas visitas foram agendadas para semana que vem.\",\"intent\":\"schedule_visits\",\"nextStep\":\"schedule_status\",\"confidence\":0.9,\"entities\":{}}",
                InputTokens: 90,
                OutputTokens: 30,
                TotalTokens: 120,
                AttemptCount: 1,
                LatencyMilliseconds: 70));

        var history = new TelegramChatbotConversationHistoryDto(
            Conversation: new TelegramChatbotConversationDto(
                Id: conversationId,
                ClientId: Guid.NewGuid(),
                Channel: "telegram",
                ChannelConversationId: "chat-id",
                Status: 1,
                StartedAtUtc: DateTime.UtcNow,
                LastInteractionAtUtc: DateTime.UtcNow,
                LastIntent: "schedule_visits",
                LastStep: "schedule_status",
                MetadataJson: null),
            Messages: [],
            ContextSnapshots:
            [
                new TelegramChatbotContextSnapshotDto(
                    Id: Guid.NewGuid(),
                    ConversationId: conversationId,
                    ClientId: Guid.NewGuid(),
                    SnapshotType: "service_request_triage_state",
                    ContextJson: $"{{\"state\":{{\"serviceRequestId\":\"{serviceRequestId:D}\",\"categoryRaw\":\"hidraulica\",\"categoryEnum\":\"Plumbing\",\"problemDescription\":\"torneira pingando\",\"zipCode\":\"11704-150\"}}}}",
                    PromptVersion: "v1",
                    ModelName: "gpt-4.1-mini",
                    PromptTokens: null,
                    CompletionTokens: null,
                    TotalTokens: null,
                    CapturedAtUtc: DateTime.UtcNow.AddMinutes(-2))
            ],
            ActionLogs: []);

        var apiClientMock = BuildApiClientMock(conversationId, history);
        apiClientMock
            .Setup(client => client.GetEligibleProvidersAsync(
                It.IsAny<string>(),
                serviceRequestId,
                5,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramChatbotEligibleProvidersResultDto(
                Success: false,
                ServiceRequestId: serviceRequestId,
                Providers: [],
                ErrorCode: "provider_lookup_failed",
                ErrorMessage: "Falha de rede"));

        var options = Options.Create(new TelegramBridgeAiOptions
        {
            Enabled = true,
            Provider = "OpenAI",
            Model = "gpt-4.1-mini",
            ApiKey = "test-key"
        });

        using var memoryCache = new MemoryCache(new MemoryCacheOptions());

        var orchestrator = new TelegramChatbotOrchestrator(
            gatewayMock.Object,
            apiClientMock.Object,
            options,
            memoryCache,
            Mock.Of<ILogger<TelegramChatbotOrchestrator>>(),
            new TelegramServiceRequestTriageEngine(),
            new TelegramSchedulingNaturalLanguageParser());

        var clientMessage = BuildClientMessage(
            conversationId: 181L,
            text: "ja foi agendado?",
            messageId: "m-guardrail-status");

        var reply = await orchestrator.GenerateAssistantReplyAsync(
            "api-token",
            181L,
            clientMessage,
            "Atendimento Cliente",
            CancellationToken.None);

        Assert.NotNull(reply);
        Assert.Equal("schedule_visits", reply!.Intent);
        Assert.Equal("awaiting_provider_confirmation", reply.NextStep);
        Assert.Contains("precisa de uma acao do prestador", reply.MessageText, StringComparison.OrdinalIgnoreCase);

        apiClientMock.Verify(client => client.GetEligibleProvidersAsync(
            It.IsAny<string>(),
            serviceRequestId,
            5,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<ITelegramChatbotApiClient> BuildApiClientMock(
        Guid conversationId,
        TelegramChatbotConversationHistoryDto? history = null)
    {
        var apiClientMock = new Mock<ITelegramChatbotApiClient>();

        apiClientMock
            .Setup(client => client.OpenOrResumeSessionAsync(
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversationId);

        apiClientMock
            .Setup(client => client.GetConversationHistoryAsync(
                It.IsAny<string>(),
                conversationId,
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(history ?? new TelegramChatbotConversationHistoryDto(
                Conversation: new TelegramChatbotConversationDto(
                    Id: conversationId,
                    ClientId: Guid.NewGuid(),
                    Channel: "telegram",
                    ChannelConversationId: "chat-id",
                    Status: 1,
                    StartedAtUtc: DateTime.UtcNow,
                    LastInteractionAtUtc: DateTime.UtcNow,
                    LastIntent: "unknown",
                    LastStep: "collect_missing_data",
                    MetadataJson: null),
                Messages: new List<TelegramChatbotMessageDto>(),
                ContextSnapshots: new List<TelegramChatbotContextSnapshotDto>(),
                ActionLogs: new List<TelegramChatbotActionLogDto>()));

        apiClientMock
            .Setup(client => client.RegisterContextSnapshotAsync(
                It.IsAny<string>(),
                conversationId,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        apiClientMock
            .Setup(client => client.RegisterActionAsync(
                It.IsAny<string>(),
                conversationId,
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        apiClientMock
            .Setup(client => client.UpdateConversationStateAsync(
                It.IsAny<string>(),
                conversationId,
                It.IsAny<int?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        apiClientMock
            .Setup(client => client.CreateServiceRequestAsync(
                It.IsAny<string>(),
                It.IsAny<TelegramServiceRequestCreatePayload>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((TelegramCreatedServiceRequestDto?)null);

        apiClientMock
            .Setup(client => client.GetClientOrdersAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramChatbotOrdersResultDto(
                Success: true,
                Orders: [],
                TotalCount: 0,
                Skip: 0,
                Take: 3,
                HasMore: false));

        apiClientMock
            .Setup(client => client.GetOrderStatusAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((TelegramChatbotOrderStatusResultDto?)null);

        apiClientMock
            .Setup(client => client.GetOrderDetailsAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((TelegramChatbotOrderDetailsResultDto?)null);

        apiClientMock
            .Setup(client => client.GetClientAppointmentsAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramChatbotAppointmentsResultDto(
                Success: true,
                Appointments: [],
                TotalCount: 0,
                Skip: 0,
                Take: 3,
                HasMore: false));

        apiClientMock
            .Setup(client => client.GetEligibleProvidersAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramChatbotEligibleProvidersResultDto(
                Success: true,
                ServiceRequestId: Guid.NewGuid(),
                Providers: []));

        apiClientMock
            .Setup(client => client.ScheduleVisitsBatchAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyList<TelegramChatbotBatchScheduleVisitRequestDto>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((TelegramChatbotBatchScheduleResultDto?)null);

        return apiClientMock;
    }

    private static ChatMessageDto BuildClientMessage(long conversationId, string text, string messageId)
    {
        return new ChatMessageDto(
            Id: messageId,
            ChatId: conversationId,
            IsOutgoing: true,
            SenderDisplayName: "Cliente",
            Text: text,
            SentAtUtc: DateTimeOffset.UtcNow,
            Attachments: []);
    }
}
