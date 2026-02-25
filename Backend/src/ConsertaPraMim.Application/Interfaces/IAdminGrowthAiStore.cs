using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminGrowthAiStore
{
    Task<AdminGrowthAiStoreSnapshot> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AdminGrowthAiStoreSnapshot snapshot, CancellationToken cancellationToken = default);
}

public record AdminGrowthAiStoreSnapshot(
    AdminGrowthAiStoreSettings? Settings,
    IReadOnlyList<AdminGrowthAiAnalysisDto> Analyses)
{
    public static AdminGrowthAiStoreSnapshot Empty { get; } = new(
        Settings: null,
        Analyses: Array.Empty<AdminGrowthAiAnalysisDto>());
}

public record AdminGrowthAiStoreSettings(
    bool Enabled,
    string Provider,
    string Model,
    string ApiKey,
    decimal Temperature,
    int MaxOutputTokens,
    string SystemPrompt,
    DateTime UpdatedAtUtc);
