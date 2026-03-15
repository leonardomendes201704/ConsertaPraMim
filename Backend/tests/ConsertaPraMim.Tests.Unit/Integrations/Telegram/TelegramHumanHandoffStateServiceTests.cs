using ConsertaPraMim.Web.TelegramBridge.Services;

namespace ConsertaPraMim.Tests.Unit.Integrations.Telegram;

public sealed class TelegramHumanHandoffStateServiceTests
{
    [Fact(DisplayName = "Telegram handoff state | Deve ativar e resetar chat corretamente")]
    public void ActivateAndDeactivate_DeveControlarEstadoDoChat()
    {
        var service = new TelegramHumanHandoffStateService();
        var chatId = 5513997114422;

        service.Activate(chatId, new DateTime(2026, 3, 15, 18, 0, 0, DateTimeKind.Utc));

        Assert.True(service.IsActive(chatId));
        Assert.True(service.Deactivate(chatId));
        Assert.False(service.IsActive(chatId));
        Assert.False(service.Deactivate(chatId));
    }
}
