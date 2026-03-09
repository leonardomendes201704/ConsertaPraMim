namespace ConsertaPraMim.Web.Landing.Models;

public sealed class LandingPageViewModel
{
    public required string CanonicalUrl { get; init; }
    public required string ClientPortalUrl { get; init; }
    public required string ProviderPortalUrl { get; init; }
    public required string AdminPortalUrl { get; init; }
    public required string ApiBaseUrl { get; init; }
    public required string ApiSwaggerUrl { get; init; }
    public required string LeadCaptureUrl { get; init; }
    public required string AnalyticsConfigUrl { get; init; }
    public required string TelemetryUrl { get; init; }
    public required string VisitorId { get; init; }
    public required string SessionId { get; init; }
    public string? InitialLeadOrigin { get; init; }
}
