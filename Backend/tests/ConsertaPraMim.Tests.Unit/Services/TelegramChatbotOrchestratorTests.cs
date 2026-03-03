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
            new TelegramServiceRequestTriageEngine());

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
            new TelegramServiceRequestTriageEngine());

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
                    payload.Category == "Appliances" &&
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
            new TelegramServiceRequestTriageEngine());

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
        Assert.Equal("open_service_request", reply!.Intent);
        Assert.Equal("service_request_created", reply.NextStep);
        Assert.Contains("Registrei seu pedido", reply.MessageText, StringComparison.OrdinalIgnoreCase);

        apiClientMock.Verify();
    }

    private static Mock<ITelegramChatbotApiClient> BuildApiClientMock(Guid conversationId)
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
            .ReturnsAsync(new TelegramChatbotConversationHistoryDto(
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
