using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.DTOs;

public record ServiceFinancialCalculationRequestDto(
    ServiceFinancialPolicyEventType EventType,
    decimal ServiceValue,
    DateTime WindowStartUtc,
    DateTime EventOccurredAtUtc);

public record ServiceFinancialPolicyOverrideRequestDto(
    ServiceFinancialPolicyEventType EventType,
    string Justification,
    DateTime? EventOccurredAtUtc = null);

public record ServiceFinancialCalculationResultDto(
    bool Success,
    ServiceFinancialCalculationBreakdownDto? Breakdown = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record ServiceFinancialCalculationBreakdownDto(
    Guid RuleId,
    string RuleName,
    ServiceFinancialPolicyEventType EventType,
    decimal ServiceValue,
    double HoursBeforeWindowStart,
    int MinHoursBeforeWindowStart,
    int? MaxHoursBeforeWindowStart,
    int Priority,
    decimal PenaltyPercent,
    decimal PenaltyAmount,
    decimal CounterpartyCompensationPercent,
    decimal CounterpartyCompensationAmount,
    decimal PlatformRetainedPercent,
    decimal PlatformRetainedAmount,
    decimal RemainingAmount,
    string CounterpartyActorLabel,
    string CalculationMemo);
