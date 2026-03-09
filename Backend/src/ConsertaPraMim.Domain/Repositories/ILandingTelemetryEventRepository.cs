using ConsertaPraMim.Domain.Entities;

namespace ConsertaPraMim.Domain.Repositories;

public interface ILandingTelemetryEventRepository
{
    Task AddRangeAsync(IReadOnlyCollection<LandingTelemetryEvent> events, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LandingTelemetryEvent>> GetByPeriodAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LandingTelemetryEvent>> GetBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default);
}
