using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConsertaPraMim.Application.Services;

public class AdminGrowthAiService : IAdminGrowthAiService
{
    private const string ProviderName = "OpenAI";
    private const string DefaultModel = "gpt-4.1-mini";
    private const decimal DefaultTemperature = 0.20m;
    private const int DefaultMaxOutputTokens = 900;
    private const int MaxHistoryEntries = 40;
    private const int MaxPromptItems = 12;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAdminGrowthAiStore _store;
    private readonly IAdminGrowthService _adminGrowthService;
    private readonly IAdminLiquidityScoreService _adminLiquidityScoreService;
    private readonly IAdminGrowthAiGateway _adminGrowthAiGateway;
    private readonly ILogger<AdminGrowthAiService> _logger;

    public AdminGrowthAiService(
        IAdminGrowthAiStore store,
        IAdminGrowthService adminGrowthService,
        IAdminLiquidityScoreService adminLiquidityScoreService,
        IAdminGrowthAiGateway adminGrowthAiGateway,
        ILogger<AdminGrowthAiService>? logger = null)
    {
        _store = store;
        _adminGrowthService = adminGrowthService;
        _adminLiquidityScoreService = adminLiquidityScoreService;
        _adminGrowthAiGateway = adminGrowthAiGateway;
        _logger = logger ?? NullLogger<AdminGrowthAiService>.Instance;
    }

    public async Task<AdminGrowthAiSnapshotDto> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadAsync(cancellationToken);
        var orderedAnalyses = (snapshot.Analyses ?? Array.Empty<AdminGrowthAiAnalysisDto>())
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(MaxHistoryEntries)
            .ToArray();

