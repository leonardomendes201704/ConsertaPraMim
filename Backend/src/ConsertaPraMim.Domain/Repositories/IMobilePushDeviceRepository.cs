using ConsertaPraMim.Domain.Entities;

namespace ConsertaPraMim.Domain.Repositories;

public interface IMobilePushDeviceRepository
{
    Task<MobilePushDevice?> GetByTokenAndAppKindAsync(string token, string appKind, CancellationToken cancellationToken = default);
    Task<MobilePushDevice?> GetByInstallationIdAndAppKindAsync(string installationId, string appKind, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MobilePushDevice>> GetActiveByUserIdAsync(Guid userId, DateTime? minLastSeenAtUtc = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MobilePushDevice>> GetActiveByAppKindAsync(string appKind, DateTime? minLastSeenAtUtc = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MobilePushDevice>> GetLatestAsync(string? appKind, bool onlyActive, int limit, CancellationToken cancellationToken = default);
    Task AddAsync(MobilePushDevice device, CancellationToken cancellationToken = default);
    Task UpdateAsync(MobilePushDevice device, CancellationToken cancellationToken = default);
    Task UpdateRangeAsync(IEnumerable<MobilePushDevice> devices, CancellationToken cancellationToken = default);
    Task<int> DeactivateByUserAndTokenAsync(Guid userId, string appKind, string token, string reason, CancellationToken cancellationToken = default);
    Task<int> DeactivateByUserAndInstallationIdAsync(Guid userId, string appKind, string installationId, string reason, CancellationToken cancellationToken = default);
    Task<int> DeactivateByUserAndDeviceIdAsync(Guid userId, string appKind, string deviceId, string reason, CancellationToken cancellationToken = default);
    Task<int> DeactivateByUserAndInstallationIdExceptIdAsync(Guid userId, string appKind, string installationId, Guid keepDeviceRecordId, string reason, CancellationToken cancellationToken = default);
    Task<int> DeactivateStaleActiveAsync(DateTime staleBeforeUtc, string reason, CancellationToken cancellationToken = default);
    Task<int> DeleteInactiveOlderThanAsync(DateTime olderThanUtc, CancellationToken cancellationToken = default);
}
