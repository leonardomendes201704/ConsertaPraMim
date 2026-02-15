using ConsertaPraMim.Domain.Entities;

namespace ConsertaPraMim.Domain.Repositories;

public interface IServiceAppointmentNoShowRiskPolicyRepository
{
    Task<ServiceAppointmentNoShowRiskPolicy?> GetActiveAsync();
    Task<IReadOnlyList<ServiceAppointmentNoShowRiskPolicy>> GetAllAsync();
    Task AddAsync(ServiceAppointmentNoShowRiskPolicy policy);
    Task UpdateAsync(ServiceAppointmentNoShowRiskPolicy policy);
}
