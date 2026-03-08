using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;

namespace ConsertaPraMim.Application.Services;

public sealed class LandingTelemetryEventService : ILandingTelemetryEventService
{
    private const int MaxVisitorIdLength = 80;
    private const int MaxSessionIdLength = 80;
    private const int MaxUrlLength = 500;
    private const int MaxPathLength = 260;
    private const int MaxHostLength = 200;
    private const int MaxSchemeLength = 10;
    private const int MaxElementKeyLength = 160;
    private const int MaxElementLabelLength = 240;
    private const int MaxLanguageLength = 128;
    private const int MaxIpLength = 80;
    private const int MaxForwardedForLength = 300;
    private const int MaxUserAgentLength = 512;
    private const int MaxMetadataJsonLength = 4000;
    private const int MaxBatchSize = 80;

    private readonly ILandingTelemetryEventRepository _landingTelemetryEventRepository;
    private readonly ILandingAnalyticsRuntimeSettings _runtimeSettings;

    public LandingTelemetryEventService(
        ILandingTelemetryEventRepository landingTelemetryEventRepository,
        ILandingAnalyticsRuntimeSettings runtimeSettings)
    {
        _landingTelemetryEventRepository = landingTelemetryEventRepository;
        _runtimeSettings = runtimeSettings;
    }

    public async Task<RecordLandingTelemetryBatchResponseDto> RecordBatchAsync(
        RecordLandingTelemetryBatchRequestDto request,
        LandingLeadCaptureContextDto context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var config = await _runtimeSettings.GetConfigAsync(cancellationToken);
        var recordedAtUtc = DateTime.UtcNow;
        if (!config.ClientTelemetryEnabled || request.Events == null || request.Events.Count == 0)
        {
            return new RecordLandingTelemetryBatchResponseDto(0, recordedAtUtc);
        }

        var visitorId = NormalizeRequiredIdentifier(request.VisitorId, MaxVisitorIdLength);
        var sessionId = NormalizeRequiredIdentifier(request.SessionId, MaxSessionIdLength);
        var initialLeadOrigin = NormalizeOrigin(request.InitialLeadOrigin);
        var currentUrl = NormalizeOptional(request.CurrentUrl, MaxUrlLength);
        var path = NormalizeOptional(request.Path, MaxPathLength);
        var host = NormalizeOptional(request.Host, MaxHostLength);
        var scheme = NormalizeOptional(request.Scheme, MaxSchemeLength);
        var viewportWidth = NormalizePositiveInteger(request.ViewportWidth);
        var viewportHeight = NormalizePositiveInteger(request.ViewportHeight);
        var browserLanguage = NormalizeOptional(request.BrowserLanguage, MaxLanguageLength);
        var ipAddress = NormalizeOptional(context.IpAddress, MaxIpLength);
        var forwardedFor = NormalizeOptional(context.ForwardedFor, MaxForwardedForLength);
        var userAgent = NormalizeOptional(context.UserAgent, MaxUserAgentLength);
        var acceptLanguage = NormalizeOptional(context.AcceptLanguage, MaxLanguageLength);

        var entities = new List<LandingTelemetryEvent>(Math.Min(request.Events.Count, MaxBatchSize));
        foreach (var item in request.Events.Take(MaxBatchSize))
        {
            if (item == null || !TryParseType(item.Type, out var eventType))
            {
                continue;
            }

            var occurredAtUtc = NormalizeOccurredAtUtc(item.OccurredAtUtc, recordedAtUtc);
            var clickXPercent = NormalizePercent(item.ClickXPercent);
            var clickYPercent = NormalizePercent(item.ClickYPercent);
            var heatmapRow = NormalizeHeatmapIndex(item.HeatmapRow, config.Clicks.HeatmapGridRows, clickYPercent);
            var heatmapColumn = NormalizeHeatmapIndex(item.HeatmapColumn, config.Clicks.HeatmapGridColumns, clickXPercent);
            var metadataJson = BuildMetadataJson(item, viewportWidth, viewportHeight);

            entities.Add(new LandingTelemetryEvent
            {
                VisitorId = visitorId,
                SessionId = sessionId,
                CurrentUrl = currentUrl,
                Path = path,
                Host = host,
                Scheme = scheme,
                InitialLeadOrigin = initialLeadOrigin,
                EventType = eventType,
                OccurredAtUtc = occurredAtUtc,
                ActiveSeconds = NormalizeRange(item.ActiveSeconds, 0, 300),
                ScrollDepthPercent = NormalizeRange(item.ScrollDepthPercent, 0, 100),
                ClickXPercent = clickXPercent,
                ClickYPercent = clickYPercent,
                HeatmapRow = heatmapRow,
                HeatmapColumn = heatmapColumn,
                ElementKey = NormalizeOptional(item.ElementKey, MaxElementKeyLength),
                ElementLabel = NormalizeOptional(item.ElementLabel, MaxElementLabelLength),
                ElementHref = NormalizeOptional(item.ElementHref, MaxUrlLength),
                BrowserLanguage = browserLanguage,
                ViewportWidth = viewportWidth,
                ViewportHeight = viewportHeight,
                IpAddress = ipAddress,
                ForwardedFor = forwardedFor,
                UserAgent = userAgent,
                AcceptLanguage = acceptLanguage,
                MetadataJson = metadataJson
            });
        }

        if (entities.Count > 0)
        {
            await _landingTelemetryEventRepository.AddRangeAsync(entities, cancellationToken);
        }

        return new RecordLandingTelemetryBatchResponseDto(entities.Count, recordedAtUtc);
    }

