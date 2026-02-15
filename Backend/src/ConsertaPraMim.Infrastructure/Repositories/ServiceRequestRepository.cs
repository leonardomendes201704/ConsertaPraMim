using Microsoft.EntityFrameworkCore;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Infrastructure.Data;

namespace ConsertaPraMim.Infrastructure.Repositories;

public class ServiceRequestRepository : IServiceRequestRepository
{
    private readonly ConsertaPraMimDbContext _context;

    public ServiceRequestRepository(ConsertaPraMimDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ServiceRequest request)
    {
        await _context.ServiceRequests.AddAsync(request);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<ServiceRequest>> GetByClientIdAsync(Guid clientId)
    {
        return await _context.ServiceRequests
            .Include(r => r.Proposals)
            .Include(r => r.CategoryDefinition)
            .Where(r => r.ClientId == clientId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ServiceRequest>> GetAllAsync()
    {
        return await _context.ServiceRequests
            .Include(r => r.Client)
            .Include(r => r.CategoryDefinition)
            .Include(r => r.PaymentTransactions)
            .Include(r => r.Appointments)
                .ThenInclude(a => a.History)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ServiceRequest>> GetMatchingForProviderAsync(double lat, double lng, double radiusKm, List<ServiceCategory> categories, string? searchTerm = null)
    {
        // Simple bounding box calculation to filter in DB
        // 1 degree ~ 111km
        double latDegreeDelta = radiusKm / 111.0;
        double lngDegreeDelta = radiusKm / (111.0 * Math.Cos(lat * Math.PI / 180.0));

        double minLat = lat - latDegreeDelta;
        double maxLat = lat + latDegreeDelta;
        double minLng = lng - lngDegreeDelta;
        double maxLng = lng + lngDegreeDelta;

        var query = _context.ServiceRequests
            .Include(r => r.Client)
            .Include(r => r.CategoryDefinition)
            .Where(r => r.Status == ServiceRequestStatus.Created)
            .Where(r => categories.Contains(r.Category))
            .Where(r => r.Latitude >= minLat && r.Latitude <= maxLat)
            .Where(r => r.Longitude >= minLng && r.Longitude <= maxLng);

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(r => r.Description.Contains(searchTerm));
        }

        return await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
    }

    public async Task<IEnumerable<ServiceRequest>> GetOpenWithinRadiusAsync(double lat, double lng, double radiusKm)
    {
        double latDegreeDelta = radiusKm / 111.0;
        double lngDegreeDelta = radiusKm / (111.0 * Math.Cos(lat * Math.PI / 180.0));

        double minLat = lat - latDegreeDelta;
        double maxLat = lat + latDegreeDelta;
        double minLng = lng - lngDegreeDelta;
        double maxLng = lng + lngDegreeDelta;

        return await _context.ServiceRequests
            .Include(r => r.CategoryDefinition)
            .Where(r => r.Status == ServiceRequestStatus.Created || r.Status == ServiceRequestStatus.Matching)
            .Where(r => r.Latitude >= minLat && r.Latitude <= maxLat)
            .Where(r => r.Longitude >= minLng && r.Longitude <= maxLng)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<ServiceRequest?> GetByIdAsync(Guid id)
    {
        return await _context.ServiceRequests
            .Include(r => r.Client)
            .Include(r => r.Proposals)
            .Include(r => r.PaymentTransactions)
            .Include(r => r.Appointments)
                .ThenInclude(a => a.Provider)
            .Include(r => r.Appointments)
                .ThenInclude(a => a.History)
                    .ThenInclude(h => h.ActorUser)
            .Include(r => r.CategoryDefinition)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task UpdateAsync(ServiceRequest request)
    {
        _context.ServiceRequests.Update(request);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<ServiceRequest>> GetScheduledByProviderAsync(Guid providerId)
    {
        return await _context.ServiceRequests
            .Include(r => r.Client)
            .Include(r => r.Proposals)
            .Include(r => r.CategoryDefinition)
            .Where(r => r.Proposals.Any(p => p.ProviderId == providerId && p.Accepted))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ServiceRequest>> GetHistoryByProviderAsync(Guid providerId)
    {
        return await _context.ServiceRequests
            .Include(r => r.Client)
            .Include(r => r.Proposals)
            .Include(r => r.Review)
            .Include(r => r.CategoryDefinition)
            .Where(r => r.Proposals.Any(p => p.ProviderId == providerId && p.Accepted) && r.Status == ServiceRequestStatus.Completed)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }
}
