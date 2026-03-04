namespace ConsertaPraMim.Web.TelegramBridge.Models;

public sealed class OpenConversationRequest
{
    public string ChatId { get; init; } = string.Empty;

    public string? Title { get; init; }
}
