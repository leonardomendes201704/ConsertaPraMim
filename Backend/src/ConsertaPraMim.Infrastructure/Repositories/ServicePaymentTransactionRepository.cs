using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Infrastructure.Repositories;

public class ServicePaymentTransactionRepository : IServicePaymentTransactionRepository
{
    private readonly ConsertaPraMimDbContext _context;

    public ServicePaymentTransactionRepository(ConsertaPraMimDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ServicePaymentTransaction transaction)
    {
        await _context.ServicePaymentTransactions.AddAsync(transaction);
        await _context.SaveChangesAsync();
    }

    public async Task<(ServicePaymentTransaction Transaction, bool Created)> AddOrGetByProviderTransactionIdAsync(ServicePaymentTransaction transaction)
    {
        var normalizedProviderTransactionId = transaction.ProviderTransactionId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedProviderTransactionId))
        {
            await AddAsync(transaction);
            return (transaction, true);
        }

        transaction.ProviderTransactionId = normalizedProviderTransactionId;
        var existing = await GetByProviderTransactionIdAsync(normalizedProviderTransactionId);
        if (existing != null)
        {
            return (existing, false);
        }

        try
        {
            await _context.ServicePaymentTransactions.AddAsync(transaction);
            await _context.SaveChangesAsync();
            return (transaction, true);
        }
        catch (DbUpdateException)
        {
            var concurrent = await GetByProviderTransactionIdAsync(normalizedProviderTransactionId);
            if (concurrent != null)
            {
                return (concurrent, false);
            }

            throw;
        }
    }

    public async Task<ServicePaymentTransaction?> GetByIdAsync(Guid id)
    {
        return await _context.ServicePaymentTransactions
            .Include(t => t.ServiceRequest)
            .Include(t => t.Client)
            .Include(t => t.Provider)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<ServicePaymentTransaction?> GetByProviderTransactionIdAsync(string providerTransactionId)
    {
        if (string.IsNullOrWhiteSpace(providerTransactionId))
        {
            return null;
        }

        var normalized = providerTransactionId.Trim();
        return await _context.ServicePaymentTransactions
            .Include(t => t.ServiceRequest)
            .Include(t => t.Client)
            .Include(t => t.Provider)
            .FirstOrDefaultAsync(t => t.ProviderTransactionId == normalized);
    }

    public async Task<IReadOnlyList<ServicePaymentTransaction>> GetByServiceRequestIdAsync(
        Guid serviceRequestId,
        PaymentTransactionStatus? status = null)
    {
        var query = _context.ServicePaymentTransactions
            .Include(t => t.Client)
            .Include(t => t.Provider)
            .Where(t => t.ServiceRequestId == serviceRequestId)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task UpdateAsync(ServicePaymentTransaction transaction)
    {
        _context.ServicePaymentTransactions.Update(transaction);
        await _context.SaveChangesAsync();
    }
}
