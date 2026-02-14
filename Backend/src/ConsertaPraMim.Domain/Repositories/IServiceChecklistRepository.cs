using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Repositories;

public interface IServiceChecklistRepository
{
    Task<IReadOnlyList<ServiceChecklistTemplate>> GetTemplatesAsync(bool includeInactive);
    Task<ServiceChecklistTemplate?> GetTemplateByIdAsync(Guid templateId);
    Task<ServiceChecklistTemplate?> GetTemplateByCategoryDefinitionAsync(Guid categoryDefinitionId, bool onlyActive = true);
    Task<ServiceChecklistTemplate?> GetTemplateByLegacyCategoryAsync(ServiceCategory legacyCategory, bool onlyActive = true);
    Task AddTemplateAsync(ServiceChecklistTemplate template);
    Task UpdateTemplateAsync(ServiceChecklistTemplate template);

    Task<IReadOnlyList<ServiceAppointmentChecklistResponse>> GetResponsesByAppointmentAsync(Guid appointmentId);
    Task<ServiceAppointmentChecklistResponse?> GetResponseByAppointmentAndItemAsync(Guid appointmentId, Guid templateItemId);
    Task AddResponseAsync(ServiceAppointmentChecklistResponse response);
    Task UpdateResponseAsync(ServiceAppointmentChecklistResponse response);

    Task<IReadOnlyList<ServiceAppointmentChecklistHistory>> GetHistoryByAppointmentAsync(Guid appointmentId);
    Task AddHistoryAsync(ServiceAppointmentChecklistHistory history);
}
