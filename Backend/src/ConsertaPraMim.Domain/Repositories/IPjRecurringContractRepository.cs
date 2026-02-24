using ConsertaPraMim.Domain.Entities;

namespace ConsertaPraMim.Domain.Repositories;

public interface IPjRecurringContractRepository
{
    Task<IReadOnlyList<PjRecurringContract>> ListAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PjRecurringContract>> ListByClientUserIdAsync(Guid clientUserId, CancellationToken cancellationToken = default);
    Task<PjRecurringContract?> GetByIdAsync(Guid contractId, CancellationToken cancellationToken = default);
    Task AddAsync(PjRecurringContract contract, CancellationToken cancellationToken = default);
    Task UpdateAsync(PjRecurringContract contract, CancellationToken cancellationToken = default);
}
