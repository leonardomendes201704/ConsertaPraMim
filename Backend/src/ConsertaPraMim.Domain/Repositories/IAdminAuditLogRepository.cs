using ConsertaPraMim.Domain.Entities;

namespace ConsertaPraMim.Domain.Repositories;

public interface IAdminAuditLogRepository
{
    Task AddAsync(AdminAuditLog auditLog);
    Task<IReadOnlyList<AdminAuditLog>> GetByTargetAndPeriodAsync(
        string targetType,
        DateTime fromUtc,
        DateTime toUtc,
        Guid? actorUserId = null,
        Guid? targetId = null,
        string? action = null,
        int take = 2000);
}
