using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminProviderCreditService
{
    Task<AdminProviderCreditUsageReportDto> GetUsageReportAsync(
        AdminProviderCreditUsageReportQueryDto query,
        CancellationToken cancellationToken = default);

    Task<AdminProviderCreditMutationResultDto> GrantAsync(
        AdminProviderCreditGrantRequestDto request,
        Guid actorUserId,
        string actorEmail,
        CancellationToken cancellationToken = default);

    Task<AdminProviderCreditMutationResultDto> ReverseAsync(
        AdminProviderCreditReversalRequestDto request,
        Guid actorUserId,
        string actorEmail,
        CancellationToken cancellationToken = default);
}
