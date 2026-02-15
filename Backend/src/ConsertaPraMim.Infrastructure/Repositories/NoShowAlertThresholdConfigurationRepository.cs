using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Infrastructure.Repositories;

public class NoShowAlertThresholdConfigurationRepository : INoShowAlertThresholdConfigurationRepository
{
    private readonly ConsertaPraMimDbContext _context;

    public NoShowAlertThresholdConfigurationRepository(ConsertaPraMimDbContext context)
    {
        _context = context;
    }

    public async Task<NoShowAlertThresholdConfiguration?> GetActiveAsync()
    {
        return await _context.NoShowAlertThresholdConfigurations
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<NoShowAlertThresholdConfiguration>> GetAllAsync()
    {
        return await _context.NoShowAlertThresholdConfigurations
            .AsNoTracking()
            .OrderByDescending(c => c.IsActive)
            .ThenByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(NoShowAlertThresholdConfiguration configuration)
    {
        await _context.NoShowAlertThresholdConfigurations.AddAsync(configuration);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(NoShowAlertThresholdConfiguration configuration)
    {
        _context.NoShowAlertThresholdConfigurations.Update(configuration);
        await _context.SaveChangesAsync();
    }
}
