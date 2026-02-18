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

    public async Task<MobilePushDevice?> GetByTokenAndAppKindAsync(string token, string appKind, CancellationToken cancellationToken = default)
    {
        return await _context.MobilePushDevices
            .FirstOrDefaultAsync(
                d => d.Token == token && d.AppKind == appKind,
                cancellationToken);
    }

    public async Task<IReadOnlyList<MobilePushDevice>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.MobilePushDevices
            .Where(d => d.UserId == userId && d.IsActive)
            .OrderByDescending(d => d.LastRegisteredAtUtc)
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
        var devices = await _context.MobilePushDevices
            .Where(d => d.UserId == userId && d.AppKind == appKind && d.Token == token && d.IsActive)
            .ToListAsync(cancellationToken);

        if (devices.Count == 0)
        {
            return 0;
        }

        var now = DateTime.UtcNow;
        foreach (var device in devices)
        {
            device.IsActive = false;
            device.LastFailureAtUtc = now;
            device.LastFailureReason = TruncateReason(reason);
            device.UpdatedAt = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return devices.Count;
    }

    public async Task<int> DeactivateByUserAndDeviceIdExceptTokenAsync(
        Guid userId,
        string appKind,
        string deviceId,
        string keepToken,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return 0;
        }

        var query = _context.MobilePushDevices
            .Where(d => d.UserId == userId && d.AppKind == appKind && d.DeviceId == deviceId && d.IsActive);

        if (!string.IsNullOrWhiteSpace(keepToken))
        {
            query = query.Where(d => d.Token != keepToken);
        }

        var devices = await query.ToListAsync(cancellationToken);
        if (devices.Count == 0)
        {
            return 0;
        }

        var now = DateTime.UtcNow;
        foreach (var device in devices)
        {
            device.IsActive = false;
            device.LastFailureAtUtc = now;
            device.LastFailureReason = TruncateReason(reason);
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
