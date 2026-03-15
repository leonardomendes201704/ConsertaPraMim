using System.Collections.Concurrent;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public sealed class TelegramHumanHandoffStateService : ITelegramHumanHandoffStateService
{
    private const string ActiveStatus = "active";
    private const string BotResumedStatus = "bot_resumed";
    private const string DefaultActivationReasonCode = "human_handoff";
    private const string DefaultActivationReasonLabel = "Handoff humano ativo";
    private const string DefaultActivationSource = "telegram_bridge";
    private const string DefaultResumeReasonCode = "bot_resumed";
    private const string DefaultResumeReasonLabel = "Bot retomado";
    private const string DefaultResumeSource = "telegram_bridge";

    private readonly ConcurrentDictionary<long, TelegramHumanHandoffState> _states = new();

    public TelegramHumanHandoffState Activate(
        long chatId,
        DateTime activatedAtUtc,
        string? reasonCode,
        string? reasonLabel,
        string? source)
    {
        if (chatId <= 0)
        {
            return CreateDefaultState(chatId, isActive: false, BotResumedStatus, reasonCode, reasonLabel, source, null, DateTime.UtcNow);
        }

        var utcValue = EnsureUtc(activatedAtUtc);
        var state = _states.AddOrUpdate(
            chatId,
            _ => CreateDefaultState(chatId, isActive: true, ActiveStatus, reasonCode, reasonLabel, source, utcValue, utcValue),
            (_, current) => new TelegramHumanHandoffState
            {
                TelegramChatId = chatId,
                IsActive = true,
                Status = ActiveStatus,
                ReasonCode = NormalizeReasonCode(reasonCode, DefaultActivationReasonCode),
                ReasonLabel = NormalizeReasonLabel(reasonLabel, DefaultActivationReasonLabel),
                Source = NormalizeSource(source, DefaultActivationSource),
                StartedAtUtc = utcValue,
                UpdatedAtUtc = utcValue > current.UpdatedAtUtc ? utcValue : current.UpdatedAtUtc
            });

        return state;
    }

    public TelegramHumanHandoffState ResumeBot(
        long chatId,
        DateTime resumedAtUtc,
        string? reasonCode,
        string? reasonLabel,
        string? source)
    {
        if (chatId <= 0)
        {
            return CreateDefaultState(chatId, isActive: false, BotResumedStatus, reasonCode, reasonLabel, source, null, DateTime.UtcNow);
        }

        var utcValue = EnsureUtc(resumedAtUtc);
        var state = _states.AddOrUpdate(
            chatId,
            _ => CreateDefaultState(chatId, isActive: false, BotResumedStatus, reasonCode, reasonLabel, source, null, utcValue),
            (_, current) => new TelegramHumanHandoffState
            {
                TelegramChatId = chatId,
                IsActive = false,
                Status = BotResumedStatus,
                ReasonCode = NormalizeReasonCode(reasonCode, DefaultResumeReasonCode),
                ReasonLabel = NormalizeReasonLabel(reasonLabel, DefaultResumeReasonLabel),
                Source = NormalizeSource(source, DefaultResumeSource),
                StartedAtUtc = current.StartedAtUtc,
                UpdatedAtUtc = utcValue > current.UpdatedAtUtc ? utcValue : current.UpdatedAtUtc
            });

        return state;
    }

    public TelegramHumanHandoffState? GetState(long chatId)
    {
        if (chatId <= 0)
        {
            return null;
        }

        return _states.TryGetValue(chatId, out var state)
            ? state
            : null;
    }

    public bool Deactivate(long chatId)
    {
        return chatId > 0 && _states.TryRemove(chatId, out _);
    }

    public bool IsActive(long chatId)
    {
        return chatId > 0 &&
               _states.TryGetValue(chatId, out var state) &&
               state.IsActive;
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.ToUniversalTime();

    private static TelegramHumanHandoffState CreateDefaultState(
        long chatId,
        bool isActive,
        string status,
        string? reasonCode,
        string? reasonLabel,
        string? source,
        DateTime? startedAtUtc,
        DateTime updatedAtUtc) =>
        new()
        {
            TelegramChatId = chatId,
            IsActive = isActive,
            Status = status,
            ReasonCode = NormalizeReasonCode(reasonCode, isActive ? DefaultActivationReasonCode : DefaultResumeReasonCode),
            ReasonLabel = NormalizeReasonLabel(reasonLabel, isActive ? DefaultActivationReasonLabel : DefaultResumeReasonLabel),
            Source = NormalizeSource(source, isActive ? DefaultActivationSource : DefaultResumeSource),
            StartedAtUtc = startedAtUtc,
            UpdatedAtUtc = EnsureUtc(updatedAtUtc)
        };

    private static string NormalizeReasonCode(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim().ToLowerInvariant();

    private static string NormalizeReasonLabel(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();

    private static string NormalizeSource(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim().ToLowerInvariant();
}
