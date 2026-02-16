using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Repositories;

public interface IServiceWarrantyClaimRepository
{
    Task<ServiceWarrantyClaim?> GetByIdAsync(Guid warrantyClaimId);
    Task<ServiceWarrantyClaim?> GetLatestByAppointmentIdAsync(Guid appointmentId);
    Task<IReadOnlyList<ServiceWarrantyClaim>> GetByAppointmentIdAsync(Guid appointmentId);
    Task<IReadOnlyList<ServiceWarrantyClaim>> GetByServiceRequestIdAsync(Guid serviceRequestId);
    Task<IReadOnlyList<ServiceWarrantyClaim>> GetByProviderAndStatusAsync(Guid providerId, ServiceWarrantyClaimStatus status);
    Task<IReadOnlyList<ServiceWarrantyClaim>> GetPendingProviderReviewOverdueAsync(DateTime asOfUtc, int take = 200);
    Task AddAsync(ServiceWarrantyClaim warrantyClaim);
    Task UpdateAsync(ServiceWarrantyClaim warrantyClaim);
}
