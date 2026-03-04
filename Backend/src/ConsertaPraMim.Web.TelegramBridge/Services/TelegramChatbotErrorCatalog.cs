namespace ConsertaPraMim.Web.TelegramBridge.Services;

public sealed record TelegramChatbotErrorDescriptor(
    string ErrorCode,
    string NextStep,
    string ClientMessage,
    bool RequiresHumanHandoff);

public static class TelegramChatbotErrorCatalog
{
    private static readonly IReadOnlyDictionary<string, TelegramChatbotErrorDescriptor> Catalog =
        new Dictionary<string, TelegramChatbotErrorDescriptor>(StringComparer.OrdinalIgnoreCase)
        {
            ["openai_api_key_missing"] = new(
                ErrorCode: "openai_api_key_missing",
                NextStep: "handoff_human_configuration",
                ClientMessage: "Estou com indisponibilidade tecnica para concluir seu atendimento automatico agora. Vou encaminhar para atendimento humano.",
                RequiresHumanHandoff: true),
            ["openai_prompt_missing"] = new(
                ErrorCode: "openai_prompt_missing",
                NextStep: "retry_collect_context",
                ClientMessage: "Nao consegui interpretar sua mensagem agora. Pode me explicar novamente em uma frase?",
                RequiresHumanHandoff: false),
            ["openai_empty_output"] = new(
                ErrorCode: "openai_empty_output",
                NextStep: "retry_ai_generation",
                ClientMessage: "Tive uma instabilidade para gerar a resposta agora. Pode repetir sua mensagem para eu tentar de novo?",
                RequiresHumanHandoff: false),
            ["openai_unavailable"] = new(
                ErrorCode: "openai_unavailable",
                NextStep: "retry_ai_generation",
                ClientMessage: "Estou com instabilidade temporaria para analisar seu caso agora. Tente novamente em instantes.",
                RequiresHumanHandoff: false),
            ["provider_not_supported"] = new(
                ErrorCode: "provider_not_supported",
                NextStep: "handoff_human_provider",
                ClientMessage: "No momento nao consigo concluir por atendimento automatico. Vou encaminhar para atendimento humano.",
                RequiresHumanHandoff: true),
            ["orchestrator_unhandled_exception"] = new(
                ErrorCode: "orchestrator_unhandled_exception",
                NextStep: "retry_after_failure",
                ClientMessage: "Tive uma falha inesperada ao processar seu atendimento. Pode tentar novamente em seguida?",
                RequiresHumanHandoff: false),
            ["schedule_batch_failed"] = new(
                ErrorCode: "schedule_batch_failed",
                NextStep: "retry_schedule_batch",
                ClientMessage: "Nao consegui confirmar o agendamento agora. Isso depende de uma nova tentativa operacional.",
                RequiresHumanHandoff: false),
            ["provider_lookup_failed"] = new(
                ErrorCode: "provider_lookup_failed",
                NextStep: "retry_provider_matching",
                ClientMessage: "Nao consegui consultar prestadores disponiveis agora. Pode me confirmar os dias e periodo para eu tentar novamente?",
                RequiresHumanHandoff: false),
            ["guardrail_emergency"] = new(
                ErrorCode: "guardrail_emergency",
                NextStep: "handoff_human_emergency",
                ClientMessage: "Esse caso pode envolver risco de seguranca. Se houver perigo imediato, acione emergencia local (193/192). Vou encaminhar para atendimento humano prioritario.",
                RequiresHumanHandoff: true),
            ["guardrail_out_of_scope"] = new(
                ErrorCode: "guardrail_out_of_scope",
                NextStep: "handoff_human_out_of_scope",
                ClientMessage: "Posso ajudar apenas com abertura de pedido, status e agendamento de conserto. Vou encaminhar para atendimento humano.",
                RequiresHumanHandoff: true),
            ["guardrail_sensitive_data"] = new(
                ErrorCode: "guardrail_sensitive_data",
                NextStep: "collect_safe_data",
                ClientMessage: "Por seguranca, nao compartilhe senha, codigo de seguranca ou dados completos de cartao. Me passe apenas informacoes do problema e do local.",
                RequiresHumanHandoff: false),
            ["rollout_not_enabled"] = new(
                ErrorCode: "rollout_not_enabled",
                NextStep: "human_assisted_channel",
                ClientMessage: "O atendimento automatico ainda esta em liberacao gradual para este ambiente. Vou manter seu atendimento pelo fluxo assistido.",
                RequiresHumanHandoff: true),
            ["rollout_outside_percentage"] = new(
                ErrorCode: "rollout_outside_percentage",
                NextStep: "human_assisted_channel",
                ClientMessage: "O atendimento automatico esta em liberacao gradual. Neste momento seu atendimento segue pelo fluxo assistido.",
                RequiresHumanHandoff: true),
            ["rollout_chat_blocked"] = new(
                ErrorCode: "rollout_chat_blocked",
                NextStep: "human_assisted_channel",
                ClientMessage: "Seu atendimento esta temporariamente no fluxo assistido. Se precisar, sigo com abertura de pedido manual.",
                RequiresHumanHandoff: true)
        };

    public static TelegramChatbotErrorDescriptor Resolve(string? errorCode, string? fallbackMessage)
    {
        if (!string.IsNullOrWhiteSpace(errorCode) && Catalog.TryGetValue(errorCode.Trim(), out var descriptor))
        {
            return descriptor;
        }

        var safeFallback = string.IsNullOrWhiteSpace(fallbackMessage)
            ? "Estou com instabilidade temporaria para analisar seu caso agora. Tente novamente em instantes."
            : fallbackMessage.Trim();

        return new TelegramChatbotErrorDescriptor(
            ErrorCode: string.IsNullOrWhiteSpace(errorCode) ? "chatbot_generic_fallback" : errorCode.Trim(),
            NextStep: "collect_missing_data",
            ClientMessage: safeFallback,
            RequiresHumanHandoff: false);
    }
}
