using System.Net;
using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace ConsertaPraMim.Application.Services;

public sealed class LandingLeadService : ILandingLeadService
{
    private const int MaxFullNameLength = 160;
    private const int MaxVisitorIdLength = 80;
    private const int MaxSessionIdLength = 80;
    private const int MaxPhoneLength = 40;
    private const int MaxEmailLength = 200;
    private const int MaxCityLength = 120;
    private const int MaxStateLength = 2;
    private const int MaxNeighborhoodLength = 120;
    private const int MaxServiceCategoryLength = 120;
    private const int MaxRequestedServiceLength = 220;
    private const int MaxCompanyNameLength = 180;
    private const int MaxCompanyDocumentLength = 32;
    private const int MaxMessageLength = 1600;
    private const int MaxUrlLength = 500;
    private const int MaxHostLength = 200;
    private const int MaxSchemeLength = 10;
    private const int MaxPathLength = 260;
    private const int MaxQueryLength = 2000;
    private const int MaxUtmLength = 180;
    private const int MaxIpLength = 80;
    private const int MaxForwardedForLength = 300;
    private const int MaxUserAgentLength = 512;
    private const int MaxLanguageLength = 128;
    private const int MaxScreenResolutionLength = 32;
    private const int MaxDevicePlatformLength = 80;
    private const int MaxTimeZoneLength = 128;
    private const int MaxMetadataJsonLength = 4000;

    private readonly ILandingLeadRepository _landingLeadRepository;
    private readonly ILandingAdminNotificationService _landingAdminNotificationService;
    private readonly IServiceJourneyAutomationGateway? _serviceJourneyAutomationGateway;
    private readonly ILogger<LandingLeadService>? _logger;

    public LandingLeadService(
        ILandingLeadRepository landingLeadRepository,
        ILandingAdminNotificationService landingAdminNotificationService,
        IServiceJourneyAutomationGateway? serviceJourneyAutomationGateway = null,
        ILogger<LandingLeadService>? logger = null)
    {
        _landingLeadRepository = landingLeadRepository;
        _landingAdminNotificationService = landingAdminNotificationService;
        _serviceJourneyAutomationGateway = serviceJourneyAutomationGateway;
        _logger = logger;
    }

