using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.Interfaces;

public interface ILegalTermsService
{
    Task<LegalTermsDocumentDto?> GetActiveAsync(
        LegalTermsAudience audience,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LegalTermsDocumentDto>> GetVersionsAsync(
        LegalTermsAudience audience,
        CancellationToken cancellationToken = default);

    Task<LegalTermsPublishResultDto> PublishAsync(
        LegalTermsAudience audience,
        LegalTermsPublishPayloadDto payload,
        Guid actorUserId,
        string? actorEmail,
        CancellationToken cancellationToken = default);
}
