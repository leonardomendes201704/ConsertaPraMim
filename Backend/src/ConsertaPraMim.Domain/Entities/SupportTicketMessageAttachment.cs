using ConsertaPraMim.Domain.Common;

namespace ConsertaPraMim.Domain.Entities;

public class SupportTicketMessageAttachment : BaseEntity
{
    public Guid SupportTicketMessageId { get; set; }
    public SupportTicketMessage SupportTicketMessage { get; set; } = null!;

    public string FileUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string MediaKind { get; set; } = string.Empty;
}
