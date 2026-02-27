using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConsertaPraMim.Application.Services;

public class ServiceRequestProblemAnalysisService : IServiceRequestProblemAnalysisService
{
    private const string DefaultModel = "gpt-4.1-mini";
    private const decimal DefaultTemperature = 0.15m;
    private const int DefaultMaxOutputTokens = 420;
    private const int MaxHighlights = 6;
    private static readonly string[] NarrativePrefixesToStrip =
    {
        "o cliente relata",
        "cliente relata",
        "o cliente informa",
        "cliente informa",
        "o cliente informou",
        "cliente informou",
        "o cliente descreve",
        "cliente descreve",
        "o cliente solicita",
        "cliente solicita",
        "solicitacao do cliente",
        "pedido do cliente"
    };
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceCategoryRepository _serviceCategoryRepository;
    private readonly IAdminGrowthAiStore _growthAiStore;
    private readonly IAdminGrowthAiGateway _growthAiGateway;
    private readonly ILogger<ServiceRequestProblemAnalysisService> _logger;

    public ServiceRequestProblemAnalysisService(
        IServiceCategoryRepository serviceCategoryRepository,
        IAdminGrowthAiStore growthAiStore,
        IAdminGrowthAiGateway growthAiGateway,
        ILogger<ServiceRequestProblemAnalysisService>? logger = null)
    {
        _serviceCategoryRepository = serviceCategoryRepository;
        _growthAiStore = growthAiStore;
        _growthAiGateway = growthAiGateway;
        _logger = logger ?? NullLogger<ServiceRequestProblemAnalysisService>.Instance;
    }

    public async Task<ServiceRequestProblemAnalysisResultDto> AnalyzeAsync(
        ServiceRequestProblemAnalysisRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.CategoryId == Guid.Empty)
        {
            return Failure("invalid_category", "Informe uma categoria valida.");
        }

        var normalizedDescription = NormalizeDescription(request.Description);
        if (normalizedDescription.Length < 15)
        {
            return Failure("invalid_description", "Descreva o problema com mais detalhes (minimo 15 caracteres).");
        }

        var category = await _serviceCategoryRepository.GetByIdAsync(request.CategoryId);
        if (category == null || !category.IsActive)
        {
            return Failure("category_not_available", "Categoria selecionada esta inativa ou indisponivel.");
        }

        var settingsSnapshot = await _growthAiStore.LoadAsync(cancellationToken);
        var settings = settingsSnapshot.Settings;
        if (settings == null || !settings.Enabled || string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return BuildFallbackResult(
                category.Name,
                normalizedDescription,
                "OpenAI nao configurada para analise de pedido.");
        }

        var gatewayRequest = new AdminGrowthAiGatewayRequest(
            ApiKey: settings.ApiKey,
            Model: string.IsNullOrWhiteSpace(settings.Model) ? DefaultModel : settings.Model.Trim(),
            Temperature: settings.Temperature <= 0 ? DefaultTemperature : settings.Temperature,
            MaxOutputTokens: settings.MaxOutputTokens <= 0 ? DefaultMaxOutputTokens : settings.MaxOutputTokens,
            SystemPrompt: BuildSystemPrompt(),
            UserPrompt: BuildUserPrompt(category.Name, normalizedDescription));

        var gatewayResult = await _growthAiGateway.GenerateAnalysisAsync(gatewayRequest, cancellationToken);
        if (!gatewayResult.Success || string.IsNullOrWhiteSpace(gatewayResult.OutputText))
        {
            _logger.LogWarning(
                "Service request problem analysis fallback. category={CategoryName} errorCode={ErrorCode}",
                category.Name,
                gatewayResult.ErrorCode ?? "unknown_gateway_error");

            return BuildFallbackResult(
                category.Name,
                normalizedDescription,
                gatewayResult.ErrorMessage ?? "Falha ao gerar analise automatica.",
                gatewayResult.ErrorCode,
                gatewayRequest.Model);
        }

