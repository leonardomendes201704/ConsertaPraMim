using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;

namespace ConsertaPraMim.Application.Services;

public sealed class LandingAccessEventService : ILandingAccessEventService
{
    private const int MaxVisitorIdLength = 80;
    private const int MaxUrlLength = 500;
    private const int MaxPathLength = 260;
    private const int MaxHostLength = 200;
    private const int MaxSchemeLength = 10;
    private const int MaxIpLength = 80;
    private const int MaxForwardedForLength = 300;
    private const int MaxUserAgentLength = 512;
    private const int MaxAcceptLanguageLength = 128;
    private const int MaxMetadataJsonLength = 4000;

    private readonly ILandingAccessEventRepository _landingAccessEventRepository;
    private readonly ILandingAdminNotificationService _landingAdminNotificationService;

    public LandingAccessEventService(
        ILandingAccessEventRepository landingAccessEventRepository,
        ILandingAdminNotificationService landingAdminNotificationService)
    {
        _landingAccessEventRepository = landingAccessEventRepository;
        _landingAdminNotificationService = landingAdminNotificationService;
    }

    public async Task RecordAccessAsync(NotifyLandingAccessRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var accessEvent = new LandingAccessEvent
        {
            VisitorId = NormalizeVisitorId(request.VisitorId),
            CurrentUrl = NormalizeOptional(request.CurrentUrl, MaxUrlLength),
            Path = NormalizeOptional(request.Path, MaxPathLength),
            Host = NormalizeOptional(request.Host, MaxHostLength),
            Scheme = NormalizeOptional(request.Scheme, MaxSchemeLength),
            InitialLeadOrigin = NormalizeOrigin(request.InitialLeadOrigin),
            IpAddress = NormalizeOptional(request.IpAddress, MaxIpLength),
            ForwardedFor = NormalizeOptional(request.ForwardedFor, MaxForwardedForLength),
            UserAgent = NormalizeOptional(request.UserAgent, MaxUserAgentLength),
            AcceptLanguage = NormalizeOptional(request.AcceptLanguage, MaxAcceptLanguageLength),
            RefererUrl = NormalizeOptional(request.RefererUrl, MaxUrlLength),
            MetadataJson = BuildMetadataJson(request)
        };

        await _landingAccessEventRepository.AddAsync(accessEvent, cancellationToken);
        await _landingAdminNotificationService.NotifyLandingAccessAsync(request, cancellationToken);
    }

    private static string BuildMetadataJson(NotifyLandingAccessRequestDto request)
    {
        var metadata = new
        {
            request.VisitorId,
            request.CurrentUrl,
            request.Path,
            request.Host,
            request.Scheme,
            request.InitialLeadOrigin,
            request.IpAddress,
            request.ForwardedFor,
            request.UserAgent,
            request.AcceptLanguage,
            request.RefererUrl
        };

        var serialized = JsonSerializer.Serialize(metadata);
        return serialized.Length <= MaxMetadataJsonLength
            ? serialized
            : serialized[..MaxMetadataJsonLength];
    }

    private static LandingLeadOrigin? NormalizeOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return null;
        }

        return origin.Trim().ToLowerInvariant() switch
        {
            "client" => LandingLeadOrigin.Client,
            "provider" => LandingLeadOrigin.Provider,
            _ => null
        };
    }

    private static string NormalizeVisitorId(string? value)
    {
        var normalized = NormalizeOptional(value, MaxVisitorIdLength);
        return string.IsNullOrWhiteSpace(normalized)
            ? Guid.NewGuid().ToString("N")
            : normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}
