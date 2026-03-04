using ConsertaPraMim.Web.TelegramBridge.Models;
using ConsertaPraMim.Web.TelegramBridge.Services;

namespace ConsertaPraMim.Tests.Unit.Services;

public class TelegramChatbotGuardrailPolicyTests
{
    [Fact(DisplayName = "Telegram chatbot guardrails | Emergencia | Deve acionar handoff humano prioritario")]
    public void Evaluate_ShouldTriggerEmergencyRule_WhenClientReportsRisk()
    {
        var clientMessage = BuildClientMessage("Tem vazamento de gas e fogo no equipamento.");
        var reply = BuildAssistantReply("Vou analisar seu pedido.");

        var decision = TelegramChatbotGuardrailPolicy.Evaluate(clientMessage, reply);

        Assert.True(decision.Triggered);
        Assert.Equal("guardrail_emergency", decision.RuleCode);
        Assert.True(decision.RequiresHumanHandoff);
        Assert.StartsWith("handoff_human", decision.NextStep, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Telegram chatbot guardrails | Fora de escopo | Deve bloquear temas nao relacionados")]
    public void Evaluate_ShouldTriggerOutOfScopeRule_WhenTopicIsNotRepair()
    {
        var clientMessage = BuildClientMessage("Quero orientacao de investimento e processo judicial.");
        var reply = BuildAssistantReply("Posso ajudar.");

        var decision = TelegramChatbotGuardrailPolicy.Evaluate(clientMessage, reply);

        Assert.True(decision.Triggered);
        Assert.Equal("guardrail_out_of_scope", decision.RuleCode);
        Assert.True(decision.RequiresHumanHandoff);
    }

    [Fact(DisplayName = "Telegram chatbot guardrails | Dados sensiveis | Deve bloquear resposta que pede senha/cartao")]
    public void Evaluate_ShouldTriggerSensitiveDataRule_WhenAssistantAsksForPassword()
    {
        var clientMessage = BuildClientMessage("Meu aparelho nao liga.");
        var reply = BuildAssistantReply("Me informe sua senha e numero do cartao para seguir.");

        var decision = TelegramChatbotGuardrailPolicy.Evaluate(clientMessage, reply);

        Assert.True(decision.Triggered);
        Assert.Equal("guardrail_sensitive_data", decision.RuleCode);
        Assert.False(decision.RequiresHumanHandoff);
    }

    [Fact(DisplayName = "Telegram chatbot guardrails | Fluxo normal | Nao deve intervir sem violacao")]
    public void Evaluate_ShouldNotTrigger_WhenConversationIsSafe()
    {
        var clientMessage = BuildClientMessage("Minha torneira esta pingando.");
        var reply = BuildAssistantReply("Perfeito. Me informe o CEP para abrir seu pedido.");

        var decision = TelegramChatbotGuardrailPolicy.Evaluate(clientMessage, reply);

        Assert.False(decision.Triggered);
        Assert.Equal(TelegramChatbotGuardrailDecision.None, decision);
    }

    private static ChatMessageDto BuildClientMessage(string text)
    {
        return new ChatMessageDto(
            Id: "m-1",
            ChatId: 6614607033538827000,
            IsOutgoing: true,
            SenderDisplayName: "Cliente",
            Text: text,
            SentAtUtc: DateTimeOffset.UtcNow,
            Attachments: []);
    }

    private static TelegramChatbotAssistantReply BuildAssistantReply(string message)
    {
        return new TelegramChatbotAssistantReply(
            MessageText: message,
            Intent: "open_service_request",
            NextStep: "collect_zip_code",
            Confidence: 0.8m,
            EntitiesJson: "{}",
            UsedFallback: false,
            UsedCache: false,
            PromptTokens: 100,
            CompletionTokens: 40,
            TotalTokens: 140,
            ModelName: "gpt-4.1-mini",
            PromptVersion: "st-010-v1",
            CorrelationId: "corr-1",
            LatencyMilliseconds: 80);
    }
}
