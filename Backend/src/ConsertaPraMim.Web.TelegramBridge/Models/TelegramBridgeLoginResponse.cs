namespace ConsertaPraMim.Web.TelegramBridge.Models;

public sealed class TelegramBridgeLoginResponse
{
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
