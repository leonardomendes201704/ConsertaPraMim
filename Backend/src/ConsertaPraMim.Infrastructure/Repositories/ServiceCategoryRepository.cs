using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Infrastructure.Repositories;

public class ServiceCategoryRepository : IServiceCategoryRepository
{
    private readonly ConsertaPraMimDbContext _context;

    public ServiceCategoryRepository(ConsertaPraMimDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ServiceCategoryDefinition>> GetAllAsync(bool includeInactive = true)
    {
        var query = _context.ServiceCategoryDefinitions.AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ServiceCategoryDefinition>> GetActiveAsync()
    {
        return await _context.ServiceCategoryDefinitions
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<ServiceCategoryDefinition?> GetByIdAsync(Guid id)
    {
        return await _context.ServiceCategoryDefinitions.FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<ServiceCategoryDefinition?> GetBySlugAsync(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        var normalized = slug.Trim().ToLowerInvariant();
        return await _context.ServiceCategoryDefinitions
            .FirstOrDefaultAsync(c => c.Slug.ToLower() == normalized);
    }

    public async Task<ServiceCategoryDefinition?> GetByNameAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var normalized = name.Trim().ToLowerInvariant();
        return await _context.ServiceCategoryDefinitions
            .FirstOrDefaultAsync(c => c.Name.ToLower() == normalized);
    }

    public async Task<ServiceCategoryDefinition?> GetFirstActiveByLegacyAsync(ServiceCategory legacyCategory)
    {
        return await _context.ServiceCategoryDefinitions
            .Where(c => c.IsActive && c.LegacyCategory == legacyCategory)
            .OrderBy(c => c.Name)
            .FirstOrDefaultAsync();
    }

    public async Task AddAsync(ServiceCategoryDefinition category)
    {
        await _context.ServiceCategoryDefinitions.AddAsync(category);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ServiceCategoryDefinition category)
    {
        _context.ServiceCategoryDefinitions.Update(category);
        await _context.SaveChangesAsync();
    }
}