    private static string BuildMetadataJson(
        RecordLandingTelemetryEventItemDto item,
        int? viewportWidth,
        int? viewportHeight)
    {
        var metadata = new
        {
            item.Type,
            item.OccurredAtUtc,
            ViewportWidth = viewportWidth,
            ViewportHeight = viewportHeight
        };

        var serialized = JsonSerializer.Serialize(metadata);
        return serialized.Length <= MaxMetadataJsonLength
            ? serialized
            : serialized[..MaxMetadataJsonLength];
    }

    private static DateTime NormalizeOccurredAtUtc(DateTime? rawValue, DateTime fallbackUtc)
    {
        if (!rawValue.HasValue)
        {
            return fallbackUtc;
        }

        var utcValue = rawValue.Value.Kind == DateTimeKind.Utc
            ? rawValue.Value
            : rawValue.Value.ToUniversalTime();

        var minUtc = fallbackUtc.AddHours(-12);
        var maxUtc = fallbackUtc.AddMinutes(5);
        if (utcValue < minUtc || utcValue > maxUtc)
        {
            return fallbackUtc;
        }

        return utcValue;
    }

    private static LandingLeadOrigin? NormalizeOrigin(string? rawOrigin)
    {
        if (string.IsNullOrWhiteSpace(rawOrigin))
        {
            return null;
        }

        return rawOrigin.Trim().ToLowerInvariant() switch
        {
            "client" => LandingLeadOrigin.Client,
            "provider" => LandingLeadOrigin.Provider,
            _ => null
        };
    }

    private static bool TryParseType(string? rawType, out LandingTelemetryEventType eventType)
    {
        eventType = default;
        if (string.IsNullOrWhiteSpace(rawType))
        {
            return false;
        }

        switch (rawType.Trim().ToLowerInvariant())
        {
            case "heartbeat":
                eventType = LandingTelemetryEventType.Heartbeat;
                return true;
            case "scroll_milestone":
                eventType = LandingTelemetryEventType.ScrollMilestone;
                return true;
            case "click":
                eventType = LandingTelemetryEventType.Click;
                return true;
            case "lead_modal_open":
                eventType = LandingTelemetryEventType.LeadModalOpen;
                return true;
            case "lead_submit_success":
                eventType = LandingTelemetryEventType.LeadSubmitSuccess;
                return true;
            default:
                return false;
        }
    }

    private static int? NormalizePositiveInteger(int? rawValue)
    {
        if (!rawValue.HasValue || rawValue.Value <= 0)
        {
            return null;
        }

        return Math.Clamp(rawValue.Value, 1, 20_000);
    }

    private static int? NormalizeRange(int? rawValue, int minValue, int maxValue)
    {
        if (!rawValue.HasValue)
        {
            return null;
        }

        return Math.Clamp(rawValue.Value, minValue, maxValue);
    }

    private static double? NormalizePercent(double? rawValue)
    {
        if (!rawValue.HasValue || double.IsNaN(rawValue.Value) || double.IsInfinity(rawValue.Value))
        {
            return null;
        }

        return Math.Clamp(rawValue.Value, 0d, 100d);
    }

    private static int? NormalizeHeatmapIndex(int? rawValue, int gridDimension, double? percent)
    {
        if (rawValue.HasValue)
        {
            return Math.Clamp(rawValue.Value, 0, Math.Max(gridDimension - 1, 0));
        }

        if (!percent.HasValue || gridDimension <= 0)
        {
            return null;
        }

        var safePercent = Math.Clamp(percent.Value, 0d, 99.999d);
        var bucketSize = 100d / gridDimension;
        return Math.Clamp((int)Math.Floor(safePercent / bucketSize), 0, gridDimension - 1);
    }

    private static string NormalizeRequiredIdentifier(string? rawValue, int maxLength)
    {
        var normalized = NormalizeOptional(rawValue, maxLength);
        return string.IsNullOrWhiteSpace(normalized)
            ? Guid.NewGuid().ToString("N")
            : normalized;
    }

    private static string? NormalizeOptional(string? rawValue, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var trimmed = rawValue.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
