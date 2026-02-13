using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class ProviderOnboardingDocument : BaseEntity
{
    public Guid ProviderProfileId { get; set; }
    public ProviderProfile ProviderProfile { get; set; } = null!;

    public ProviderDocumentType DocumentType { get; set; }
    public ProviderDocumentStatus Status { get; set; } = ProviderDocumentStatus.Pending;

    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string? FileHashSha256 { get; set; }

    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
}
