using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Infrastructure.Repositories;

public class ServiceScopeChangeRequestRepository : IServiceScopeChangeRequestRepository
{
    private readonly ConsertaPraMimDbContext _context;

    public ServiceScopeChangeRequestRepository(ConsertaPraMimDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ServiceScopeChangeRequest>> GetByAppointmentIdAsync(Guid appointmentId)
    {
        return await _context.ServiceScopeChangeRequests
            .AsNoTracking()
            .Include(x => x.Attachments)
            .Where(x => x.ServiceAppointmentId == appointmentId)
            .OrderByDescending(x => x.Version)
            .ThenByDescending(x => x.RequestedAtUtc)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ServiceScopeChangeRequest>> GetByServiceRequestIdAsync(Guid serviceRequestId)
    {
        return await _context.ServiceScopeChangeRequests
            .AsNoTracking()
            .Include(x => x.Attachments)
            .Where(x => x.ServiceRequestId == serviceRequestId)
            .OrderByDescending(x => x.RequestedAtUtc)
            .ThenByDescending(x => x.Version)
            .ToListAsync();
    }

    public async Task<ServiceScopeChangeRequest?> GetByIdAsync(Guid scopeChangeRequestId)
    {
        return await _context.ServiceScopeChangeRequests
            .FirstOrDefaultAsync(x => x.Id == scopeChangeRequestId);
    }

    public async Task<ServiceScopeChangeRequest?> GetByIdWithAttachmentsAsync(Guid scopeChangeRequestId)
    {
        return await _context.ServiceScopeChangeRequests
            .Include(x => x.Attachments)
            .FirstOrDefaultAsync(x => x.Id == scopeChangeRequestId);
    }

    public async Task<ServiceScopeChangeRequest?> GetLatestByAppointmentIdAsync(Guid appointmentId)
    {
        return await _context.ServiceScopeChangeRequests
            .Where(x => x.ServiceAppointmentId == appointmentId)
            .OrderByDescending(x => x.Version)
            .ThenByDescending(x => x.RequestedAtUtc)
            .FirstOrDefaultAsync();
    }

    public async Task<ServiceScopeChangeRequest?> GetLatestByAppointmentIdAndStatusAsync(
        Guid appointmentId,
        ServiceScopeChangeRequestStatus status)
    {
        return await _context.ServiceScopeChangeRequests
            .Where(x => x.ServiceAppointmentId == appointmentId && x.Status == status)
            .OrderByDescending(x => x.Version)
            .ThenByDescending(x => x.RequestedAtUtc)
            .FirstOrDefaultAsync();
    }

    public async Task AddAsync(ServiceScopeChangeRequest scopeChangeRequest)
    {
        await _context.ServiceScopeChangeRequests.AddAsync(scopeChangeRequest);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ServiceScopeChangeRequest scopeChangeRequest)
    {
        _context.ServiceScopeChangeRequests.Update(scopeChangeRequest);
        await _context.SaveChangesAsync();
    }

    public async Task AddAttachmentAsync(ServiceScopeChangeRequestAttachment attachment)
    {
        await _context.ServiceScopeChangeRequestAttachments.AddAsync(attachment);
        await _context.SaveChangesAsync();
    }
}
