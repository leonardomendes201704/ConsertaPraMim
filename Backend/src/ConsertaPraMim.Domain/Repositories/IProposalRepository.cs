using ConsertaPraMim.Domain.Entities;

namespace ConsertaPraMim.Domain.Repositories;

public interface IProposalRepository
{
    Task AddAsync(Proposal proposal);
    Task<Proposal?> GetByIdAsync(Guid id);
    Task<IEnumerable<Proposal>> GetAllAsync();
    Task<IEnumerable<Proposal>> GetByRequestIdAsync(Guid requestId);
    Task<IEnumerable<Proposal>> GetByProviderIdAsync(Guid providerId);
    Task UpdateAsync(Proposal proposal);
}
