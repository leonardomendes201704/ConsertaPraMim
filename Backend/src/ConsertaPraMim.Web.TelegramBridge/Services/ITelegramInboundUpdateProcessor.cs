using ConsertaPraMim.Web.TelegramBridge.Models;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public interface ITelegramInboundUpdateProcessor
{
    Task<bool> ProcessAsync(TelegramUpdate update, string source, CancellationToken cancellationToken);
}
