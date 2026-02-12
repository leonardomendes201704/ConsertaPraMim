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
            .Where(r => r.ClientId == clientId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ServiceRequest>> GetAllAsync()
    {
        return await _context.ServiceRequests
            .Include(r => r.Client)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ServiceRequest>> GetMatchingForProviderAsync(double lat, double lng, double radiusKm, List<ServiceCategory> categories)
    {
        // Simple bounding box calculation to filter in DB
        // 1 degree ~ 111km
        double latDegreeDelta = radiusKm / 111.0;
        double lngDegreeDelta = radiusKm / (111.0 * Math.Cos(lat * Math.PI / 180.0));

        double minLat = lat - latDegreeDelta;
        double maxLat = lat + latDegreeDelta;
        double minLng = lng - lngDegreeDelta;
        double maxLng = lng + lngDegreeDelta;

        return await _context.ServiceRequests
            .Include(r => r.Client)
            .Where(r => r.Status == ServiceRequestStatus.Created)
            .Where(r => categories.Contains(r.Category))
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
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task UpdateAsync(ServiceRequest request)
    {
        _context.ServiceRequests.Update(request);
        await _context.SaveChangesAsync();
    }
}
