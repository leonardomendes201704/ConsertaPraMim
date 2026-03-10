using ConsertaPraMim.Web.TelegramBridge.Models;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public interface ITelegramBridgeAuthApiClient
{
    Task<(TelegramBridgeLoginResponse? Response, string? ErrorMessage)> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);
}
