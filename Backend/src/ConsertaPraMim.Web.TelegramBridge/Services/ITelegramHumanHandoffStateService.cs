namespace ConsertaPraMim.Web.TelegramBridge.Services;

public interface ITelegramHumanHandoffStateService
{
    TelegramHumanHandoffState Activate(long chatId, DateTime activatedAtUtc, string? reasonCode, string? reasonLabel, string? source);
    TelegramHumanHandoffState ResumeBot(long chatId, DateTime resumedAtUtc, string? reasonCode, string? reasonLabel, string? source);
    TelegramHumanHandoffState? GetState(long chatId);
    bool Deactivate(long chatId);
    bool IsActive(long chatId);
}

public sealed record TelegramHumanHandoffState
{
    public long TelegramChatId { get; init; }
    public bool IsActive { get; init; }
    public string Status { get; init; } = string.Empty;
    public string ReasonCode { get; init; } = string.Empty;
    public string ReasonLabel { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public DateTime? StartedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}
