using ConsertaPraMim.Domain.Entities;

namespace ConsertaPraMim.Domain.Repositories;

public interface ILandingAccessEventRepository
{
    Task AddAsync(LandingAccessEvent accessEvent, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LandingAccessEvent>> GetByPeriodAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
    Task<LandingAccessEvent?> GetBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default);
}
