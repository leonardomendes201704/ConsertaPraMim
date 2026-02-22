using ConsertaPraMim.Domain.Entities;

namespace ConsertaPraMim.Domain.Repositories;

public interface IMobilePushDeviceRepository
{
    Task<MobilePushDevice?> GetByTokenAndAppKindAsync(string token, string appKind, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MobilePushDevice>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MobilePushDevice>> GetActiveByAppKindAsync(string appKind, CancellationToken cancellationToken = default);
    Task AddAsync(MobilePushDevice device, CancellationToken cancellationToken = default);
    Task UpdateAsync(MobilePushDevice device, CancellationToken cancellationToken = default);
    Task UpdateRangeAsync(IEnumerable<MobilePushDevice> devices, CancellationToken cancellationToken = default);
    Task<int> DeactivateByUserAndTokenAsync(Guid userId, string appKind, string token, string reason, CancellationToken cancellationToken = default);
    Task<int> DeactivateByUserAndDeviceIdExceptTokenAsync(Guid userId, string appKind, string deviceId, string keepToken, string reason, CancellationToken cancellationToken = default);
}
