using ConsertaPraMim.Domain.Common;

namespace ConsertaPraMim.Domain.Entities;

public class ServiceScopeChangeRequestAttachment : BaseEntity
{
    public Guid ServiceScopeChangeRequestId { get; set; }
    public ServiceScopeChangeRequest ServiceScopeChangeRequest { get; set; } = null!;

    public Guid UploadedByUserId { get; set; }
    public User UploadedByUser { get; set; } = null!;

    public string FileUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string MediaKind { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}