        return new AdminGrowthAiSnapshotDto(
            Settings: BuildSettingsDto(snapshot.Settings, orderedAnalyses.FirstOrDefault()),
            RecentAnalyses: orderedAnalyses);
    }

    public async Task<AdminOperationResultDto> UpsertSettingsAsync(
        AdminGrowthAiUpsertSettingsRequestDto request,
        Guid actorUserId,
        string? actorEmail,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return new AdminOperationResultDto(false, "invalid_request", "Payload de configuracao nao informado.");
        }

        var snapshot = await _store.LoadAsync(cancellationToken);
        var currentSettings = snapshot.Settings;

        var model = NormalizeModel(request.Model, currentSettings?.Model);
        if (string.IsNullOrWhiteSpace(model))
        {
            return new AdminOperationResultDto(false, "invalid_model", "Modelo OpenAI invalido.");
        }

        var apiKey = string.IsNullOrWhiteSpace(request.ApiKey)
            ? currentSettings?.ApiKey ?? string.Empty
            : request.ApiKey.Trim();

        if (request.Enabled && string.IsNullOrWhiteSpace(apiKey))
        {
            return new AdminOperationResultDto(false, "openai_api_key_required", "Informe a OpenAI API key para habilitar o copiloto.");
        }

        var normalizedSettings = new AdminGrowthAiStoreSettings(
            Enabled: request.Enabled,
            Provider: ProviderName,
            Model: model,
            ApiKey: apiKey,
            Temperature: Math.Clamp(request.Temperature, 0.0m, 1.0m),
            MaxOutputTokens: Math.Clamp(request.MaxOutputTokens, 200, 4000),
            SystemPrompt: NormalizeSystemPrompt(request.SystemPrompt, currentSettings?.SystemPrompt),
            UpdatedAtUtc: DateTime.UtcNow);

        await _store.SaveAsync(
            snapshot with
            {
                Settings = normalizedSettings
            },
            cancellationToken);

        _logger.LogInformation(
            "AdminGrowthAi settings updated. enabled={Enabled} model={Model} actorUserId={ActorUserId} actorEmail={ActorEmail}",
            normalizedSettings.Enabled,
            normalizedSettings.Model,
            actorUserId,
            actorEmail);

        return new AdminOperationResultDto(true);
    }

    public async Task<AdminGrowthAiAnalyzeResultDto> AnalyzeAsync(
        AdminGrowthAiAnalyzeRequestDto request,
        Guid actorUserId,
        string? actorEmail,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _store.LoadAsync(cancellationToken);
        var settings = snapshot.Settings;
        if (settings == null || string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return new AdminGrowthAiAnalyzeResultDto(
                Success: false,
                ErrorCode: "growth_ai_not_configured",
                ErrorMessage: "Copiloto IA nao configurado. Defina a OpenAI API key.");
        }

        if (!settings.Enabled)
        {
            return new AdminGrowthAiAnalyzeResultDto(
                Success: false,
                ErrorCode: "growth_ai_disabled",
                ErrorMessage: "Copiloto IA esta desabilitado nas configuracoes.");
        }

        var normalizedRequest = NormalizeAnalyzeRequest(request);
        var funnel = await _adminGrowthService.GetFunnelAsync(
            new AdminGrowthFunnelQueryDto(
                FromUtc: normalizedRequest.FromUtc,
                ToUtc: normalizedRequest.ToUtc,
                Category: normalizedRequest.Category,
                City: normalizedRequest.City,
                ProposalSlaMinutes: normalizedRequest.ProposalSlaMinutes,
                AcceptanceSlaHours: normalizedRequest.AcceptanceSlaHours));

        var liquidity = await _adminLiquidityScoreService.GetScoreAsync(
            new AdminLiquidityScoreQueryDto(
                FromUtc: normalizedRequest.FromUtc,
                ToUtc: normalizedRequest.ToUtc,
                Category: normalizedRequest.Category,
                City: normalizedRequest.City,
                ProposalSlaMinutes: normalizedRequest.ProposalSlaMinutes,
                Take: normalizedRequest.LiquidityTake));

        var cockpit = await _adminGrowthService.GetExecutiveCockpitAsync(
            new AdminGrowthExecutiveCockpitQueryDto(
                FromUtc: normalizedRequest.FromUtc,
                ToUtc: normalizedRequest.ToUtc,
                Category: normalizedRequest.Category,
                City: normalizedRequest.City,
                ProposalSlaMinutes: normalizedRequest.ProposalSlaMinutes,
                AcceptanceSlaHours: normalizedRequest.AcceptanceSlaHours,
                NorthStarResolutionHours: 72),
            cancellationToken);

        var weeklyRitual = await _adminGrowthService.GetWeeklyRitualSnapshotAsync(
            normalizedRequest.ToUtc ?? DateTime.UtcNow,
            cancellationToken);

        var monthlyReview = await _adminGrowthService.GetMonthlyReviewSnapshotAsync(
            normalizedRequest.ToUtc ?? DateTime.UtcNow,
            cancellationToken);

        var userPrompt = BuildUserPrompt(
            normalizedRequest,
            funnel,
            liquidity,
            cockpit,
            weeklyRitual,
            monthlyReview);
        var gatewayResult = await _adminGrowthAiGateway.GenerateAnalysisAsync(
            new AdminGrowthAiGatewayRequest(
                ApiKey: settings.ApiKey,
                Model: settings.Model,
                Temperature: settings.Temperature,
                MaxOutputTokens: settings.MaxOutputTokens,
                SystemPrompt: settings.SystemPrompt,
                UserPrompt: userPrompt),
            cancellationToken);

        if (!gatewayResult.Success || string.IsNullOrWhiteSpace(gatewayResult.OutputText))
        {
            return new AdminGrowthAiAnalyzeResultDto(
                Success: false,
                ErrorCode: gatewayResult.ErrorCode ?? "growth_ai_gateway_error",
                ErrorMessage: gatewayResult.ErrorMessage ?? "Falha ao gerar analise IA.");
        }

        var parsed = ParseInsights(gatewayResult.OutputText);
        var analysis = new AdminGrowthAiAnalysisDto(
            AnalysisId: Guid.NewGuid(),
            CreatedAtUtc: DateTime.UtcNow,
            ActorEmail: string.IsNullOrWhiteSpace(actorEmail) ? "admin@consertapramim.local" : actorEmail.Trim(),
            FromUtc: normalizedRequest.FromUtc,
            ToUtc: normalizedRequest.ToUtc,
            Category: normalizedRequest.Category,
            City: normalizedRequest.City,
            ExecutiveSummary: parsed.ExecutiveSummary,
            FunnelInsights: parsed.FunnelInsights,
            LiquidityInsights: parsed.LiquidityInsights,
            Risks: parsed.Risks,
            RecommendedActions: parsed.RecommendedActions,
            Model: settings.Model,
            InputTokens: gatewayResult.InputTokens,
            OutputTokens: gatewayResult.OutputTokens,
            TotalTokens: gatewayResult.TotalTokens);

        var updatedHistory = (snapshot.Analyses ?? Array.Empty<AdminGrowthAiAnalysisDto>())
            .Append(analysis)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(MaxHistoryEntries)
            .ToArray();

        await _store.SaveAsync(
            snapshot with
            {
                Settings = settings with { UpdatedAtUtc = settings.UpdatedAtUtc },
                Analyses = updatedHistory
            },
            cancellationToken);

        _logger.LogInformation(
            "AdminGrowthAi analysis generated. actorUserId={ActorUserId} actorEmail={ActorEmail} category={Category} city={City}",
            actorUserId,
            analysis.ActorEmail,
            analysis.Category,
            analysis.City);

        return new AdminGrowthAiAnalyzeResultDto(
            Success: true,
            Analysis: analysis);
    }

    private static AdminGrowthAiSettingsDto BuildSettingsDto(
        AdminGrowthAiStoreSettings? settings,
        AdminGrowthAiAnalysisDto? latestAnalysis)
    {
        var effectiveSettings = settings ?? new AdminGrowthAiStoreSettings(
            Enabled: false,
            Provider: ProviderName,
            Model: DefaultModel,
            ApiKey: string.Empty,
            Temperature: DefaultTemperature,
            MaxOutputTokens: DefaultMaxOutputTokens,
            SystemPrompt: DefaultSystemPrompt,
            UpdatedAtUtc: DateTime.UtcNow);

        var hasApiKey = !string.IsNullOrWhiteSpace(effectiveSettings.ApiKey);
        return new AdminGrowthAiSettingsDto(
            Enabled: effectiveSettings.Enabled,
            IsConfigured: hasApiKey,
            Provider: string.IsNullOrWhiteSpace(effectiveSettings.Provider) ? ProviderName : effectiveSettings.Provider,
            Model: string.IsNullOrWhiteSpace(effectiveSettings.Model) ? DefaultModel : effectiveSettings.Model,
            Temperature: effectiveSettings.Temperature,
            MaxOutputTokens: effectiveSettings.MaxOutputTokens,
            SystemPrompt: string.IsNullOrWhiteSpace(effectiveSettings.SystemPrompt) ? DefaultSystemPrompt : effectiveSettings.SystemPrompt,
            ApiKeyMasked: hasApiKey ? MaskApiKey(effectiveSettings.ApiKey) : null,
            UpdatedAtUtc: settings?.UpdatedAtUtc,
            LastAnalysisAtUtc: latestAnalysis?.CreatedAtUtc);
    }

    private static string NormalizeModel(string? requestedModel, string? currentModel)
    {
        var candidate = requestedModel;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            candidate = currentModel;
        }

        if (string.IsNullOrWhiteSpace(candidate))
        {
            candidate = DefaultModel;
        }

        return candidate.Trim();
    }

    private static string NormalizeSystemPrompt(string? requestedPrompt, string? currentPrompt)
    {
        if (!string.IsNullOrWhiteSpace(requestedPrompt))
        {
            return requestedPrompt.Trim();
        }

        if (!string.IsNullOrWhiteSpace(currentPrompt))
        {
            return currentPrompt.Trim();
        }

        return DefaultSystemPrompt;
    }

    private static AdminGrowthAiAnalyzeRequestDto NormalizeAnalyzeRequest(AdminGrowthAiAnalyzeRequestDto request)
    {
        var normalizedFrom = request.FromUtc?.ToUniversalTime();
        var normalizedTo = request.ToUtc?.ToUniversalTime();

        if (normalizedFrom.HasValue && normalizedTo.HasValue && normalizedFrom > normalizedTo)
        {
            (normalizedFrom, normalizedTo) = (normalizedTo, normalizedFrom);
        }

        return request with
        {
            FromUtc = normalizedFrom,
            ToUtc = normalizedTo,
            Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim(),
            City = string.IsNullOrWhiteSpace(request.City) ? null : request.City.Trim(),
            ProposalSlaMinutes = Math.Clamp(request.ProposalSlaMinutes, 5, 720),
            AcceptanceSlaHours = Math.Clamp(request.AcceptanceSlaHours, 1, 168),
            LiquidityTake = Math.Clamp(request.LiquidityTake, 5, 100)
        };
    }

    private static string BuildUserPrompt(
        AdminGrowthAiAnalyzeRequestDto request,
        AdminGrowthFunnelDto funnel,
        AdminLiquidityScoreResponseDto liquidity,
        AdminGrowthExecutiveCockpitDto cockpit,
        AdminGrowthWeeklyRitualSnapshotDto weeklyRitual,
        AdminGrowthMonthlyReviewSnapshotDto monthlyReview)
    {
        var latestWeeklyRecord = weeklyRitual.RecentRecords
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefault();

        var latestMonthlyRecord = monthlyReview.RecentRecords
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefault();

        var compactPayload = new
        {
            request = new
            {
                fromUtc = request.FromUtc,
                toUtc = request.ToUtc,
                category = request.Category,
                city = request.City,
                proposalSlaMinutes = request.ProposalSlaMinutes,
                acceptanceSlaHours = request.AcceptanceSlaHours
            },
            funnel = new
            {
                totalRequests = funnel.RequestsTotal,
                withProposal = funnel.RequestsWithAnyProposal,
                withoutProposal = funnel.RequestsWithoutProposal,
                acceptedRequests = funnel.AcceptedRequests,
                scheduledOrBeyond = funnel.ScheduledOrBeyondRequests,
                completedRequests = funnel.CompletedRequests,
                firstProposalStage = funnel.FirstProposalStage,
                acceptanceStage = funnel.ProposalAcceptanceStage,
                alerts = funnel.Alerts.Take(MaxPromptItems).ToArray()
            },
            liquidity = new
            {
                formula = liquidity.FormulaDescription,
                topItems = liquidity.Items
                    .Take(MaxPromptItems)
                    .Select(item => new
                    {
                        item.Region,
                        item.Category,
                        item.DemandRequests,
                        item.RequestsWithProposal,
                        item.RequestsWithoutProposal,
                        item.DistinctProviders,
                        item.ProposalCoverageRatePercent,
                        item.FirstProposalSlaRatePercent,
                        item.LiquidityScore,
                        item.LiquidityBand
                    })
                    .ToArray(),
                alerts = liquidity.Alerts.Take(MaxPromptItems).ToArray()
            },
            cockpit = new
            {
                northStar = new
                {
                    cockpit.NorthStarName,
                    cockpit.NorthStarFormula,
                    cockpit.NorthStarRatePercent,
                    cockpit.NorthStarNumerator,
                    cockpit.NorthStarDenominator
                },
                quarterTargets = cockpit.QuarterTargets
                    .Take(MaxPromptItems)
                    .Select(item => new
                    {
                        item.QuarterCode,
                        item.TargetPercent,
                        item.CurrentPercent,
                        item.IsCurrentQuarter,
                        item.Status
                    })
                    .ToArray(),
                kpis = cockpit.Kpis
                    .Take(MaxPromptItems)
                    .Select(item => new
                    {
                        item.Code,
                        item.Label,
                        item.Value,
                        item.Unit,
                        item.TargetValue,
                        item.Description
                    })
                    .ToArray(),
                weeklyTrend = cockpit.WeeklyTrend
                    .Take(MaxPromptItems)
                    .Select(item => new
                    {
                        item.WeekStartUtc,
                        item.RequestsOpened,
                        item.RequestsWithProposal,
                        item.RequestsAccepted,
                        item.RequestsScheduledOrBeyond,
                        item.NorthStarRatePercent
                    })
                    .ToArray()
            },
            governance = new
            {
                weeklyRitual = new
                {
                    weeklyRitual.WeekStartUtc,
                    latestRecord = latestWeeklyRecord == null
                        ? null
                        : new
                        {
                            latestWeeklyRecord.CreatedAtUtc,
                            latestWeeklyRecord.Summary,
                            latestWeeklyRecord.Decisions,
                            latestWeeklyRecord.Risks,
                            latestWeeklyRecord.NextActions
                        }
                },
                monthlyReview = new
                {
                    monthlyReview.MonthStartUtc,
                    latestRecord = latestMonthlyRecord == null
                        ? null
                        : new
                        {
                            latestMonthlyRecord.CreatedAtUtc,
                            latestMonthlyRecord.ExecutiveSummary,
                            latestMonthlyRecord.StrategicDecisions,
                            latestMonthlyRecord.RisksAndBlockers,
                            latestMonthlyRecord.NextMonthBets
                        }
                }
            }
        };

        var payloadJson = JsonSerializer.Serialize(compactPayload, JsonOptions);
        return
            "Gere uma analise executiva para o ConsertaPraMim com base no payload JSON abaixo.\n" +
            "Responda OBRIGATORIAMENTE em JSON valido com o contrato:\n" +
            "{\n" +
            "  \"executiveSummary\": \"texto curto (max 700 chars)\",\n" +
            "  \"funnelInsights\": [\"insight 1\", \"insight 2\", \"... max 6\"],\n" +
            "  \"liquidityInsights\": [\"insight 1\", \"insight 2\", \"... max 6\"],\n" +
            "  \"risks\": [\"risco 1\", \"... max 6\"],\n" +
            "  \"recommendedActions\": [\"acao 1\", \"... max 8\"]\n" +
            "}\n\n" +
            "Regras:\n" +
            "- linguagem: portugues-BR.\n" +
            "- nada de markdown.\n" +
            "- objetivo: diagnostico acionavel para decisao semanal de growth.\n" +
            "- usar apenas informacoes do payload.\n\n" +
            "Payload:\n" +
            payloadJson;
    }

    private static ParsedInsights ParseInsights(string outputText)
    {
        if (string.IsNullOrWhiteSpace(outputText))
        {
            return ParsedInsights.Empty;
        }

        var jsonCandidate = TryExtractJson(outputText);
        if (string.IsNullOrWhiteSpace(jsonCandidate))
        {
            return new ParsedInsights(
                ExecutiveSummary: outputText.Trim(),
                FunnelInsights: Array.Empty<string>(),
                LiquidityInsights: Array.Empty<string>(),
                Risks: Array.Empty<string>(),
                RecommendedActions: Array.Empty<string>());
        }

        try
        {
            using var document = JsonDocument.Parse(jsonCandidate);
            var root = document.RootElement;

            var summary = GetString(root, "executiveSummary");
            var funnel = GetStringArray(root, "funnelInsights");
            var liquidity = GetStringArray(root, "liquidityInsights");
            var risks = GetStringArray(root, "risks");
            var actions = GetStringArray(root, "recommendedActions");

            if (string.IsNullOrWhiteSpace(summary))
            {
                summary = outputText.Trim();
            }

            return new ParsedInsights(
                ExecutiveSummary: summary,
                FunnelInsights: funnel,
                LiquidityInsights: liquidity,
                Risks: risks,
                RecommendedActions: actions);
        }
        catch (JsonException)
        {
            return new ParsedInsights(
                ExecutiveSummary: outputText.Trim(),
                FunnelInsights: Array.Empty<string>(),
                LiquidityInsights: Array.Empty<string>(),
                Risks: Array.Empty<string>(),
                RecommendedActions: Array.Empty<string>());
        }
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
            .Take(MaxPromptItems)
            .ToArray();
    }

    private static string MaskApiKey(string apiKey)
    {
        var trimmed = apiKey.Trim();
        if (trimmed.Length <= 8)
        {
            return new string('*', trimmed.Length);
        }

        return $"{trimmed[..4]}...{trimmed[^4..]}";
    }

    private sealed record ParsedInsights(
        string ExecutiveSummary,
        IReadOnlyList<string> FunnelInsights,
        IReadOnlyList<string> LiquidityInsights,
        IReadOnlyList<string> Risks,
        IReadOnlyList<string> RecommendedActions)
    {
        public static ParsedInsights Empty { get; } = new(
            ExecutiveSummary: "Analise nao disponivel.",
            FunnelInsights: Array.Empty<string>(),
            LiquidityInsights: Array.Empty<string>(),
            Risks: Array.Empty<string>(),
            RecommendedActions: Array.Empty<string>());
    }

    private const string DefaultSystemPrompt =
        "Voce e um analista senior de growth do ConsertaPraMim. " +
        "Seu foco e liquidez do marketplace de servicos (pedido->proposta->aceite), " +
        "SLA de primeira proposta, conversao e risco operacional por regiao/categoria. " +
        "Entregue diagnostico objetivo, sem texto generico, com acoes praticas e priorizadas.";
}
