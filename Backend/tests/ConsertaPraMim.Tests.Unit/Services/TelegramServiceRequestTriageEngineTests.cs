using ConsertaPraMim.Web.TelegramBridge.Models;
using ConsertaPraMim.Web.TelegramBridge.Services;

namespace ConsertaPraMim.Tests.Unit.Services;

public class TelegramServiceRequestTriageEngineTests
{
    [Fact(DisplayName = "Telegram triage engine | Dados incompletos | Deve solicitar campo faltante")]
    public void Evaluate_ShouldRequestMissingField_WhenZipIsMissing()
    {
        var engine = new TelegramServiceRequestTriageEngine();
        var history = BuildHistory();
        var aiReply = BuildReply(
            intent: "open_service_request",
            entitiesJson: "{\"category\":\"ar condicionado\",\"problemDescription\":\"Erro CH26 no equipamento\"}");

        var decision = engine.Evaluate(
            history,
            aiReply,
            BuildClientMessage("Meu ar esta com CH26"));

        Assert.True(decision.IsTriageIntent);
        Assert.Contains("zip_code", decision.MissingFields);
        Assert.NotNull(decision.FollowUpMessage);
        Assert.Null(decision.CreatePayload);
    }

    [Fact(DisplayName = "Telegram triage engine | Dados completos | Deve montar payload para abertura")]
    public void Evaluate_ShouldBuildCreatePayload_WhenRequiredFieldsArePresent()
    {
        var engine = new TelegramServiceRequestTriageEngine();
        var history = BuildHistory();
        var aiReply = BuildReply(
            intent: "open_service_request",
            entitiesJson: "{\"category\":\"ar condicionado\",\"problemDescription\":\"Ar condicionado LG Dual Inverter com erro CH26\",\"zipCode\":\"04567000\",\"city\":\"Sao Paulo\"}");

        var decision = engine.Evaluate(
            history,
            aiReply,
            BuildClientMessage("Ar com erro CH26"));

        Assert.True(decision.IsTriageIntent);
        Assert.Empty(decision.MissingFields);
        Assert.NotNull(decision.CreatePayload);
        Assert.Equal("Appliances", decision.CreatePayload!.Category);
        Assert.Equal("04567-000", decision.CreatePayload.Zip);
        Assert.Contains("erro CH26", decision.CreatePayload.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Telegram triage engine | Continuidade de contexto | Deve reaproveitar dados de snapshot anterior")]
    public void Evaluate_ShouldMergePreviousStateFromHistorySnapshot()
    {
        var engine = new TelegramServiceRequestTriageEngine();

        var previousState = new TelegramServiceRequestTriageState(
            CategoryRaw: "eletrica",
            CategoryEnum: "Electrical",
            ProblemDescription: "Tomada da cozinha sem energia.",
            Equipment: null,
            Brand: null,
            Model: null,
            ErrorCode: null,
            ZipCode: null,
            Street: null,
            City: null,
            Availability: "manha",
            ServiceRequestId: null,
            ServiceRequestCreatedAtUtc: null,
            LastUpdatedAtUtc: DateTime.UtcNow,
            LastClientMessage: "Tomada sem energia");

        var history = BuildHistory(previousState);
        var aiReply = BuildReply(
            intent: "open_service_request",
            entitiesJson: "{\"zipCode\":\"01001000\"}");

        var decision = engine.Evaluate(
            history,
            aiReply,
            BuildClientMessage("Meu CEP e 01001-000"));

        Assert.True(decision.IsTriageIntent);
        Assert.NotNull(decision.CreatePayload);
        Assert.Equal("Electrical", decision.CreatePayload!.Category);
        Assert.Equal("01001-000", decision.CreatePayload.Zip);
        Assert.Contains("Tomada", decision.CreatePayload.Description, StringComparison.OrdinalIgnoreCase);
    }

    private static TelegramChatbotConversationHistoryDto BuildHistory(TelegramServiceRequestTriageState? state = null)
    {
        var snapshots = new List<TelegramChatbotContextSnapshotDto>();

        if (state is not null)
        {
            var contextJson = System.Text.Json.JsonSerializer.Serialize(new { state });
            snapshots.Add(new TelegramChatbotContextSnapshotDto(
                Id: Guid.NewGuid(),
                ConversationId: Guid.NewGuid(),
                ClientId: Guid.NewGuid(),
                SnapshotType: "service_request_triage_state",
                ContextJson: contextJson,
                PromptVersion: "st-007-v1",
                ModelName: "gpt-4.1-mini",
                PromptTokens: 100,
                CompletionTokens: 80,
                TotalTokens: 180,
                CapturedAtUtc: DateTime.UtcNow));
        }

        return new TelegramChatbotConversationHistoryDto(
            Conversation: new TelegramChatbotConversationDto(
                Id: Guid.NewGuid(),
                ClientId: Guid.NewGuid(),
                Channel: "telegram",
                ChannelConversationId: "chat-1",
                Status: 1,
                StartedAtUtc: DateTime.UtcNow,
                LastInteractionAtUtc: DateTime.UtcNow,
                LastIntent: null,
                LastStep: null,
                MetadataJson: null),
            Messages: new List<TelegramChatbotMessageDto>(),
            ContextSnapshots: snapshots,
            ActionLogs: new List<TelegramChatbotActionLogDto>());
    }

    private static TelegramChatbotAssistantReply BuildReply(string intent, string entitiesJson)
    {
        return new TelegramChatbotAssistantReply(
            MessageText: "ok",
            Intent: intent,
            NextStep: "collect_missing_data",
            Confidence: 0.8m,
            EntitiesJson: entitiesJson,
            UsedFallback: false,
            UsedCache: false,
            PromptTokens: 120,
            CompletionTokens: 70,
            TotalTokens: 190,
            ModelName: "gpt-4.1-mini",
            PromptVersion: "st-007-v1",
            CorrelationId: "corr-1",
            LatencyMilliseconds: 120);
    }

    private static ChatMessageDto BuildClientMessage(string text)
    {
        return new ChatMessageDto(
            Id: Guid.NewGuid().ToString("N"),
            ChatId: 1,
            IsOutgoing: true,
            SenderDisplayName: "Cliente",
            Text: text,
            SentAtUtc: DateTimeOffset.UtcNow,
            Attachments: []);
    }
}
