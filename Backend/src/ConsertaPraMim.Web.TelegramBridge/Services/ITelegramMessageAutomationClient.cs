using ConsertaPraMim.Web.TelegramBridge.Models;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public interface ITelegramMessageAutomationClient
{
    Task<TelegramInboundMessageAutomationResult> MirrorInboundMessageAsync(
        TelegramInboundMessageAutomationRequest request,
        CancellationToken cancellationToken = default);
}
