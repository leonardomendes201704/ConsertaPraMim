using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IProviderCreditService
{
    Task<ProviderCreditBalanceDto> GetBalanceAsync(Guid providerId, CancellationToken cancellationToken = default);
    Task<ProviderCreditStatementDto> GetStatementAsync(
        Guid providerId,
        ProviderCreditStatementQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ProviderCreditMutationResultDto> ApplyMutationAsync(
        ProviderCreditMutationRequestDto request,
        Guid? actorUserId,
        string? actorEmail,
        CancellationToken cancellationToken = default);
}
