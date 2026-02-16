using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Infrastructure.Repositories;

public class ServiceDisputeCaseRepository : IServiceDisputeCaseRepository
{
    private static readonly DisputeCaseStatus[] OpenStatuses =
    {
        DisputeCaseStatus.Open,
        DisputeCaseStatus.UnderReview,
        DisputeCaseStatus.WaitingParties
    };

    private readonly ConsertaPraMimDbContext _context;

    public ServiceDisputeCaseRepository(ConsertaPraMimDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceDisputeCase?> GetByIdAsync(Guid disputeCaseId)
    {
        return await _context.ServiceDisputeCases
            .FirstOrDefaultAsync(x => x.Id == disputeCaseId);
    }

    public async Task<ServiceDisputeCase?> GetByIdWithDetailsAsync(Guid disputeCaseId)
    {
        return await _context.ServiceDisputeCases
            .Include(x => x.ServiceRequest)
            .Include(x => x.ServiceAppointment)
            .Include(x => x.OpenedByUser)
            .Include(x => x.CounterpartyUser)
            .Include(x => x.OwnedByAdminUser)
            .Include(x => x.Messages)
                .ThenInclude(m => m.AuthorUser)
            .Include(x => x.Attachments)
                .ThenInclude(a => a.UploadedByUser)
            .Include(x => x.AuditEntries)
                .ThenInclude(a => a.ActorUser)
            .FirstOrDefaultAsync(x => x.Id == disputeCaseId);
    }

    public async Task<IReadOnlyList<ServiceDisputeCase>> GetByServiceRequestIdAsync(Guid serviceRequestId)
    {
        return await _context.ServiceDisputeCases
            .AsNoTracking()
            .Include(x => x.Messages)
            .Include(x => x.Attachments)
            .Include(x => x.AuditEntries)
            .Where(x => x.ServiceRequestId == serviceRequestId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ServiceDisputeCase>> GetByAppointmentIdAsync(Guid appointmentId)
    {
        return await _context.ServiceDisputeCases
            .AsNoTracking()
            .Include(x => x.Messages)
            .Include(x => x.Attachments)
            .Include(x => x.AuditEntries)
            .Where(x => x.ServiceAppointmentId == appointmentId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ServiceDisputeCase>> GetOpenCasesAsync(int take = 200)
    {
        var cappedTake = Math.Clamp(take, 1, 2000);
        return await _context.ServiceDisputeCases
            .AsNoTracking()
            .Include(x => x.ServiceRequest)
            .Include(x => x.OpenedByUser)
            .Include(x => x.CounterpartyUser)
            .Include(x => x.OwnedByAdminUser)
            .Include(x => x.Messages)
            .Include(x => x.Attachments)
            .Where(x => OpenStatuses.Contains(x.Status))
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.SlaDueAtUtc)
            .ThenByDescending(x => x.CreatedAt)
            .Take(cappedTake)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ServiceDisputeCase>> GetCasesByOpenedPeriodAsync(DateTime fromUtc, DateTime toUtc, int take = 5000)
    {
        var startUtc = fromUtc.ToUniversalTime();
        var endUtc = toUtc.ToUniversalTime();
        if (startUtc > endUtc)
        {
            (startUtc, endUtc) = (endUtc, startUtc);
        }

        var cappedTake = Math.Clamp(take, 1, 20000);
        return await _context.ServiceDisputeCases
            .AsNoTracking()
            .Include(x => x.ServiceRequest)
            .Include(x => x.OpenedByUser)
            .Where(x => x.OpenedAtUtc >= startUtc && x.OpenedAtUtc <= endUtc)
            .OrderByDescending(x => x.OpenedAtUtc)
            .Take(cappedTake)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ServiceDisputeCaseAuditEntry>> GetAuditEntriesByPeriodAsync(
        DateTime fromUtc,
        DateTime toUtc,
        Guid? actorUserId = null,
        Guid? disputeCaseId = null,
        string? eventType = null,
        int take = 2000)
    {
        var startUtc = fromUtc.ToUniversalTime();
        var endUtc = toUtc.ToUniversalTime();
        if (startUtc > endUtc)
        {
            (startUtc, endUtc) = (endUtc, startUtc);
        }

        var normalizedEventType = string.IsNullOrWhiteSpace(eventType)
            ? null
            : eventType.Trim();
        var cappedTake = Math.Clamp(take, 1, 10000);

        var query = _context.ServiceDisputeCaseAuditEntries
            .AsNoTracking()
            .Include(x => x.ActorUser)
            .Where(x => x.CreatedAt >= startUtc && x.CreatedAt <= endUtc);

        if (actorUserId.HasValue && actorUserId.Value != Guid.Empty)
        {
            query = query.Where(x => x.ActorUserId == actorUserId.Value);
        }

        if (disputeCaseId.HasValue && disputeCaseId.Value != Guid.Empty)
        {
            query = query.Where(x => x.ServiceDisputeCaseId == disputeCaseId.Value);
        }

        if (!string.IsNullOrWhiteSpace(normalizedEventType))
        {
            query = query.Where(x => x.EventType == normalizedEventType);
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(cappedTake)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ServiceDisputeCase>> GetClosedCasesClosedBeforeAsync(DateTime closedBeforeUtc, int take = 500)
    {
        var cutoffUtc = closedBeforeUtc.ToUniversalTime();
        var cappedTake = Math.Clamp(take, 1, 5000);
        return await _context.ServiceDisputeCases
            .Include(x => x.Messages)
            .Include(x => x.Attachments)
            .Where(x => x.ClosedAtUtc.HasValue && x.ClosedAtUtc.Value <= cutoffUtc)
            .OrderBy(x => x.ClosedAtUtc)
            .ThenBy(x => x.CreatedAt)
            .Take(cappedTake)
            .ToListAsync();
    }

    public async Task<bool> HasOpenDisputeAsync(Guid serviceRequestId)
    {
        return await _context.ServiceDisputeCases
            .AsNoTracking()
            .AnyAsync(x => x.ServiceRequestId == serviceRequestId && OpenStatuses.Contains(x.Status));
    }

    public async Task AddAsync(ServiceDisputeCase disputeCase)
    {
        await _context.ServiceDisputeCases.AddAsync(disputeCase);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ServiceDisputeCase disputeCase)
    {
        _context.ServiceDisputeCases.Update(disputeCase);
        await _context.SaveChangesAsync();
    }

    public async Task AddMessageAsync(ServiceDisputeCaseMessage message)
    {
        await _context.ServiceDisputeCaseMessages.AddAsync(message);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateMessageAsync(ServiceDisputeCaseMessage message)
    {
        _context.ServiceDisputeCaseMessages.Update(message);
        await _context.SaveChangesAsync();
    }

    public async Task AddAttachmentAsync(ServiceDisputeCaseAttachment attachment)
    {
        await _context.ServiceDisputeCaseAttachments.AddAsync(attachment);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAttachmentAsync(ServiceDisputeCaseAttachment attachment)
    {
        _context.ServiceDisputeCaseAttachments.Update(attachment);
        await _context.SaveChangesAsync();
    }

    public async Task AddAuditEntryAsync(ServiceDisputeCaseAuditEntry auditEntry)
    {
        await _context.ServiceDisputeCaseAuditEntries.AddAsync(auditEntry);
        await _context.SaveChangesAsync();
    }
}
