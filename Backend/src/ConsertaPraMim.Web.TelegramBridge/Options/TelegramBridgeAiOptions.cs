namespace ConsertaPraMim.Web.TelegramBridge.Options;

public sealed class TelegramBridgeAiOptions
{
    public const string SectionName = "TelegramBridgeAi";

    public bool Enabled { get; set; } = true;

    public string Provider { get; set; } = "OpenAI";

    public string Model { get; set; } = "gpt-4.1-mini";

    public string ApiKey { get; set; } = string.Empty;

    public decimal Temperature { get; set; } = 0.30m;

    public int MaxOutputTokens { get; set; } = 600;

    public int RequestTimeoutSeconds { get; set; } = 25;

    public int MaxRetries { get; set; } = 2;

    public int MaxContextMessages { get; set; } = 16;

    public int MaxContextSnapshots { get; set; } = 4;

    public int MaxContextActionLogs { get; set; } = 4;

    public int CacheTtlSeconds { get; set; } = 20;

    public string PromptVersion { get; set; } = "st-006-v1";

    public string FallbackMessage { get; set; } = "Estou com instabilidade temporaria para analisar seu caso agora. Me descreva novamente em uma frase e tento de novo em seguida.";

    public string SystemPrompt { get; set; } =
        "Voce e o assistente de atendimento do ConsertaPraMim. " +
        "Fale em portugues-BR, tom humano, claro e objetivo. " +
        "Sempre faca perguntas curtas para coletar dados faltantes (tipo de problema, marca/modelo, localidade, disponibilidade). " +
        "Nao invente informacoes. Quando houver incerteza, admita e peca confirmacao. " +
        "Priorize proximo passo pratico para abrir pedido e facilitar agendamento.";
}
