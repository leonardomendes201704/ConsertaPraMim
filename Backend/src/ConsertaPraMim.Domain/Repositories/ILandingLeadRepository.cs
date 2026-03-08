using ConsertaPraMim.Domain.Entities;

namespace ConsertaPraMim.Domain.Repositories;

public interface ILandingLeadRepository
{
    Task AddAsync(LandingLead lead, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LandingLead>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LandingLead>> GetByPeriodAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
    Task<LandingLead?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
