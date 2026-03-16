using ConsertaPraMim.Web.TelegramBridge.Models;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public interface ITelegramJourneySchedulingClient
{
    Task<TelegramJourneySchedulingTurnResult> ProcessTurnAsync(
        TelegramJourneySchedulingTurnRequest request,
        CancellationToken cancellationToken = default);
}