        var parsed = ParseGatewayOutput(gatewayResult.OutputText);
        return new ServiceRequestProblemAnalysisResultDto(
            Success: true,
            CategoryName: category.Name,
            UnderstandingSummary: parsed.UnderstandingSummary,
            Highlights: parsed.Highlights,
            UsedFallback: false,
            Model: gatewayRequest.Model,
            GeneratedAtUtc: DateTime.UtcNow);
    }

    private static ServiceRequestProblemAnalysisResultDto Failure(string errorCode, string errorMessage)
    {
        return new ServiceRequestProblemAnalysisResultDto(
            Success: false,
            CategoryName: string.Empty,
            UnderstandingSummary: string.Empty,
            Highlights: Array.Empty<string>(),
            UsedFallback: true,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage,
            GeneratedAtUtc: DateTime.UtcNow);
    }

    private static ServiceRequestProblemAnalysisResultDto BuildFallbackResult(
        string categoryName,
        string description,
        string reason,
        string? errorCode = "problem_analysis_fallback",
        string? model = null)
    {
        var fallbackSummary = BuildFallbackSummary(categoryName, description);
        var fallbackHighlights = BuildFallbackHighlights(description);

        return new ServiceRequestProblemAnalysisResultDto(
            Success: true,
            CategoryName: categoryName,
            UnderstandingSummary: fallbackSummary,
            Highlights: fallbackHighlights,
            UsedFallback: true,
            Model: model,
            ErrorCode: errorCode,
            ErrorMessage: reason,
            GeneratedAtUtc: DateTime.UtcNow);
    }

    private static string BuildSystemPrompt()
    {
        return
            "Voce e um especialista tecnico do marketplace ConsertaPraMim. " +
            "Receba categoria e descricao do cliente e gere um resumo curto para confirmar entendimento do problema em linguagem tecnica direta. " +
            "Nao invente dados fora da descricao.";
    }

    private static string BuildUserPrompt(string categoryName, string description)
    {
        var payload = JsonSerializer.Serialize(
            new
            {
                category = categoryName,
                description
            },
            JsonOptions);

        return
            "Analise o problema descrito e responda OBRIGATORIAMENTE em JSON valido com o contrato:\n" +
            "{\n" +
            "  \"understandingSummary\": \"texto curto com entendimento (max 500 chars)\",\n" +
            "  \"highlights\": [\"item 1\", \"item 2\", \"item 3\"]\n" +
            "}\n\n" +
            "Regras:\n" +
            "- linguagem: portugues-BR.\n" +
            "- sem markdown.\n" +
            "- nao usar narracao com sujeito pessoal (ex.: 'o cliente relata', 'o cliente informou').\n" +
            "- comecar pelo problema objetivo e contexto tecnico.\n" +
            "- highlights maximo 5 itens, objetivos.\n\n" +
            "Payload:\n" +
            payload;
    }

    private static ParsedProblemAnalysis ParseGatewayOutput(string outputText)
    {
        var json = TryExtractJson(outputText);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ParsedProblemAnalysis(
                UnderstandingSummary: outputText.Trim(),
                Highlights: Array.Empty<string>());
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var understandingSummary = NormalizeUnderstandingSummary(GetString(root, "understandingSummary"));
            var highlights = GetStringArray(root, "highlights");

            if (string.IsNullOrWhiteSpace(understandingSummary))
            {
                understandingSummary = NormalizeUnderstandingSummary(outputText.Trim());
            }

            return new ParsedProblemAnalysis(understandingSummary, highlights);
        }
        catch (JsonException)
        {
            return new ParsedProblemAnalysis(
                UnderstandingSummary: NormalizeUnderstandingSummary(outputText.Trim()),
                Highlights: Array.Empty<string>());
        }
    }

    private static string NormalizeDescription(string? description)
    {
        return string.IsNullOrWhiteSpace(description)
            ? string.Empty
            : description.Trim();
    }

    private static string BuildFallbackSummary(string categoryName, string description)
    {
        var compactDescription = description.Length > 280
            ? $"{description[..277]}..."
            : description;

        return $"Problema identificado ({categoryName}): {compactDescription}";
    }

    private static IReadOnlyList<string> BuildFallbackHighlights(string description)
    {
        var highlights = description
            .Split(new[] { '.', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .Where(part => part.Length >= 8)
            .Take(MaxHighlights)
            .ToList();

        if (highlights.Count == 0)
        {
            highlights.Add("Detalhes insuficientes para gerar highlights automaticos.");
        }

        return highlights;
    }

    private static string? TryExtractJson(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
        {
            return trimmed;
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        return trimmed[start..(end + 1)];
    }

    private static string GetString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }

        return property.GetString()?.Trim() ?? string.Empty;
    }

    private static IReadOnlyList<string> GetStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return property.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()?.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxHighlights)
            .ToArray();
    }

    private static string NormalizeUnderstandingSummary(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return string.Empty;
        }

        var normalized = summary.Trim();

        foreach (var prefix in NarrativePrefixesToStrip)
        {
            if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            normalized = normalized[prefix.Length..].TrimStart(':', '-', ',', ';', ' ');
            break;
        }

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return summary.Trim();
        }

        var chars = normalized.ToCharArray();
        chars[0] = char.ToUpperInvariant(chars[0]);
        return new string(chars);
    }

    private sealed record ParsedProblemAnalysis(
        string UnderstandingSummary,
        IReadOnlyList<string> Highlights);
}
