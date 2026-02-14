using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Infrastructure.Repositories;

public class ServiceChecklistRepository : IServiceChecklistRepository
{
    private readonly ConsertaPraMimDbContext _context;

    public ServiceChecklistRepository(ConsertaPraMimDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ServiceChecklistTemplate>> GetTemplatesAsync(bool includeInactive)
    {
        var query = _context.ServiceChecklistTemplates
            .AsNoTracking()
            .Include(t => t.CategoryDefinition)
            .Include(t => t.Items)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(t => t.IsActive);
        }

        return await query
            .OrderBy(t => t.CategoryDefinition.Name)
            .ThenBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<ServiceChecklistTemplate?> GetTemplateByIdAsync(Guid templateId)
    {
        return await _context.ServiceChecklistTemplates
            .Include(t => t.CategoryDefinition)
            .Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.Id == templateId);
    }

    public async Task<ServiceChecklistTemplate?> GetTemplateByCategoryDefinitionAsync(Guid categoryDefinitionId, bool onlyActive = true)
    {
        var query = _context.ServiceChecklistTemplates
            .Include(t => t.CategoryDefinition)
            .Include(t => t.Items)
            .Where(t => t.CategoryDefinitionId == categoryDefinitionId);

        if (onlyActive)
        {
            query = query.Where(t => t.IsActive);
        }

        return await query
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<ServiceChecklistTemplate?> GetTemplateByLegacyCategoryAsync(ServiceCategory legacyCategory, bool onlyActive = true)
    {
        var query = _context.ServiceChecklistTemplates
            .Include(t => t.CategoryDefinition)
            .Include(t => t.Items)
            .Where(t => t.CategoryDefinition.LegacyCategory == legacyCategory);

        if (onlyActive)
        {
            query = query.Where(t => t.IsActive);
        }

        return await query
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task AddTemplateAsync(ServiceChecklistTemplate template)
    {
        await _context.ServiceChecklistTemplates.AddAsync(template);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateTemplateAsync(ServiceChecklistTemplate template)
    {
        _context.ServiceChecklistTemplates.Update(template);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<ServiceAppointmentChecklistResponse>> GetResponsesByAppointmentAsync(Guid appointmentId)
    {
        return await _context.ServiceAppointmentChecklistResponses
            .Include(r => r.TemplateItem)
            .Where(r => r.ServiceAppointmentId == appointmentId)
            .OrderBy(r => r.TemplateItem.SortOrder)
            .ThenBy(r => r.TemplateItem.Title)
            .ToListAsync();
    }

    public async Task<ServiceAppointmentChecklistResponse?> GetResponseByAppointmentAndItemAsync(Guid appointmentId, Guid templateItemId)
    {
        return await _context.ServiceAppointmentChecklistResponses
            .Include(r => r.TemplateItem)
            .FirstOrDefaultAsync(r => r.ServiceAppointmentId == appointmentId && r.TemplateItemId == templateItemId);
    }

    public async Task AddResponseAsync(ServiceAppointmentChecklistResponse response)
    {
        await _context.ServiceAppointmentChecklistResponses.AddAsync(response);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateResponseAsync(ServiceAppointmentChecklistResponse response)
    {
        _context.ServiceAppointmentChecklistResponses.Update(response);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<ServiceAppointmentChecklistHistory>> GetHistoryByAppointmentAsync(Guid appointmentId)
    {
        return await _context.ServiceAppointmentChecklistHistories
            .AsNoTracking()
            .Include(h => h.TemplateItem)
            .Where(h => h.ServiceAppointmentId == appointmentId)
            .OrderByDescending(h => h.OccurredAtUtc)
            .ToListAsync();
    }

    public async Task AddHistoryAsync(ServiceAppointmentChecklistHistory history)
    {
        await _context.ServiceAppointmentChecklistHistories.AddAsync(history);
        await _context.SaveChangesAsync();
    }
}
