using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class LegalTermsDocument : BaseEntity
{
    public LegalTermsAudience Audience { get; set; }
    public int Version { get; set; }
    public string Title { get; set; } = string.Empty;
    public string HtmlContent { get; set; } = string.Empty;
    public string? ChangeSummary { get; set; }
    public bool IsPublished { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public Guid? PublishedByUserId { get; set; }

    public ICollection<UserLegalTermsAcceptance> Acceptances { get; set; } = new List<UserLegalTermsAcceptance>();
}
