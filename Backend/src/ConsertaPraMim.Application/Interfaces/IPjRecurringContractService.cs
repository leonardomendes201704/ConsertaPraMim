using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.Interfaces;

public interface IPjRecurringContractService
{
    Task<IReadOnlyList<PjRecurringContractDto>> GetClientContractsAsync(
        Guid clientUserId,
        CancellationToken cancellationToken = default);

    Task<PjRecurringContractDto> CreateAsync(
        Guid clientUserId,
        CreatePjRecurringContractRequestDto request,
        CancellationToken cancellationToken = default);

    Task<PjRecurringContractDto> RenewAsync(
        Guid clientUserId,
        Guid contractId,
        RenewPjRecurringContractRequestDto request,
        CancellationToken cancellationToken = default);

    Task<AdminPjRecurringPortfolioDto> GetAdminPortfolioAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        PjRecurringContractStatus? status,
        CancellationToken cancellationToken = default);
}
