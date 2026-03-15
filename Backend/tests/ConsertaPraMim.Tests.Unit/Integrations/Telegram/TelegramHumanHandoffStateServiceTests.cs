using ConsertaPraMim.Web.TelegramBridge.Services;

namespace ConsertaPraMim.Tests.Unit.Integrations.Telegram;

public sealed class TelegramHumanHandoffStateServiceTests
{
    [Fact(DisplayName = "Telegram handoff state | Deve ativar e resetar chat corretamente")]
    public void ActivateAndDeactivate_DeveControlarEstadoDoChat()
    {
        var service = new TelegramHumanHandoffStateService();
        var chatId = 5513997114422;

        var activated = service.Activate(
            chatId,
            new DateTime(2026, 3, 15, 18, 0, 0, DateTimeKind.Utc),
            "chatwoot_first_human_reply",
            "Primeira resposta humana do Chatwoot",
            "chatwoot_outbound");

        Assert.True(service.IsActive(chatId));
        Assert.True(activated.IsActive);
        Assert.Equal("active", activated.Status);
        Assert.Equal("chatwoot_first_human_reply", activated.ReasonCode);
        Assert.Equal("Primeira resposta humana do Chatwoot", activated.ReasonLabel);
        Assert.Equal("chatwoot_outbound", activated.Source);
        Assert.True(service.Deactivate(chatId));
        Assert.False(service.IsActive(chatId));
        Assert.False(service.Deactivate(chatId));
    }

    [Fact(DisplayName = "Telegram handoff state | Deve retomar bot preservando ultimo handoff conhecido")]
    public void ResumeBot_DevePersistirEstadoInativoComAuditoria()
    {
        var service = new TelegramHumanHandoffStateService();
        var chatId = 5513997114422;
        var startedAtUtc = new DateTime(2026, 3, 15, 18, 0, 0, DateTimeKind.Utc);
        var resumedAtUtc = startedAtUtc.AddMinutes(12);

        service.Activate(
            chatId,
            startedAtUtc,
            "manual_operator_activation",
            "Handoff ativado manualmente pela operacao",
            "cpmfull_admin");

        var resumed = service.ResumeBot(
            chatId,
            resumedAtUtc,
            "manual_operator_resume",
            "Bot retomado manualmente pela operacao",
            "cpmfull_admin");

        var state = service.GetState(chatId);

        Assert.NotNull(state);
        Assert.False(service.IsActive(chatId));
        Assert.False(resumed.IsActive);
        Assert.Equal("bot_resumed", resumed.Status);
        Assert.Equal("manual_operator_resume", resumed.ReasonCode);
        Assert.Equal("Bot retomado manualmente pela operacao", resumed.ReasonLabel);
        Assert.Equal(startedAtUtc, resumed.StartedAtUtc);
        Assert.Equal(resumedAtUtc, resumed.UpdatedAtUtc);
        Assert.Equal(resumed, state);
    }
}