    public async Task<CaptureLandingLeadResponseDto> CaptureAsync(
        CaptureLandingLeadRequestDto request,
        LandingLeadCaptureContextDto context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var pageUri = TryCreateAbsoluteUri(request.CurrentPageUrl);
        var mergedQueryString = FirstNonEmpty(request.QueryString, pageUri?.Query);
        var queryParameters = ParseQueryString(mergedQueryString);

        var lead = new LandingLead
        {
            Origin = request.Origin,
            VisitorId = NormalizeOptional(request.VisitorId, MaxVisitorIdLength),
            SessionId = NormalizeOptional(request.SessionId, MaxSessionIdLength),
            FullName = NormalizeRequired(request.FullName, MaxFullNameLength),
            Phone = NormalizeRequired(request.Phone, MaxPhoneLength),
            Email = NormalizeRequired(request.Email, MaxEmailLength),
            City = NormalizeRequired(request.City, MaxCityLength),
            State = NormalizeState(request.State),
            Neighborhood = NormalizeRequired(request.Neighborhood, MaxNeighborhoodLength),
            ServiceCategory = NormalizeOptional(request.ServiceCategory, MaxServiceCategoryLength),
            RequestedService = NormalizeOptional(request.RequestedService, MaxRequestedServiceLength),
            CompanyName = NormalizeOptional(request.CompanyName, MaxCompanyNameLength),
            CompanyDocument = NormalizeOptional(OnlyDigits(request.CompanyDocument), MaxCompanyDocumentLength),
            YearsOfExperience = NormalizeYearsOfExperience(request.YearsOfExperience),
            Message = NormalizeOptional(request.Message, MaxMessageLength),
            CurrentPageUrl = NormalizeOptional(FirstNonEmpty(request.CurrentPageUrl, pageUri?.ToString()), MaxUrlLength),
            ReferrerUrl = NormalizeOptional(FirstNonEmpty(request.ReferrerUrl, context.RefererHeader), MaxUrlLength),
            Host = NormalizeOptional(context.Host, MaxHostLength),
            Scheme = NormalizeOptional(context.Scheme, MaxSchemeLength),
            Path = NormalizeOptional(FirstNonEmpty(context.Path, pageUri?.AbsolutePath), MaxPathLength),
            QueryString = NormalizeOptional(mergedQueryString, MaxQueryLength),
            UtmSource = ResolveCampaignValue(request.UtmSource, queryParameters, "utm_source"),
            UtmMedium = ResolveCampaignValue(request.UtmMedium, queryParameters, "utm_medium"),
            UtmCampaign = ResolveCampaignValue(request.UtmCampaign, queryParameters, "utm_campaign"),
            UtmTerm = ResolveCampaignValue(request.UtmTerm, queryParameters, "utm_term"),
            UtmContent = ResolveCampaignValue(request.UtmContent, queryParameters, "utm_content"),
            IpAddress = NormalizeOptional(context.IpAddress, MaxIpLength),
            ForwardedFor = NormalizeOptional(context.ForwardedFor, MaxForwardedForLength),
            UserAgent = NormalizeOptional(context.UserAgent, MaxUserAgentLength),
            AcceptLanguage = NormalizeOptional(context.AcceptLanguage, MaxLanguageLength),
            BrowserLanguage = NormalizeOptional(request.BrowserLanguage, MaxLanguageLength),
            ScreenResolution = NormalizeOptional(request.ScreenResolution, MaxScreenResolutionLength),
            DevicePlatform = NormalizeOptional(request.DevicePlatform, MaxDevicePlatformLength),
            TimeZone = NormalizeOptional(request.TimeZone, MaxTimeZoneLength),
            MetadataJson = BuildMetadataJson(request, context, queryParameters)
        };

        await _landingLeadRepository.AddAsync(lead, cancellationToken);
        await _landingAdminNotificationService.NotifyLandingLeadCapturedAsync(lead, cancellationToken);
        await TrySyncJourneyAsync(lead, cancellationToken);

        return new CaptureLandingLeadResponseDto(
            lead.Id,
            lead.Origin,
            lead.Origin == LandingLeadOrigin.Client
                ? "Recebemos seu interesse. Nosso time pode entrar em contato para conectar voce a um profissional da sua regiao."
                : "Recebemos seu cadastro de interesse. Nosso time pode entrar em contato para avaliar a parceria.",
            lead.CreatedAt);
    }

    private static string BuildMetadataJson(
        CaptureLandingLeadRequestDto request,
        LandingLeadCaptureContextDto context,
        IReadOnlyDictionary<string, string> queryParameters)
    {
        var metadata = new
        {
            request.Origin,
            request.VisitorId,
            request.SessionId,
            request.CurrentPageUrl,
            request.ReferrerUrl,
            request.QueryString,
            request.UtmSource,
            request.UtmMedium,
            request.UtmCampaign,
            request.UtmTerm,
            request.UtmContent,
            request.BrowserLanguage,
            request.ScreenResolution,
            request.DevicePlatform,
            request.TimeZone,
            context.IpAddress,
            context.ForwardedFor,
            context.UserAgent,
            context.AcceptLanguage,
            context.Host,
            context.Scheme,
            context.Path,
            context.RefererHeader,
            QueryParameters = queryParameters
        };

        var serialized = JsonSerializer.Serialize(metadata);
        return serialized.Length <= MaxMetadataJsonLength
            ? serialized
            : serialized[..MaxMetadataJsonLength];
    }

    private static string? ResolveCampaignValue(string? directValue, IReadOnlyDictionary<string, string> queryParameters, string queryKey)
    {
        return NormalizeOptional(
            FirstNonEmpty(
                directValue,
                queryParameters.TryGetValue(queryKey, out var fromQuery) ? fromQuery : null),
            MaxUtmLength);
    }

    private static IReadOnlyDictionary<string, string> ParseQueryString(string? rawQuery)
    {
        var output = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(rawQuery))
        {
            return output;
        }

