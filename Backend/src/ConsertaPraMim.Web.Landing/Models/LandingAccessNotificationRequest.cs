namespace ConsertaPraMim.Web.Landing.Models;

public sealed record LandingAccessNotificationRequest(
    string VisitorId,
    string? CurrentUrl,
    string? Path,
    string? Host,
    string? Scheme,
    string? InitialLeadOrigin,
    string? IpAddress,
    string? ForwardedFor,
    string? UserAgent,
    string? AcceptLanguage,
    string? RefererUrl);
