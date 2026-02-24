using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.DTOs;

public record PjRecurringContractDto(
    Guid Id,
    Guid ClientUserId,
    ClientPjType ClientPjType,
    ServiceCategory Category,
    ProviderClientPreference ProviderEligibility,
    string Title,
    string? Description,
    PjRecurringCadence Cadence,
    PjRecurringContractStatus Status,
    decimal MonthlyAmount,
    int IncludedVisitsPerCycle,
    int ResponseSlaHours,
    int OperationalWindowStartMinute,
    int OperationalWindowEndMinute,
    int OperationalDaysMask,
    DateTime StartsAtUtc,
    DateTime NextRenewalAtUtc,
    DateTime? EndsAtUtc,
    DateTime? LastRenewedAtUtc,
    DateTime? LastPaymentAtUtc,
    bool AutoRenew,
    string? CancellationReason,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int EligibleProvidersCount);

public record CreatePjRecurringContractRequestDto(
    ClientPjType ClientPjType,
    ServiceCategory Category,
    ProviderClientPreference ProviderEligibility,
    string Title,
    string? Description,
    PjRecurringCadence Cadence,
    decimal MonthlyAmount,
    int IncludedVisitsPerCycle,
    int ResponseSlaHours,
    int OperationalWindowStartMinute,
    int OperationalWindowEndMinute,
    int OperationalDaysMask,
    DateTime StartsAtUtc,
    bool AutoRenew,
    DateTime? EndsAtUtc = null);

public record RenewPjRecurringContractRequestDto(
    DateTime? RenewedAtUtc = null,
    string? Note = null);
