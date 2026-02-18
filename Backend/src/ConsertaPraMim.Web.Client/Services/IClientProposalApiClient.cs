namespace ConsertaPraMim.Web.Client.Services;

public interface IClientProposalApiClient
{
    Task<(bool Success, string? ErrorMessage)> AcceptAsync(Guid proposalId, CancellationToken cancellationToken = default);
}
