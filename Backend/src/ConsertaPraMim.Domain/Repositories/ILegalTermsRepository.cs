using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Repositories;

public interface ILegalTermsRepository
{
    Task<LegalTermsDocument?> GetActiveByAudienceAsync(
        LegalTermsAudience audience,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LegalTermsDocument>> ListByAudienceAsync(
        LegalTermsAudience audience,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        LegalTermsDocument document,
        CancellationToken cancellationToken = default);

    Task AddAcceptanceAsync(
        UserLegalTermsAcceptance acceptance,
        CancellationToken cancellationToken = default);

    Task<UserLegalTermsAcceptance?> GetLatestAcceptanceByUserAsync(
        Guid userId,
        LegalTermsAudience audience,
        CancellationToken cancellationToken = default);

    Task<UserLegalTermsAcceptance?> GetAcceptanceByUserAndDocumentAsync(
        Guid userId,
        Guid legalTermsDocumentId,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
