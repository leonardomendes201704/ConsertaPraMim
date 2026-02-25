namespace ConsertaPraMim.Application.DTOs;

public record AdminGrowthAiSnapshotDto(
    AdminGrowthAiSettingsDto Settings,
    IReadOnlyList<AdminGrowthAiAnalysisDto> RecentAnalyses);

public record AdminGrowthAiSettingsDto(
    bool Enabled,
    bool IsConfigured,
    string Provider,
    string Model,
    decimal Temperature,
    int MaxOutputTokens,
    string SystemPrompt,
    string? ApiKeyMasked,
    DateTime? UpdatedAtUtc,
    DateTime? LastAnalysisAtUtc);

public record AdminGrowthAiUpsertSettingsRequestDto(
    bool Enabled,
    string? ApiKey,
    string? Model,
    decimal Temperature = 0.20m,
    int MaxOutputTokens = 900,
    string? SystemPrompt = null);

public record AdminGrowthAiAnalyzeRequestDto(
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? Category,
    string? City,
    int ProposalSlaMinutes = 30,
    int AcceptanceSlaHours = 24,
    int LiquidityTake = 10);

public record AdminGrowthAiAnalyzeResultDto(
    bool Success,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    AdminGrowthAiAnalysisDto? Analysis = null);

public record AdminGrowthAiAnalysisDto(
    Guid AnalysisId,
    DateTime CreatedAtUtc,
    string ActorEmail,
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? Category,
    string? City,
    string ExecutiveSummary,
    IReadOnlyList<string> FunnelInsights,
    IReadOnlyList<string> LiquidityInsights,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> RecommendedActions,
    string Model,
    int? InputTokens,
    int? OutputTokens,
    int? TotalTokens);
