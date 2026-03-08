namespace ConsertaPraMim.Application.DTOs;

public sealed record NotifyLandingAccessRequestDto(
    string? VisitorId,
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
