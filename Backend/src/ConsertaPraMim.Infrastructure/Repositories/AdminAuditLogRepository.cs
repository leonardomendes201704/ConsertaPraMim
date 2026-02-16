using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Infrastructure.Repositories;

public class AdminAuditLogRepository : IAdminAuditLogRepository
{
    private readonly ConsertaPraMimDbContext _context;

    public AdminAuditLogRepository(ConsertaPraMimDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AdminAuditLog auditLog)
    {
        await _context.AdminAuditLogs.AddAsync(auditLog);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<AdminAuditLog>> GetByTargetAndPeriodAsync(
        string targetType,
        DateTime fromUtc,
        DateTime toUtc,
        Guid? actorUserId = null,
        Guid? targetId = null,
        string? action = null,
        int take = 2000)
    {
        var normalizedTargetType = string.IsNullOrWhiteSpace(targetType)
            ? string.Empty
            : targetType.Trim();
        var startUtc = fromUtc.ToUniversalTime();
        var endUtc = toUtc.ToUniversalTime();
        if (startUtc > endUtc)
        {
            (startUtc, endUtc) = (endUtc, startUtc);
        }

        var normalizedAction = string.IsNullOrWhiteSpace(action)
            ? null
            : action.Trim();
        var cappedTake = Math.Clamp(take, 1, 10000);

        var query = _context.AdminAuditLogs
            .AsNoTracking()
            .Where(x =>
                x.TargetType == normalizedTargetType &&
                x.CreatedAt >= startUtc &&
                x.CreatedAt <= endUtc);

        if (actorUserId.HasValue && actorUserId.Value != Guid.Empty)
        {
            query = query.Where(x => x.ActorUserId == actorUserId.Value);
        }

        if (targetId.HasValue && targetId.Value != Guid.Empty)
        {
            query = query.Where(x => x.TargetId == targetId.Value);
        }

        if (!string.IsNullOrWhiteSpace(normalizedAction))
        {
            query = query.Where(x => x.Action == normalizedAction);
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(cappedTake)
            .ToListAsync();
    }
}
