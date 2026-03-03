using ConsertaPraMim.Web.TelegramBridge.Models;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public interface ITelegramAiGateway
{
    Task<TelegramAiGatewayResult> GenerateReplyAsync(
        TelegramAiGatewayRequest request,
        CancellationToken cancellationToken = default);
}
