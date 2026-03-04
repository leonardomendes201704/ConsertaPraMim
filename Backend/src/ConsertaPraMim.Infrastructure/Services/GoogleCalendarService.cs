using System.Net;
using System.Text;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Infrastructure.Configuration;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Infrastructure.Services;

public sealed class GoogleCalendarService : IGoogleCalendarService
{
    private static readonly string CalendarScope = CalendarService.Scope.Calendar;
    private const string CalendarAppName = "ConsertaPraMim.GoogleCalendarSync";
    private readonly GoogleCalendarSyncOptions _options;
    private readonly ILogger<GoogleCalendarService> _logger;
    private readonly Lazy<CalendarService>? _calendarClient;
    private readonly TimeZoneInfo _businessTimeZone;
    private readonly string _googleTimeZoneId;

    public GoogleCalendarService(
        IOptions<GoogleCalendarSyncOptions> options,
        ILogger<GoogleCalendarService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _businessTimeZone = ResolveBusinessTimeZone(_options.Timezone);
        _googleTimeZoneId = ResolveGoogleTimeZoneId(_options.Timezone);

        if (_options.Enabled)
        {
            _calendarClient = new Lazy<CalendarService>(CreateCalendarService, isThreadSafe: true);
        }
    }

    public async Task<GoogleCalendarUpsertResult> CreateEventAsync(
        GoogleCalendarUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var preValidation = ValidateRequest(request);
        if (preValidation is not null)
        {
            return preValidation;
        }

        var calendarEvent = BuildCalendarEvent(request);
        try
        {
            var insertRequest = GetCalendarService().Events.Insert(calendarEvent, _options.CalendarId.Trim());
            var created = await insertRequest.ExecuteAsync(cancellationToken);

            return new GoogleCalendarUpsertResult(
                Success: true,
                EventId: created.Id,
                HtmlLink: created.HtmlLink);
        }
        catch (GoogleApiException ex)
            when (ex.HttpStatusCode == HttpStatusCode.Conflict && !string.IsNullOrWhiteSpace(calendarEvent.Id))
        {
            _logger.LogInformation(
                ex,
                "Evento do Google Calendar ja existe para a chave idempotente. EventId={EventId}",
                calendarEvent.Id);

            return new GoogleCalendarUpsertResult(
                Success: true,
                EventId: calendarEvent.Id);
        }
        catch (GoogleApiException ex)
        {
            _logger.LogError(
                ex,
                "Falha ao criar evento no Google Calendar. Status={StatusCode}. CalendarId={CalendarId}",
                ex.HttpStatusCode,
                _options.CalendarId);

            return new GoogleCalendarUpsertResult(
                Success: false,
                ErrorCode: "google_calendar_create_failed",
                ErrorMessage: BuildGoogleApiErrorMessage(ex, "Falha ao criar evento no Google Calendar."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao criar evento no Google Calendar.");
            return new GoogleCalendarUpsertResult(
                Success: false,
                ErrorCode: "google_calendar_unexpected_error",
                ErrorMessage: "Erro inesperado ao criar evento no Google Calendar.");
        }
    }

    public async Task<GoogleCalendarUpsertResult> UpdateEventAsync(
        string eventId,
        GoogleCalendarUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return new GoogleCalendarUpsertResult(
                Success: false,
                ErrorCode: "google_calendar_invalid_event_id",
                ErrorMessage: "EventId obrigatorio.");
        }

        var preValidation = ValidateRequest(request);
        if (preValidation is not null)
        {
            return preValidation;
        }

        var calendarEvent = BuildCalendarEvent(request);
        try
        {
            var updateRequest = GetCalendarService().Events.Update(
                calendarEvent,
                _options.CalendarId.Trim(),
                eventId.Trim());
            var updated = await updateRequest.ExecuteAsync(cancellationToken);

            return new GoogleCalendarUpsertResult(
                Success: true,
                EventId: updated.Id,
                HtmlLink: updated.HtmlLink);
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            return new GoogleCalendarUpsertResult(
                Success: false,
                ErrorCode: "google_calendar_event_not_found",
                ErrorMessage: "Evento nao encontrado no Google Calendar.");
        }
        catch (GoogleApiException ex)
        {
            _logger.LogError(
                ex,
                "Falha ao atualizar evento no Google Calendar. EventId={EventId} CalendarId={CalendarId}",
                eventId,
                _options.CalendarId);

            return new GoogleCalendarUpsertResult(
                Success: false,
                ErrorCode: "google_calendar_update_failed",
                ErrorMessage: BuildGoogleApiErrorMessage(ex, "Falha ao atualizar evento no Google Calendar."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao atualizar evento no Google Calendar. EventId={EventId}", eventId);
            return new GoogleCalendarUpsertResult(
                Success: false,
                ErrorCode: "google_calendar_unexpected_error",
                ErrorMessage: "Erro inesperado ao atualizar evento no Google Calendar.");
        }
    }

    public async Task<GoogleCalendarDeleteResult> DeleteEventAsync(
        string eventId,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return new GoogleCalendarDeleteResult(
                Success: false,
                ErrorCode: "google_calendar_disabled",
                ErrorMessage: "Integracao Google Calendar desabilitada.");
        }

        if (string.IsNullOrWhiteSpace(eventId))
        {
            return new GoogleCalendarDeleteResult(
                Success: false,
                ErrorCode: "google_calendar_invalid_event_id",
                ErrorMessage: "EventId obrigatorio.");
        }

        try
        {
            var deleteRequest = GetCalendarService().Events.Delete(_options.CalendarId.Trim(), eventId.Trim());
            await deleteRequest.ExecuteAsync(cancellationToken);
            return new GoogleCalendarDeleteResult(Success: true);
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            // delete idempotente: se nao existe mais, considera sucesso.
            return new GoogleCalendarDeleteResult(Success: true);
        }
        catch (GoogleApiException ex)
        {
            _logger.LogError(
                ex,
                "Falha ao remover evento do Google Calendar. EventId={EventId} CalendarId={CalendarId}",
                eventId,
                _options.CalendarId);

            return new GoogleCalendarDeleteResult(
                Success: false,
                ErrorCode: "google_calendar_delete_failed",
                ErrorMessage: BuildGoogleApiErrorMessage(ex, "Falha ao remover evento no Google Calendar."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao remover evento no Google Calendar. EventId={EventId}", eventId);
            return new GoogleCalendarDeleteResult(
                Success: false,
                ErrorCode: "google_calendar_unexpected_error",
                ErrorMessage: "Erro inesperado ao remover evento no Google Calendar.");
        }
    }

    private GoogleCalendarUpsertResult? ValidateRequest(GoogleCalendarUpsertRequest request)
    {
        if (!_options.Enabled)
        {
            return new GoogleCalendarUpsertResult(
                Success: false,
                ErrorCode: "google_calendar_disabled",
                ErrorMessage: "Integracao Google Calendar desabilitada.");
        }

        if (request is null)
        {
            return new GoogleCalendarUpsertResult(
                Success: false,
                ErrorCode: "google_calendar_invalid_payload",
                ErrorMessage: "Payload de evento obrigatorio.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return new GoogleCalendarUpsertResult(
                Success: false,
                ErrorCode: "google_calendar_invalid_title",
                ErrorMessage: "Titulo do evento obrigatorio.");
        }

        var startsAtUtc = NormalizeToUtc(request.StartsAtUtc);
        var endsAtUtc = NormalizeToUtc(request.EndsAtUtc);
        if (endsAtUtc <= startsAtUtc)
        {
            return new GoogleCalendarUpsertResult(
                Success: false,
                ErrorCode: "google_calendar_invalid_window",
                ErrorMessage: "Intervalo de evento invalido (fim deve ser maior que inicio).");
        }

        if (!IsValidIdempotencyKey(request.IdempotencyKey))
        {
            return new GoogleCalendarUpsertResult(
                Success: false,
                ErrorCode: "google_calendar_invalid_idempotency_key",
                ErrorMessage: "IdempotencyKey invalido (use 5-128 chars com [a-z0-9_-]).");
        }

        return null;
    }

    private Event BuildCalendarEvent(GoogleCalendarUpsertRequest request)
    {
        var startsAtUtc = NormalizeToUtc(request.StartsAtUtc);
        var endsAtUtc = NormalizeToUtc(request.EndsAtUtc);
        var metadata = NormalizeMetadata(request.Metadata);
        var description = BuildDescription(request.Description, metadata);
        var location = NormalizeOptional(request.Location);

        var calendarEvent = new Event
        {
            Summary = request.Title.Trim(),
            Description = description,
            Location = location,
            Id = NormalizeIdempotencyKey(request.IdempotencyKey),
            Start = BuildEventDateTime(startsAtUtc),
            End = BuildEventDateTime(endsAtUtc)
        };

        if (metadata.Count > 0)
        {
            calendarEvent.ExtendedProperties = new Event.ExtendedPropertiesData
            {
                Private__ = metadata
            };
        }

        return calendarEvent;
    }

    private EventDateTime BuildEventDateTime(DateTime utcValue)
    {
        var localValue = TimeZoneInfo.ConvertTimeFromUtc(utcValue, _businessTimeZone);
        var offset = _businessTimeZone.GetUtcOffset(localValue);
        var localOffsetValue = new DateTimeOffset(localValue, offset);

        return new EventDateTime
        {
            DateTimeDateTimeOffset = localOffsetValue,
            TimeZone = _googleTimeZoneId
        };
    }

    private static IDictionary<string, string> NormalizeMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (metadata is null || metadata.Count == 0)
        {
            return normalized;
        }

        foreach (var pair in metadata)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            var key = pair.Key.Trim();
            if (key.Length > 128)
            {
                key = key[..128];
            }

            var value = string.IsNullOrWhiteSpace(pair.Value) ? string.Empty : pair.Value.Trim();
            if (value.Length > 1024)
            {
                value = value[..1024];
            }

            normalized[key] = value;
        }

        return normalized;
    }

    private static string? BuildDescription(string? baseDescription, IDictionary<string, string> metadata)
    {
        var hasBase = !string.IsNullOrWhiteSpace(baseDescription);
        if (!hasBase && metadata.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        if (hasBase)
        {
            builder.AppendLine(baseDescription!.Trim());
        }

        if (metadata.Count > 0)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.AppendLine("Contexto operacional:");
            foreach (var pair in metadata.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                builder.Append("- ")
                    .Append(pair.Key)
                    .Append(": ")
                    .AppendLine(pair.Value);
            }
        }

        return builder.ToString().Trim();
    }

    private static bool IsValidIdempotencyKey(string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return true;
        }

        var normalized = idempotencyKey.Trim();
        if (normalized.Length is < 5 or > 128)
        {
            return false;
        }

        foreach (var ch in normalized)
        {
            var isLowerLetter = ch is >= 'a' and <= 'z';
            var isDigit = ch is >= '0' and <= '9';
            if (!isLowerLetter && !isDigit && ch != '-' && ch != '_')
            {
                return false;
            }
        }

        return true;
    }

    private static string? NormalizeIdempotencyKey(string? idempotencyKey)
    {
        return string.IsNullOrWhiteSpace(idempotencyKey)
            ? null
            : idempotencyKey.Trim();
    }

    private CalendarService GetCalendarService()
    {
        if (_calendarClient is null)
        {
            throw new InvalidOperationException("Google Calendar integration is disabled.");
        }

        return _calendarClient.Value;
    }

    private CalendarService CreateCalendarService()
    {
        var privateKey = NormalizePrivateKey(_options.PrivateKey);
        var initializer = new ServiceAccountCredential.Initializer(_options.ServiceAccountEmail.Trim())
        {
            ProjectId = _options.ProjectId.Trim(),
            Scopes = [CalendarScope]
        };
        var credential = new ServiceAccountCredential(initializer.FromPrivateKey(privateKey));

        return new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = CalendarAppName
        });
    }

    private static DateTime NormalizeToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static string NormalizePrivateKey(string privateKey)
    {
        var normalized = privateKey.Trim();
        if (normalized.StartsWith('"') && normalized.EndsWith('"') && normalized.Length >= 2)
        {
            normalized = normalized[1..^1];
        }

        return normalized.Replace("\\n", "\n", StringComparison.Ordinal);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string BuildGoogleApiErrorMessage(GoogleApiException exception, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(exception.Error?.Message))
        {
            return exception.Error.Message.Trim();
        }

        return $"{fallback} (HTTP {(int?)exception.HttpStatusCode ?? 0}).";
    }

    private static TimeZoneInfo ResolveBusinessTimeZone(string? configuredTimeZoneId)
    {
        var configured = configuredTimeZoneId?.Trim();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var resolved = TryFindTimeZone(configured);
            if (resolved is not null)
            {
                return resolved;
            }
        }

        var saoPaulo = TryFindTimeZone("America/Sao_Paulo") ??
                       TryFindTimeZone("E. South America Standard Time");
        return saoPaulo ?? TimeZoneInfo.Utc;
    }

    private static string ResolveGoogleTimeZoneId(string? configuredTimeZoneId)
    {
        var configured = configuredTimeZoneId?.Trim();
        if (string.IsNullOrWhiteSpace(configured))
        {
            return "America/Sao_Paulo";
        }

        if (configured.Equals("E. South America Standard Time", StringComparison.OrdinalIgnoreCase))
        {
            return "America/Sao_Paulo";
        }

        return configured;
    }

    private static TimeZoneInfo? TryFindTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }
}
