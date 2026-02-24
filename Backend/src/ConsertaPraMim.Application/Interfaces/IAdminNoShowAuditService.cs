using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminNoShowAuditService
{
    Task<AdminNoShowAuditDto> GetAuditAsync(AdminNoShowAuditQueryDto query);
}
