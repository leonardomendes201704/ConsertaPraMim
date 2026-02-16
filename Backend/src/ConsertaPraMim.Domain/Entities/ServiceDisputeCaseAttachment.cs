using ConsertaPraMim.Domain.Common;

namespace ConsertaPraMim.Domain.Entities;

public class ServiceDisputeCaseAttachment : BaseEntity
{
    public Guid ServiceDisputeCaseId { get; set; }
    public ServiceDisputeCase ServiceDisputeCase { get; set; } = null!;

    public Guid? ServiceDisputeCaseMessageId { get; set; }
    public ServiceDisputeCaseMessage? ServiceDisputeCaseMessage { get; set; }

    public Guid UploadedByUserId { get; set; }
    public User UploadedByUser { get; set; } = null!;

    public string FileUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string MediaKind { get; set; } = "file";
    public long SizeBytes { get; set; }
}
