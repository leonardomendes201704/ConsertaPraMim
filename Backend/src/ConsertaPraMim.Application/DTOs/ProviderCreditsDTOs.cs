using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.DTOs;

public record ProviderCreditBalanceDto(
    Guid ProviderId,
    decimal CurrentBalance,
    DateTime? LastMovementAtUtc);

public record ProviderCreditStatementQueryDto(
    DateTime? FromUtc,
    DateTime? ToUtc,
    ProviderCreditLedgerEntryType? EntryType,
    ProviderCreditRevenueComponent? RevenueComponent,
    int Page = 1,
    int PageSize = 20);

public record ProviderCreditStatementItemDto(
    Guid EntryId,
    ProviderCreditLedgerEntryType EntryType,
    ProviderCreditRevenueComponent RevenueComponent,
    decimal Amount,
    decimal BalanceBefore,
    decimal BalanceAfter,
    string Reason,
    string? Source,
    string? ReferenceType,
    Guid? ReferenceId,
    DateTime EffectiveAtUtc,
    DateTime? ExpiresAtUtc,
    Guid? AdminUserId,
    string? AdminEmail,
    DateTime CreatedAt);

public record ProviderCreditStatementDto(
    Guid ProviderId,
    decimal CurrentBalance,
    DateTime? LastMovementAtUtc,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<ProviderCreditStatementItemDto> Items);

public record ProviderCreditMutationRequestDto(
    Guid ProviderId,
    ProviderCreditLedgerEntryType EntryType,
    decimal Amount,
    string Reason,
    string? Source = null,
    string? ReferenceType = null,
    Guid? ReferenceId = null,
    DateTime? EffectiveAtUtc = null,
    DateTime? ExpiresAtUtc = null,
    string? Metadata = null,
    ProviderCreditRevenueComponent? RevenueComponent = null);

public record ProviderCreditMutationResultDto(
    bool Success,
    ProviderCreditBalanceDto? Balance = null,
    ProviderCreditStatementItemDto? Entry = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);
