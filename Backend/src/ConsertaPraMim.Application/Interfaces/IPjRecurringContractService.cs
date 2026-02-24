using ConsertaPraMim.Application.DTOs;

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
}
