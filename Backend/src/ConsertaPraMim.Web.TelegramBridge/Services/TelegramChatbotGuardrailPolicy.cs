using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ConsertaPraMim.Web.TelegramBridge.Models;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public sealed record TelegramChatbotGuardrailDecision(
    bool Triggered,
    string RuleCode,
    string Reason,
    string MessageToClient,
    string NextStep,
    bool RequiresHumanHandoff)
{
    public static readonly TelegramChatbotGuardrailDecision None = new(
        Triggered: false,
        RuleCode: string.Empty,
        Reason: string.Empty,
        MessageToClient: string.Empty,
        NextStep: string.Empty,
        RequiresHumanHandoff: false);
}

public static class TelegramChatbotGuardrailPolicy
{
    private static readonly Regex EmergencyPattern = new(
        "(incendio|fogo|vazamento de gas|choque eletrico|explosao|curto circuito)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex OutOfScopePattern = new(
        "(investimento|aposta|receita medica|diagnostico medico|advogado|processo judicial)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex SensitiveDataPattern = new(
        "(senha|cvv|codigo de seguranca|numero do cartao|token bancario)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static TelegramChatbotGuardrailDecision Evaluate(ChatMessageDto clientMessage, TelegramChatbotAssistantReply reply)
    {
        var normalizedClientText = Normalize(clientMessage.Text);
        var normalizedReplyText = Normalize(reply.MessageText);

        if (!string.IsNullOrWhiteSpace(normalizedClientText) && EmergencyPattern.IsMatch(normalizedClientText))
        {
            var descriptor = TelegramChatbotErrorCatalog.Resolve("guardrail_emergency", fallbackMessage: null);
            return new TelegramChatbotGuardrailDecision(
                Triggered: true,
                RuleCode: descriptor.ErrorCode,
                Reason: "Mensagem do cliente com sinal de risco urgente.",
                MessageToClient: descriptor.ClientMessage,
                NextStep: descriptor.NextStep,
                RequiresHumanHandoff: descriptor.RequiresHumanHandoff);
        }

        if (!string.IsNullOrWhiteSpace(normalizedClientText) && OutOfScopePattern.IsMatch(normalizedClientText))
        {
            var descriptor = TelegramChatbotErrorCatalog.Resolve("guardrail_out_of_scope", fallbackMessage: null);
            return new TelegramChatbotGuardrailDecision(
                Triggered: true,
                RuleCode: descriptor.ErrorCode,
                Reason: "Mensagem fora do escopo de pedidos/agendamentos.",
                MessageToClient: descriptor.ClientMessage,
                NextStep: descriptor.NextStep,
                RequiresHumanHandoff: descriptor.RequiresHumanHandoff);
        }

        if (!string.IsNullOrWhiteSpace(normalizedReplyText) && SensitiveDataPattern.IsMatch(normalizedReplyText))
        {
            var descriptor = TelegramChatbotErrorCatalog.Resolve("guardrail_sensitive_data", fallbackMessage: null);
            return new TelegramChatbotGuardrailDecision(
                Triggered: true,
                RuleCode: descriptor.ErrorCode,
                Reason: "Resposta da IA solicitou dado sensivel proibido.",
                MessageToClient: descriptor.ClientMessage,
                NextStep: descriptor.NextStep,
                RequiresHumanHandoff: descriptor.RequiresHumanHandoff);
        }

        return TelegramChatbotGuardrailDecision.None;
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Trim()
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
