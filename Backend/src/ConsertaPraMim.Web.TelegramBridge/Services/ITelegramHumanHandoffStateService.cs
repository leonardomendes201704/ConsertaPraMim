namespace ConsertaPraMim.Web.TelegramBridge.Services;

public interface ITelegramHumanHandoffStateService
{
    void Activate(long chatId, DateTime activatedAtUtc);
    bool Deactivate(long chatId);
    bool IsActive(long chatId);
}
