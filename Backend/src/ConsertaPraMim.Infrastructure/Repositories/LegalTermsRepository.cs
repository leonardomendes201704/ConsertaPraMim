using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Infrastructure.Repositories;

public class LegalTermsRepository : ILegalTermsRepository
{
    private readonly ConsertaPraMimDbContext _context;

    public LegalTermsRepository(ConsertaPraMimDbContext context)
    {
        _context = context;
    }

    public async Task<LegalTermsDocument?> GetActiveByAudienceAsync(
        LegalTermsAudience audience,
        CancellationToken cancellationToken = default)
    {
        return await _context.LegalTermsDocuments
            .AsNoTracking()
            .Where(x => x.Audience == audience && x.IsPublished)
            .OrderByDescending(x => x.PublishedAtUtc)
            .ThenByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LegalTermsDocument>> ListByAudienceAsync(
        LegalTermsAudience audience,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default)
    {
        var query = _context.LegalTermsDocuments
            .Where(x => x.Audience == audience);

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query
            .OrderByDescending(x => x.Version)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        LegalTermsDocument document,
        CancellationToken cancellationToken = default)
    {
        await _context.LegalTermsDocuments.AddAsync(document, cancellationToken);
    }

    public async Task AddAcceptanceAsync(
        UserLegalTermsAcceptance acceptance,
        CancellationToken cancellationToken = default)
    {
        await _context.UserLegalTermsAcceptances.AddAsync(acceptance, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
