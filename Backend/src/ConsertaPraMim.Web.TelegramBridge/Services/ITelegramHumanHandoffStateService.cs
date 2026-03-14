namespace ConsertaPraMim.Web.TelegramBridge.Services;

public interface ITelegramHumanHandoffStateService
{
    void Activate(long chatId, DateTime activatedAtUtc);
    bool IsActive(long chatId);
}
