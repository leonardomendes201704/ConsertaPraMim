namespace ConsertaPraMim.Web.TelegramBridge.Models;

public sealed class TelegramMessageSendOptions
{
    public bool RequestContactButton { get; init; }
    public string ContactButtonLabel { get; init; } = "Compartilhar telefone";
    public bool RemoveReplyKeyboard { get; init; }
}
