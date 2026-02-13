using ConsertaPraMim.Domain.Entities;

namespace ConsertaPraMim.Domain.Repositories;

public interface IAdminAuditLogRepository
{
    Task AddAsync(AdminAuditLog auditLog);
}
