using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Repositories;

public interface IServiceAppointmentRepository
{
    Task<IReadOnlyList<ProviderAvailabilityRule>> GetAvailabilityRulesByProviderAsync(Guid providerId);
    Task<IReadOnlyList<ProviderAvailabilityException>> GetAvailabilityExceptionsByProviderAsync(Guid providerId, DateTime rangeStartUtc, DateTime rangeEndUtc);

    Task AddAvailabilityRuleAsync(ProviderAvailabilityRule rule);
    Task UpdateAvailabilityRuleAsync(ProviderAvailabilityRule rule);
    Task RemoveAvailabilityRuleAsync(ProviderAvailabilityRule rule);

    Task AddAvailabilityExceptionAsync(ProviderAvailabilityException exception);
    Task UpdateAvailabilityExceptionAsync(ProviderAvailabilityException exception);
    Task RemoveAvailabilityExceptionAsync(ProviderAvailabilityException exception);

    Task<ServiceAppointment?> GetByIdAsync(Guid appointmentId);
    Task<ServiceAppointment?> GetByRequestIdAsync(Guid requestId);
    Task<IReadOnlyList<ServiceAppointment>> GetByProviderAsync(Guid providerId, DateTime? fromUtc = null, DateTime? toUtc = null);
    Task<IReadOnlyList<ServiceAppointment>> GetByClientAsync(Guid clientId, DateTime? fromUtc = null, DateTime? toUtc = null);
    Task<IReadOnlyList<ServiceAppointment>> GetProviderAppointmentsByStatusesInRangeAsync(
        Guid providerId,
        DateTime rangeStartUtc,
        DateTime rangeEndUtc,
        IReadOnlyCollection<ServiceAppointmentStatus> statuses);

    Task AddAsync(ServiceAppointment appointment);
    Task UpdateAsync(ServiceAppointment appointment);
    Task AddHistoryAsync(ServiceAppointmentHistory history);
}
