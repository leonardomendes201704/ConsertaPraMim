using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Web.Admin.Models;

public sealed class AdminGrowthAiViewModel
{
    public AdminGrowthAiSettingsFormModel SettingsForm { get; set; } = new();
    public AdminGrowthAiAnalyzeFormModel AnalyzeForm { get; set; } = new();
    public AdminGrowthAiCompareFormModel CompareForm { get; set; } = new();
    public AdminGrowthAiSnapshotDto? Snapshot { get; set; }
    public AdminGrowthAiAnalysisDto? LatestAnalysis { get; set; }
    public AdminGrowthAiComparisonDto? LatestComparison { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
}

public sealed class AdminGrowthAiSettingsFormModel
{
    public bool Enabled { get; set; }
    public string Model { get; set; } = "gpt-4.1-mini";
    public decimal Temperature { get; set; } = 0.20m;
    public int MaxOutputTokens { get; set; } = 900;
    public string SystemPrompt { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string? ApiKeyMasked { get; set; }
    public bool IsConfigured { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime? LastAnalysisAtUtc { get; set; }
}

public sealed class AdminGrowthAiAnalyzeFormModel
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string? Category { get; set; }
    public string? City { get; set; }
    public int ProposalSlaMinutes { get; set; } = 30;
    public int AcceptanceSlaHours { get; set; } = 24;
    public int LiquidityTake { get; set; } = 10;
}

public sealed class AdminGrowthAiCompareFormModel
{
    public Guid? BaseAnalysisId { get; set; }
    public Guid? TargetAnalysisId { get; set; }
}
