using ConsertaPraMim.Web.TelegramBridge.Models;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public interface ITelegramLeadAutomationClient
{
    Task<TelegramLeadAutomationUpsertResult> UpsertLeadAsync(
        TelegramLeadAutomationUpsertRequest request,
        CancellationToken cancellationToken = default);
}
