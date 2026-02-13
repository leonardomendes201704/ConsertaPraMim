using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;

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
}
