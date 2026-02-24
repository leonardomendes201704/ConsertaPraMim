using ConsertaPraMim.Domain.Entities;

namespace ConsertaPraMim.Domain.Repositories;

public interface IProposalComparisonInteractionRepository
{
    Task AddAsync(ProposalComparisonInteraction interaction, CancellationToken cancellationToken = default);

    Task<ProposalComparisonInteraction?> GetLatestByClientAndRequestAsync(
        Guid clientUserId,
        Guid requestId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProposalComparisonInteraction>> GetByWindowAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);
}

