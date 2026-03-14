using System.Collections.Concurrent;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public sealed class TelegramHumanHandoffStateService : ITelegramHumanHandoffStateService
{
    private readonly ConcurrentDictionary<long, DateTime> _activatedChats = new();

    public void Activate(long chatId, DateTime activatedAtUtc)
    {
        if (chatId <= 0)
        {
            return;
        }

        var utcValue = activatedAtUtc.Kind == DateTimeKind.Utc
            ? activatedAtUtc
            : activatedAtUtc.ToUniversalTime();

        _activatedChats.AddOrUpdate(chatId, utcValue, (_, current) => utcValue > current ? utcValue : current);
    }

    public bool IsActive(long chatId)
    {
        return chatId > 0 && _activatedChats.ContainsKey(chatId);
    }
}
