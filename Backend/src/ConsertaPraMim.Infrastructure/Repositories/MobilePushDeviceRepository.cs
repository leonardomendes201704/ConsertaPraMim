using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Infrastructure.Repositories;

public class MobilePushDeviceRepository : IMobilePushDeviceRepository
{
    private readonly ConsertaPraMimDbContext _context;

    public MobilePushDeviceRepository(ConsertaPraMimDbContext context)
    {
        _context = context;
    }

    public async Task<MobilePushDevice?> GetByTokenAndAppKindAsync(
        string token,
        string appKind,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(appKind))
        {
            return null;
        }

        var normalizedToken = token.Trim();
        var normalizedAppKind = appKind.Trim().ToLowerInvariant();
        return await _context.MobilePushDevices
            .FirstOrDefaultAsync(
                d => d.Token == normalizedToken && d.AppKind == normalizedAppKind,
                cancellationToken);
    }

    public async Task<MobilePushDevice?> GetByInstallationIdAndAppKindAsync(
        string installationId,
        string appKind,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(installationId) || string.IsNullOrWhiteSpace(appKind))
        {
            return null;
        }

        var normalizedInstallationId = installationId.Trim();
        var normalizedAppKind = appKind.Trim().ToLowerInvariant();
        return await _context.MobilePushDevices
            .FirstOrDefaultAsync(
                d => d.InstallationId == normalizedInstallationId && d.AppKind == normalizedAppKind,
                cancellationToken);
    }

    public async Task<IReadOnlyList<MobilePushDevice>> GetActiveByUserIdAsync(
        Guid userId,
        DateTime? minLastSeenAtUtc = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.MobilePushDevices
            .Where(d => d.UserId == userId && d.IsActive);

        if (minLastSeenAtUtc.HasValue)
        {
            var minValue = minLastSeenAtUtc.Value;
            query = query.Where(d => d.LastSeenAtUtc >= minValue);
        }

        return await query
            .OrderByDescending(d => d.LastSeenAtUtc)
            .ThenByDescending(d => d.LastRegisteredAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MobilePushDevice>> GetActiveByAppKindAsync(
        string appKind,
        DateTime? minLastSeenAtUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appKind))
        {
            return [];
        }

        var normalized = appKind.Trim().ToLowerInvariant();
        var query = _context.MobilePushDevices
            .Where(d => d.IsActive && d.AppKind == normalized);

        if (minLastSeenAtUtc.HasValue)
        {
            var minValue = minLastSeenAtUtc.Value;
            query = query.Where(d => d.LastSeenAtUtc >= minValue);
        }

        return await query
            .OrderByDescending(d => d.LastSeenAtUtc)
            .ThenByDescending(d => d.LastRegisteredAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MobilePushDevice>> GetLatestAsync(
        string? appKind,
        bool onlyActive,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var query = _context.MobilePushDevices.AsQueryable();

        if (onlyActive)
        {
            query = query.Where(d => d.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(appKind))
        {
            var normalizedAppKind = appKind.Trim().ToLowerInvariant();
            query = query.Where(d => d.AppKind == normalizedAppKind);
        }

        return await query
            .OrderByDescending(d => d.LastSeenAtUtc)
            .ThenByDescending(d => d.LastRegisteredAtUtc)
            .Take(safeLimit)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(MobilePushDevice device, CancellationToken cancellationToken = default)
    {
        await _context.MobilePushDevices.AddAsync(device, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(MobilePushDevice device, CancellationToken cancellationToken = default)
    {
        _context.MobilePushDevices.Update(device);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateRangeAsync(IEnumerable<MobilePushDevice> devices, CancellationToken cancellationToken = default)
    {
        _context.MobilePushDevices.UpdateRange(devices);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> DeactivateByUserAndTokenAsync(
        Guid userId,
        string appKind,
        string token,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(appKind))
        {
            return 0;
        }

        var normalizedToken = token.Trim();
        var normalizedAppKind = appKind.Trim().ToLowerInvariant();
        var devices = await _context.MobilePushDevices
            .Where(d => d.UserId == userId && d.AppKind == normalizedAppKind && d.Token == normalizedToken && d.IsActive)
            .ToListAsync(cancellationToken);

        return await DeactivateDevicesAsync(devices, reason, cancellationToken);
    }

    public async Task<int> DeactivateByUserAndInstallationIdAsync(
        Guid userId,
        string appKind,
        string installationId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(installationId) || string.IsNullOrWhiteSpace(appKind))
        {
            return 0;
        }

        var normalizedInstallationId = installationId.Trim();
        var normalizedAppKind = appKind.Trim().ToLowerInvariant();
        var devices = await _context.MobilePushDevices
            .Where(d => d.UserId == userId && d.AppKind == normalizedAppKind && d.InstallationId == normalizedInstallationId && d.IsActive)
            .ToListAsync(cancellationToken);

        return await DeactivateDevicesAsync(devices, reason, cancellationToken);
    }

    public async Task<int> DeactivateByUserAndDeviceIdAsync(
        Guid userId,
        string appKind,
        string deviceId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(appKind))
        {
            return 0;
        }

        var normalizedDeviceId = deviceId.Trim();
        var normalizedAppKind = appKind.Trim().ToLowerInvariant();
        var devices = await _context.MobilePushDevices
            .Where(d => d.UserId == userId && d.AppKind == normalizedAppKind && d.DeviceId == normalizedDeviceId && d.IsActive)
            .ToListAsync(cancellationToken);

        return await DeactivateDevicesAsync(devices, reason, cancellationToken);
    }

    public async Task<int> DeactivateByUserAndInstallationIdExceptIdAsync(
        Guid userId,
        string appKind,
        string installationId,
        Guid keepDeviceRecordId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(installationId) || string.IsNullOrWhiteSpace(appKind))
        {
            return 0;
        }

        var normalizedInstallationId = installationId.Trim();
        var normalizedAppKind = appKind.Trim().ToLowerInvariant();
        var devices = await _context.MobilePushDevices
            .Where(d =>
                d.UserId == userId &&
                d.AppKind == normalizedAppKind &&
                d.InstallationId == normalizedInstallationId &&
                d.Id != keepDeviceRecordId &&
                d.IsActive)
            .ToListAsync(cancellationToken);

        return await DeactivateDevicesAsync(devices, reason, cancellationToken);
    }

    public async Task<int> DeactivateStaleActiveAsync(
        DateTime staleBeforeUtc,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var devices = await _context.MobilePushDevices
            .Where(d => d.IsActive && d.LastSeenAtUtc < staleBeforeUtc)
            .ToListAsync(cancellationToken);

        return await DeactivateDevicesAsync(devices, reason, cancellationToken);
    }

    public async Task<int> DeleteInactiveOlderThanAsync(
        DateTime olderThanUtc,
        CancellationToken cancellationToken = default)
    {
        var devices = await _context.MobilePushDevices
            .Where(d => !d.IsActive && d.UpdatedAt.HasValue && d.UpdatedAt.Value < olderThanUtc)
            .ToListAsync(cancellationToken);

        if (devices.Count == 0)
        {
            return 0;
        }

        _context.MobilePushDevices.RemoveRange(devices);
        await _context.SaveChangesAsync(cancellationToken);
        return devices.Count;
    }

    private async Task<int> DeactivateDevicesAsync(
        IReadOnlyList<MobilePushDevice> devices,
        string reason,
        CancellationToken cancellationToken)
    {
        if (devices.Count == 0)
        {
            return 0;
        }

        var now = DateTime.UtcNow;
        var normalizedReason = TruncateReason(reason);
        foreach (var device in devices)
        {
            device.IsActive = false;
            device.RevokedAtUtc = now;
            device.LastFailureAtUtc = now;
            device.LastFailureReason = normalizedReason;
            device.UpdatedAt = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return devices.Count;
    }

    private static string TruncateReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return "unknown";
        }

        var normalized = reason.Trim();
        return normalized.Length <= 500 ? normalized : normalized[..500];
    }
}
