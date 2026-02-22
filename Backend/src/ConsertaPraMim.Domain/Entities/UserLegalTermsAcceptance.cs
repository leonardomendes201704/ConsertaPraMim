using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class UserLegalTermsAcceptance : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid LegalTermsDocumentId { get; set; }
    public LegalTermsAudience Audience { get; set; }
    public int TermsVersion { get; set; }
    public DateTime AcceptedAtUtc { get; set; } = DateTime.UtcNow;
    public string Source { get; set; } = "unknown";
    public string? MetadataJson { get; set; }

    public User User { get; set; } = null!;
    public LegalTermsDocument LegalTermsDocument { get; set; } = null!;
}
