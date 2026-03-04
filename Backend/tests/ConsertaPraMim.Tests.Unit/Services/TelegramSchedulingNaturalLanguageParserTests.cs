using ConsertaPraMim.Web.TelegramBridge.Services;

namespace ConsertaPraMim.Tests.Unit.Services;

public class TelegramSchedulingNaturalLanguageParserTests
{
    [Fact(DisplayName = "Telegram scheduling parser | Deve mapear quarta e sexta de manha para janelas UTC")]
    public void Parse_ShouldReturnUtcWindows_WhenMessageContainsWeekdaysAndPeriod()
    {
        var parser = new TelegramSchedulingNaturalLanguageParser();
        var nowUtc = new DateTime(2026, 3, 3, 15, 0, 0, DateTimeKind.Utc);

        var result = parser.Parse(
            "Sim, pode agendar com 2 prestadores na Quarta e na Sexta feira, no periodo da manha.",
            nowUtc);

        Assert.True(result.IsSchedulingIntent);
        Assert.Equal(2, result.RequestedVisits);
        Assert.Null(result.ErrorCode);
        Assert.Equal(2, result.Windows.Count);

        Assert.Equal(new DateTime(2026, 3, 4, 12, 0, 0, DateTimeKind.Utc), result.Windows[0].WindowStartUtc);
        Assert.Equal(new DateTime(2026, 3, 4, 14, 0, 0, DateTimeKind.Utc), result.Windows[0].WindowEndUtc);
        Assert.Equal(new DateTime(2026, 3, 6, 12, 0, 0, DateTimeKind.Utc), result.Windows[1].WindowStartUtc);
        Assert.Equal(new DateTime(2026, 3, 6, 14, 0, 0, DateTimeKind.Utc), result.Windows[1].WindowEndUtc);
    }

    [Fact(DisplayName = "Telegram scheduling parser | Deve inferir intencao de agenda com dia+periodo mesmo sem palavra-chave")]
    public void Parse_ShouldInferSchedulingIntent_WhenMessageContainsOnlyDayAndPeriodSignals()
    {
        var parser = new TelegramSchedulingNaturalLanguageParser();
        var nowUtc = new DateTime(2026, 3, 3, 15, 0, 0, DateTimeKind.Utc);

        var result = parser.Parse(
            "Pode ser na quarta, quinta e sexta, na parte da manha.",
            nowUtc);

        Assert.True(result.IsSchedulingIntent);
        Assert.Equal(3, result.RequestedVisits);
        Assert.Null(result.ErrorCode);
        Assert.Equal(3, result.Windows.Count);

        Assert.Equal(new DateTime(2026, 3, 4, 12, 0, 0, DateTimeKind.Utc), result.Windows[0].WindowStartUtc);
        Assert.Equal(new DateTime(2026, 3, 5, 12, 0, 0, DateTimeKind.Utc), result.Windows[1].WindowStartUtc);
        Assert.Equal(new DateTime(2026, 3, 6, 12, 0, 0, DateTimeKind.Utc), result.Windows[2].WindowStartUtc);
    }

    [Fact(DisplayName = "Telegram scheduling parser | Deve retornar erro quando dia informado sem periodo")]
    public void Parse_ShouldReturnMissingPeriod_WhenPeriodIsNotInformed()
    {
        var parser = new TelegramSchedulingNaturalLanguageParser();

        var result = parser.Parse(
            "Pode agendar duas visitas na quarta e sexta.",
            DateTime.UtcNow);

        Assert.True(result.IsSchedulingIntent);
        Assert.Equal("missing_period", result.ErrorCode);
        Assert.Empty(result.Windows);
    }

    [Fact(DisplayName = "Telegram scheduling parser | Deve retornar erro quando quantidade supera dias informados")]
    public void Parse_ShouldReturnInsufficientDays_WhenRequestedVisitsExceedDays()
    {
        var parser = new TelegramSchedulingNaturalLanguageParser();

        var result = parser.Parse(
            "Agendar com 2 prestadores na quarta de manha.",
            DateTime.UtcNow);

        Assert.True(result.IsSchedulingIntent);
        Assert.Equal("insufficient_days", result.ErrorCode);
        Assert.Empty(result.Windows);
    }

    [Fact(DisplayName = "Telegram scheduling parser | Deve ignorar mensagem sem intencao de agenda")]
    public void Parse_ShouldReturnNotSchedulingIntent_WhenMessageIsGeneral()
    {
        var parser = new TelegramSchedulingNaturalLanguageParser();

        var result = parser.Parse(
            "Quero saber o status do meu pedido.",
            DateTime.UtcNow);

        Assert.False(result.IsSchedulingIntent);
        Assert.Equal(0, result.RequestedVisits);
        Assert.Empty(result.Windows);
    }
}
