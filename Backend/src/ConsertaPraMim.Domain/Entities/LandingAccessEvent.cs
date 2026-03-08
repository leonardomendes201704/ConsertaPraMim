using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public sealed class LandingAccessEvent : BaseEntity
{
    public string VisitorId { get; set; } = string.Empty;
    public string? CurrentUrl { get; set; }
    public string? Path { get; set; }
    public string? Host { get; set; }
    public string? Scheme { get; set; }
    public LandingLeadOrigin? InitialLeadOrigin { get; set; }
    public string? IpAddress { get; set; }
    public string? ForwardedFor { get; set; }
    public string? UserAgent { get; set; }
    public string? AcceptLanguage { get; set; }
    public string? RefererUrl { get; set; }
    public string? MetadataJson { get; set; }
}
