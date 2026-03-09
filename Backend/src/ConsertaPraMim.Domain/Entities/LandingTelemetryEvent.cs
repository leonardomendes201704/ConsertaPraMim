using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public sealed class LandingTelemetryEvent : BaseEntity
{
    public string VisitorId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string? CurrentUrl { get; set; }
    public string? Path { get; set; }
    public string? Host { get; set; }
    public string? Scheme { get; set; }
    public LandingLeadOrigin? InitialLeadOrigin { get; set; }
    public LandingTelemetryEventType EventType { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public int? ActiveSeconds { get; set; }
    public int? ScrollDepthPercent { get; set; }
    public double? ClickXPercent { get; set; }
    public double? ClickYPercent { get; set; }
    public int? HeatmapRow { get; set; }
    public int? HeatmapColumn { get; set; }
    public string? ElementKey { get; set; }
    public string? ElementLabel { get; set; }
    public string? ElementHref { get; set; }
    public string? BrowserLanguage { get; set; }
    public int? ViewportWidth { get; set; }
    public int? ViewportHeight { get; set; }
    public string? IpAddress { get; set; }
    public string? ForwardedFor { get; set; }
    public string? UserAgent { get; set; }
    public string? AcceptLanguage { get; set; }
    public string? MetadataJson { get; set; }
}