        var normalized = rawQuery.Trim();
        if (normalized.StartsWith("?", StringComparison.Ordinal))
        {
            normalized = normalized[1..];
        }

        foreach (var pair in normalized.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split('=', 2);
            var key = WebUtility.UrlDecode(parts[0]);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var value = parts.Length > 1 ? WebUtility.UrlDecode(parts[1]) : string.Empty;
            if (!output.ContainsKey(key))
            {
                output[key] = value ?? string.Empty;
            }
        }

        return output;
    }

    private static Uri? TryCreateAbsoluteUri(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return null;
        }

        return Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out var uri) ? uri : null;
    }

    private static string NormalizeRequired(string? value, int maxLength)
    {
        var normalized = NormalizeOptional(value, maxLength);
        return string.IsNullOrWhiteSpace(normalized) ? string.Empty : normalized;
    }

    private static string NormalizeState(string? value)
    {
        var normalized = NormalizeOptional(value, MaxStateLength);
        return string.IsNullOrWhiteSpace(normalized)
            ? string.Empty
            : normalized.ToUpperInvariant();
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

    private static int? NormalizeYearsOfExperience(int? years)
    {
        if (!years.HasValue)
        {
            return null;
        }

        return Math.Clamp(years.Value, 0, 60);
    }

    private static string? OnlyDigits(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var digits = new string(raw.Where(char.IsDigit).ToArray());
        return string.IsNullOrWhiteSpace(digits) ? null : digits;
    }


    private async Task TrySyncJourneyAsync(LandingLead lead, CancellationToken cancellationToken)
    {
        if (_serviceJourneyAutomationGateway is null)
        {
            return;
        }

        var boardType = lead.Origin == LandingLeadOrigin.Provider ? "prestadores" : "clientes";
        var result = await _serviceJourneyAutomationGateway.UpsertJourneyAsync(
            new ServiceJourneyAutomationRequestDto
            {
                BoardType = boardType,
                SourceChannel = "landing",
                SourceOrigin = FirstNonEmpty(lead.CurrentPageUrl, lead.ReferrerUrl, "landing-public") ?? "landing-public",
                Name = lead.FullName,
                Phone = lead.Phone,
                Email = lead.Email,
                ServiceCategory = lead.ServiceCategory ?? lead.RequestedService ?? string.Empty,
                ProblemDescription = FirstNonEmpty(lead.Message, lead.RequestedService) ?? string.Empty,
                Neighborhood = lead.Neighborhood ?? string.Empty,
                State = lead.State ?? string.Empty,
                PostalCode = string.Empty,
                City = lead.City,
                StatusNote = lead.Origin == LandingLeadOrigin.Provider
                    ? "Prestador capturado pela landing publica para onboarding automatizado."
                    : "Lead capturado pela landing publica para jornada automatizada.",
                InternalNotes = BuildJourneyInternalNotes(lead),
                LandingLeadId = lead.Id,
                VisitorId = lead.VisitorId ?? string.Empty,
                SessionId = lead.SessionId ?? string.Empty,
                RequestedAtUtc = lead.CreatedAt,
                LastContactAtUtc = lead.CreatedAt
            },
            cancellationToken);

        if (!result.Success)
        {
            _logger?.LogWarning(
                "Falha ao sincronizar landing lead {LandingLeadId} com a jornada automatizada. Status={StatusCode}. Message={Message}",
                lead.Id,
                result.HttpStatusCode,
                result.Message);
        }
    }

    private static string BuildJourneyInternalNotes(LandingLead lead)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(lead.RequestedService))
        {
            parts.Add($"Servico solicitado: {lead.RequestedService}");
        }

        if (!string.IsNullOrWhiteSpace(lead.CompanyName))
        {
            parts.Add($"Empresa informada: {lead.CompanyName}");
        }

        if (!string.IsNullOrWhiteSpace(lead.Message))
        {
            parts.Add($"Mensagem do lead: {lead.Message}");
        }

        return string.Join(Environment.NewLine, parts);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}
