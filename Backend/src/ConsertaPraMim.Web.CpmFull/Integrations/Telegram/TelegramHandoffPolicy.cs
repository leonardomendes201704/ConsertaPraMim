namespace AppMobileCPM.Integrations.Telegram;

public static class TelegramHandoffPolicy
{
    public const string ActiveStatus = "active";
    public const string BotResumedStatus = "bot_resumed";

    public const string ChatwootFirstHumanReplyReasonCode = "chatwoot_first_human_reply";
    public const string ChatwootFirstHumanReplyReasonLabel = "Primeira resposta humana do Chatwoot";
    public const string ChatwootOutboundSource = "chatwoot_outbound";

    public const string ManualActivationReasonCode = "manual_operator_activation";
    public const string ManualActivationReasonLabel = "Handoff ativado manualmente pela operacao";
    public const string ManualResumeReasonCode = "manual_operator_resume";
    public const string ManualResumeReasonLabel = "Bot retomado manualmente pela operacao";
    public const string AdminSource = "cpmfull_admin";

    public static bool IsActiveStatus(string? value) =>
        string.Equals(value?.Trim(), ActiveStatus, StringComparison.OrdinalIgnoreCase);
}
