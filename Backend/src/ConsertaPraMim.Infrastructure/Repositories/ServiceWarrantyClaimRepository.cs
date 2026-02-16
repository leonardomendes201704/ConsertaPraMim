using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Infrastructure.Repositories;

public class ServiceWarrantyClaimRepository : IServiceWarrantyClaimRepository
{
    private readonly ConsertaPraMimDbContext _context;

    public ServiceWarrantyClaimRepository(ConsertaPraMimDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceWarrantyClaim?> GetByIdAsync(Guid warrantyClaimId)
    {
        return await _context.ServiceWarrantyClaims
            .Include(x => x.ServiceRequest)
            .Include(x => x.ServiceAppointment)
            .Include(x => x.RevisitAppointment)
            .FirstOrDefaultAsync(x => x.Id == warrantyClaimId);
    }

    public async Task<ServiceWarrantyClaim?> GetLatestByAppointmentIdAsync(Guid appointmentId)
    {
        return await _context.ServiceWarrantyClaims
            .Where(x => x.ServiceAppointmentId == appointmentId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.RequestedAtUtc)
            .FirstOrDefaultAsync();
    }

    public async Task<ServiceWarrantyClaim?> GetByRevisitAppointmentIdAsync(Guid revisitAppointmentId)
    {
        return await _context.ServiceWarrantyClaims
            .Include(x => x.ServiceRequest)
            .Include(x => x.ServiceAppointment)
            .Include(x => x.RevisitAppointment)
            .FirstOrDefaultAsync(x => x.RevisitAppointmentId == revisitAppointmentId);
    }

    public async Task<IReadOnlyList<ServiceWarrantyClaim>> GetByAppointmentIdAsync(Guid appointmentId)
    {
        return await _context.ServiceWarrantyClaims
            .AsNoTracking()
            .Where(x => x.ServiceAppointmentId == appointmentId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.RequestedAtUtc)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ServiceWarrantyClaim>> GetByServiceRequestIdAsync(Guid serviceRequestId)
    {
        return await _context.ServiceWarrantyClaims
            .AsNoTracking()
            .Where(x => x.ServiceRequestId == serviceRequestId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.RequestedAtUtc)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ServiceWarrantyClaim>> GetByProviderAndStatusAsync(
        Guid providerId,
        ServiceWarrantyClaimStatus status)
    {
        return await _context.ServiceWarrantyClaims
            .AsNoTracking()
            .Where(x => x.ProviderId == providerId && x.Status == status)
            .OrderBy(x => x.ProviderResponseDueAtUtc)
            .ThenByDescending(x => x.RequestedAtUtc)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ServiceWarrantyClaim>> GetPendingProviderReviewOverdueAsync(DateTime asOfUtc, int take = 200)
    {
        var cappedTake = Math.Clamp(take, 1, 1000);
        return await _context.ServiceWarrantyClaims
            .Include(x => x.ServiceRequest)
            .Include(x => x.ServiceAppointment)
            .Where(x => x.Status == ServiceWarrantyClaimStatus.PendingProviderReview)
            .Where(x => x.ProviderResponseDueAtUtc <= asOfUtc)
            .OrderBy(x => x.ProviderResponseDueAtUtc)
            .ThenBy(x => x.RequestedAtUtc)
            .Take(cappedTake)
            .ToListAsync();
    }

    public async Task AddAsync(ServiceWarrantyClaim warrantyClaim)
    {
        await _context.ServiceWarrantyClaims.AddAsync(warrantyClaim);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ServiceWarrantyClaim warrantyClaim)
    {
        _context.ServiceWarrantyClaims.Update(warrantyClaim);
        await _context.SaveChangesAsync();
    }
}
