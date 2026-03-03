using ConsertaPraMim.Web.TelegramBridge.Services;

namespace ConsertaPraMim.Tests.Unit.Services;

public class TelegramAiResponseParserTests
{
    [Fact(DisplayName = "Telegram IA parser | JSON estruturado | Deve mapear intent entidades e nextStep")]
    public void Parse_ShouldMapStructuredJsonFields_WhenOutputContainsValidJson()
    {
        const string rawOutput = "{\"messageToClient\":\"Entendi. Qual a marca e modelo?\",\"intent\":\"triage_problem\",\"nextStep\":\"collect_equipment_details\",\"confidence\":0.86,\"entities\":{\"categoria\":\"ar-condicionado\"}}";

        var parsed = TelegramAiResponseParser.Parse(rawOutput, "fallback");

        Assert.Equal("Entendi. Qual a marca e modelo?", parsed.MessageToClient);
        Assert.Equal("triage_problem", parsed.Intent);
        Assert.Equal("collect_equipment_details", parsed.NextStep);
        Assert.Equal(0.86m, parsed.Confidence);
        Assert.Equal("{\"categoria\":\"ar-condicionado\"}", parsed.EntitiesJson);
    }

    [Fact(DisplayName = "Telegram IA parser | Sem JSON | Deve usar texto puro e defaults")]
    public void Parse_ShouldUseRawTextWithDefaults_WhenOutputHasNoJson()
    {
        const string rawOutput = "Perfeito, me passe sua cidade para eu continuar.";

        var parsed = TelegramAiResponseParser.Parse(rawOutput, "fallback");

        Assert.Equal(rawOutput, parsed.MessageToClient);
        Assert.Equal("unknown", parsed.Intent);
        Assert.Equal("collect_missing_data", parsed.NextStep);
        Assert.Null(parsed.Confidence);
        Assert.Null(parsed.EntitiesJson);
    }

    [Fact(DisplayName = "Telegram IA parser | Saida vazia | Deve retornar fallback seguro")]
    public void Parse_ShouldReturnFallback_WhenOutputIsEmpty()
    {
        const string fallback = "Estou com instabilidade temporaria para analisar seu caso agora.";

        var parsed = TelegramAiResponseParser.Parse(string.Empty, fallback);

        Assert.Equal(fallback, parsed.MessageToClient);
        Assert.Equal("unknown", parsed.Intent);
        Assert.Equal("collect_missing_data", parsed.NextStep);
        Assert.Null(parsed.Confidence);
        Assert.Null(parsed.EntitiesJson);
    }
}
