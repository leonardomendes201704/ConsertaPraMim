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

public record AdminPjRecurringPortfolioDto(
    DateTime GeneratedAtUtc,
    int TotalContracts,
    int ActiveContracts,
    int DelinquentContracts,
    decimal MonthlyRecurringRevenue,
    decimal AverageTicket,
    IReadOnlyList<AdminPjRecurringStatusBreakdownDto> StatusBreakdown,
    IReadOnlyList<AdminPjRecurringCategoryBreakdownDto> CategoryBreakdown,
    IReadOnlyList<AdminPjRecurringPortfolioItemDto> Contracts);

public record AdminPjRecurringStatusBreakdownDto(
    PjRecurringContractStatus Status,
    int Count,
    decimal MonthlyRecurringRevenue);

public record AdminPjRecurringCategoryBreakdownDto(
    ServiceCategory Category,
    int Count,
    decimal MonthlyRecurringRevenue);

public record AdminPjRecurringPortfolioItemDto(
    Guid ContractId,
    Guid ClientUserId,
    string ClientName,
    ClientPjType ClientPjType,
    ServiceCategory Category,
    ProviderClientPreference ProviderEligibility,
    PjRecurringContractStatus Status,
    decimal MonthlyAmount,
    int IncludedVisitsPerCycle,
    int ResponseSlaHours,
    DateTime StartsAtUtc,
    DateTime NextRenewalAtUtc,
    DateTime? EndsAtUtc,
    DateTime? LastPaymentAtUtc,
    bool AutoRenew,
    int EligibleProvidersCount);
