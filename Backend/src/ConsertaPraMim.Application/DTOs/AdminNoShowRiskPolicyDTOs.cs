namespace ConsertaPraMim.Application.DTOs;

public record AdminNoShowRiskPolicyDto(
    Guid Id,
    string Name,
    bool IsActive,
    int LookbackDays,
    int MaxHistoryEventsPerActor,
    int MinClientHistoryRiskEvents,
    int MinProviderHistoryRiskEvents,
    int WeightClientNotConfirmed,
    int WeightProviderNotConfirmed,
    int WeightBothNotConfirmedBonus,
    int WeightWindowWithin24Hours,
    int WeightWindowWithin6Hours,
    int WeightWindowWithin2Hours,
    int WeightClientHistoryRisk,
    int WeightProviderHistoryRisk,
    int LowThresholdScore,
    int MediumThresholdScore,
    int HighThresholdScore,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record AdminUpdateNoShowRiskPolicyRequestDto(
    int LookbackDays,
    int MaxHistoryEventsPerActor,
    int MinClientHistoryRiskEvents,
    int MinProviderHistoryRiskEvents,
    int WeightClientNotConfirmed,
    int WeightProviderNotConfirmed,
    int WeightBothNotConfirmedBonus,
    int WeightWindowWithin24Hours,
    int WeightWindowWithin6Hours,
    int WeightWindowWithin2Hours,
    int WeightClientHistoryRisk,
    int WeightProviderHistoryRisk,
    int LowThresholdScore,
    int MediumThresholdScore,
    int HighThresholdScore,
    string? Notes = null);

public record AdminNoShowRiskPolicyUpdateResultDto(
    bool Success,
    AdminNoShowRiskPolicyDto? Policy = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);
