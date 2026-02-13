using ConsertaPraMim.Domain.Common;

namespace ConsertaPraMim.Domain.Entities;

public class ChatAttachment : BaseEntity
{
    public Guid ChatMessageId { get; set; }
    public ChatMessage ChatMessage { get; set; } = null!;

    public string FileUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string MediaKind { get; set; } = string.Empty;
}
