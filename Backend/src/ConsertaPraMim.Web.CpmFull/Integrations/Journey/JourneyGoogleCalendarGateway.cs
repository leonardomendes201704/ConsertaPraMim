using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyGoogleCalendarGateway : IJourneyCalendarGateway
{
    private const string CalendarScope = "https://www.googleapis.com/auth/calendar";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly JourneySchedulingOptions _options;
    private readonly ILogger<JourneyGoogleCalendarGateway> _logger;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private string _cachedAccessToken = string.Empty;
    private DateTimeOffset _cachedAccessTokenExpiresAtUtc = DateTimeOffset.MinValue;

    public JourneyGoogleCalendarGateway(
        IHttpClientFactory httpClientFactory,
        IOptions<JourneySchedulingOptions> options,
        ILogger<JourneyGoogleCalendarGateway> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<JourneyCalendarBusySlot>> ListBusySlotsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return [];
        }

        var payload = new
        {
            timeMin = NormalizeUtc(fromUtc).ToString("O"),
            timeMax = NormalizeUtc(toUtc).ToString("O"),
            timeZone = _options.Timezone.Trim(),
            items = new[]
            {
                new { id = _options.CalendarId.Trim() }
            }
        };

        using var document = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            "https://www.googleapis.com/calendar/v3/freeBusy",
            payload,
            cancellationToken);

        if (document.RootElement.TryGetProperty("calendars", out var calendarsElement) &&
            calendarsElement.TryGetProperty(_options.CalendarId.Trim(), out var calendarElement) &&
            calendarElement.TryGetProperty("busy", out var busyElement))
        {
            var slots = new List<JourneyCalendarBusySlot>();
            foreach (var item in busyElement.EnumerateArray())
            {
                if (!item.TryGetProperty("start", out var startElement) ||
                    !item.TryGetProperty("end", out var endElement) ||
                    !DateTime.TryParse(startElement.GetString(), out var startAt) ||
                    !DateTime.TryParse(endElement.GetString(), out var endAt))
                {
                    continue;
                }

                slots.Add(new JourneyCalendarBusySlot
                {
                    StartsAtUtc = NormalizeUtc(startAt),
                    EndsAtUtc = NormalizeUtc(endAt)
                });
            }

            return slots
                .OrderBy(item => item.StartsAtUtc)
                .ToList();
        }

        return [];
    }

    public async Task<JourneyCalendarEventUpsertResult> CreateEventAsync(
        JourneyCalendarEventUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return new JourneyCalendarEventUpsertResult
            {
                Success = false,
                ErrorCode = "calendar_disabled",
                ErrorMessage = "Integracao Google Calendar desabilitada."
            };
        }

        var validationError = ValidateEventRequest(request);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            return new JourneyCalendarEventUpsertResult
            {
                Success = false,
                ErrorCode = "invalid_request",
                ErrorMessage = validationError
            };
        }

        var uri = $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(_options.CalendarId.Trim())}/events";

        try
        {
            using var document = await SendAuthorizedJsonAsync(
                HttpMethod.Post,
                uri,
                BuildEventPayload(request),
                cancellationToken);

            return ReadUpsertResult(document.RootElement, request.IdempotencyKey);
        }
        catch (JourneyCalendarHttpException exception) when (exception.StatusCode == HttpStatusCode.Conflict && IsValidIdempotencyKey(request.IdempotencyKey))
        {
            _logger.LogInformation(
                "Evento de jornada ja existe para a chave idempotente {IdempotencyKey}.",
                request.IdempotencyKey);

            return new JourneyCalendarEventUpsertResult
            {
                Success = true,
                EventId = request.IdempotencyKey.Trim()
            };
        }
        catch (JourneyCalendarHttpException exception)
        {
            _logger.LogWarning(
                exception,
                "Falha ao criar evento da jornada no Google Calendar. StatusCode={StatusCode}",
                (int)exception.StatusCode);

            return new JourneyCalendarEventUpsertResult
            {
                Success = false,
                ErrorCode = "google_calendar_create_failed",
                ErrorMessage = exception.Message
            };
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Erro inesperado ao criar evento da jornada no Google Calendar.");
            return new JourneyCalendarEventUpsertResult
            {
                Success = false,
                ErrorCode = "google_calendar_unexpected_error",
                ErrorMessage = "Erro inesperado ao criar evento no Google Calendar."
            };
        }
    }

    public async Task<JourneyCalendarEventUpsertResult> UpdateEventAsync(
        string eventId,
        JourneyCalendarEventUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return new JourneyCalendarEventUpsertResult
            {
                Success = false,
                ErrorCode = "calendar_disabled",
                ErrorMessage = "Integracao Google Calendar desabilitada."
            };
        }

        if (string.IsNullOrWhiteSpace(eventId))
        {
            return new JourneyCalendarEventUpsertResult
            {
                Success = false,
                ErrorCode = "invalid_event_id",
                ErrorMessage = "EventId obrigatorio."
            };
        }

        var validationError = ValidateEventRequest(request);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            return new JourneyCalendarEventUpsertResult
            {
                Success = false,
                ErrorCode = "invalid_request",
                ErrorMessage = validationError
            };
        }

        var uri = $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(_options.CalendarId.Trim())}/events/{Uri.EscapeDataString(eventId.Trim())}";

        try
        {
            using var document = await SendAuthorizedJsonAsync(
                HttpMethod.Put,
                uri,
                BuildEventPayload(request),
                cancellationToken);

            return ReadUpsertResult(document.RootElement, eventId);
        }
        catch (JourneyCalendarHttpException exception)
        {
            _logger.LogWarning(
                exception,
                "Falha ao atualizar evento {EventId} da jornada no Google Calendar. StatusCode={StatusCode}",
                eventId,
                (int)exception.StatusCode);

            return new JourneyCalendarEventUpsertResult
            {
                Success = false,
                ErrorCode = "google_calendar_update_failed",
                ErrorMessage = exception.Message
            };
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Erro inesperado ao atualizar evento {EventId} da jornada no Google Calendar.", eventId);
            return new JourneyCalendarEventUpsertResult
            {
                Success = false,
                ErrorCode = "google_calendar_unexpected_error",
                ErrorMessage = "Erro inesperado ao atualizar evento no Google Calendar."
            };
        }
    }

    public async Task<JourneyCalendarEventDeleteResult> DeleteEventAsync(
        string eventId,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return new JourneyCalendarEventDeleteResult
            {
                Success = false,
                ErrorCode = "calendar_disabled",
                ErrorMessage = "Integracao Google Calendar desabilitada."
            };
        }

        if (string.IsNullOrWhiteSpace(eventId))
        {
            return new JourneyCalendarEventDeleteResult
            {
                Success = false,
                ErrorCode = "invalid_event_id",
                ErrorMessage = "EventId obrigatorio."
            };
        }

        var uri = $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(_options.CalendarId.Trim())}/events/{Uri.EscapeDataString(eventId.Trim())}";

        try
        {
            using var _ = await SendAuthorizedJsonAsync(HttpMethod.Delete, uri, payload: null, cancellationToken);
            return new JourneyCalendarEventDeleteResult { Success = true };
        }
        catch (JourneyCalendarHttpException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return new JourneyCalendarEventDeleteResult { Success = true };
        }
        catch (JourneyCalendarHttpException exception)
        {
            _logger.LogWarning(
                exception,
                "Falha ao remover evento {EventId} da jornada no Google Calendar. StatusCode={StatusCode}",
                eventId,
                (int)exception.StatusCode);

            return new JourneyCalendarEventDeleteResult
            {
                Success = false,
                ErrorCode = "google_calendar_delete_failed",
                ErrorMessage = exception.Message
            };
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Erro inesperado ao remover evento {EventId} da jornada no Google Calendar.", eventId);
            return new JourneyCalendarEventDeleteResult
            {
                Success = false,
                ErrorCode = "google_calendar_unexpected_error",
                ErrorMessage = "Erro inesperado ao remover evento no Google Calendar."
            };
        }
    }

    private async Task<JsonDocument> SendAuthorizedJsonAsync(
        HttpMethod method,
        string requestUri,
        object? payload,
        CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(_options.RequestTimeoutSeconds);

        using var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(cancellationToken));

        if (payload is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        }

        using var response = await client.SendAsync(request, cancellationToken);
        var content = response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new JourneyCalendarHttpException(response.StatusCode, ExtractErrorMessage(content));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return JsonDocument.Parse("{}");
        }

        return JsonDocument.Parse(content);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(_cachedAccessToken) && _cachedAccessTokenExpiresAtUtc > now)
        {
            return _cachedAccessToken;
        }

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            now = DateTimeOffset.UtcNow;
            if (!string.IsNullOrWhiteSpace(_cachedAccessToken) && _cachedAccessTokenExpiresAtUtc > now)
            {
                return _cachedAccessToken;
            }

            var assertion = BuildServiceAccountAssertion(now);
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(_options.RequestTimeoutSeconds);

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
            {
                Content = new FormUrlEncodedContent(
                [
                    new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer"),
                    new KeyValuePair<string, string>("assertion", assertion)
                ])
            };

            using var response = await client.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new JourneyCalendarHttpException(response.StatusCode, ExtractErrorMessage(content));
            }

            var envelope = JsonSerializer.Deserialize<JourneyGoogleAccessTokenEnvelope>(content, JsonOptions)
                ?? throw new InvalidOperationException("Resposta invalida ao solicitar token do Google Calendar.");

            if (string.IsNullOrWhiteSpace(envelope.AccessToken))
            {
                throw new InvalidOperationException("Token de acesso do Google Calendar veio vazio.");
            }

            _cachedAccessToken = envelope.AccessToken.Trim();
            _cachedAccessTokenExpiresAtUtc = now.AddSeconds(Math.Max(envelope.ExpiresIn - (_options.TokenRefreshSafetyMinutes * 60), 60));
            return _cachedAccessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private string BuildServiceAccountAssertion(DateTimeOffset issuedAtUtc)
    {
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes("""{"alg":"RS256","typ":"JWT"}"""));
        var payloadJson = JsonSerializer.Serialize(new
        {
            iss = _options.ServiceAccountEmail.Trim(),
            scope = CalendarScope,
            aud = "https://oauth2.googleapis.com/token",
            exp = issuedAtUtc.AddMinutes(55).ToUnixTimeSeconds(),
            iat = issuedAtUtc.ToUnixTimeSeconds()
        });
        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
        var unsignedToken = $"{header}.{payload}";

        using var rsa = RSA.Create();
        rsa.ImportFromPem(NormalizePrivateKey(_options.PrivateKey));
        var signature = rsa.SignData(Encoding.UTF8.GetBytes(unsignedToken), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{unsignedToken}.{Base64UrlEncode(signature)}";
    }

    private object BuildEventPayload(JourneyCalendarEventUpsertRequest request)
    {
        var metadata = request.Metadata
            .Where(item => !string.IsNullOrWhiteSpace(item.Key) && !string.IsNullOrWhiteSpace(item.Value))
            .ToDictionary(item => item.Key.Trim(), item => item.Value.Trim(), StringComparer.OrdinalIgnoreCase);

        return new
        {
            id = NormalizeIdempotencyKey(request.IdempotencyKey),
            summary = request.Title.Trim(),
            description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim(),
            start = new
            {
                dateTime = NormalizeUtc(request.StartsAtUtc).ToString("O"),
                timeZone = _options.Timezone.Trim()
            },
            end = new
            {
                dateTime = NormalizeUtc(request.EndsAtUtc).ToString("O"),
                timeZone = _options.Timezone.Trim()
            },
            extendedProperties = metadata.Count == 0
                ? null
                : new
                {
                    @private = metadata
                }
        };
    }

    private static JourneyCalendarEventUpsertResult ReadUpsertResult(JsonElement root, string fallbackEventId)
    {
        var eventId = root.TryGetProperty("id", out var idElement)
            ? idElement.GetString() ?? string.Empty
            : fallbackEventId;
        var htmlLink = root.TryGetProperty("htmlLink", out var htmlLinkElement)
            ? htmlLinkElement.GetString() ?? string.Empty
            : string.Empty;

        return new JourneyCalendarEventUpsertResult
        {
            Success = true,
            EventId = eventId,
            EventLink = htmlLink
        };
    }

    private static string? ValidateEventRequest(JourneyCalendarEventUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return "Titulo do evento obrigatorio.";
        }

        var startsAtUtc = NormalizeUtc(request.StartsAtUtc);
        var endsAtUtc = NormalizeUtc(request.EndsAtUtc);
        if (endsAtUtc <= startsAtUtc)
        {
            return "Janela do evento invalida.";
        }

        if (!IsValidIdempotencyKey(request.IdempotencyKey))
        {
            return "IdempotencyKey invalido (use 5-128 chars com [a-z0-9_-]).";
        }

        return null;
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

        return normalized.All(ch => char.IsLower(ch) || char.IsDigit(ch) || ch is '-' or '_');
    }

    private static string? NormalizeIdempotencyKey(string? idempotencyKey) =>
        string.IsNullOrWhiteSpace(idempotencyKey)
            ? null
            : idempotencyKey.Trim();

    private static string NormalizePrivateKey(string privateKey)
    {
        var normalized = privateKey.Trim();
        if (normalized.StartsWith('"') && normalized.EndsWith('"') && normalized.Length >= 2)
        {
            normalized = normalized[1..^1];
        }

        return normalized.Replace("\\n", "\n", StringComparison.Ordinal);
    }

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static string ExtractErrorMessage(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return "Falha na integracao com Google Calendar.";
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            if (document.RootElement.TryGetProperty("error", out var errorElement))
            {
                if (errorElement.ValueKind == JsonValueKind.Object &&
                    errorElement.TryGetProperty("message", out var messageElement) &&
                    !string.IsNullOrWhiteSpace(messageElement.GetString()))
                {
                    return messageElement.GetString()!.Trim();
                }

                if (errorElement.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(errorElement.GetString()))
                {
                    return errorElement.GetString()!.Trim();
                }
            }
        }
        catch (JsonException)
        {
        }

        return content.Trim();
    }

    private static string Base64UrlEncode(byte[] value)
    {
        return Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private sealed class JourneyCalendarHttpException : Exception
    {
        public JourneyCalendarHttpException(HttpStatusCode statusCode, string message)
            : base(message)
        {
            StatusCode = statusCode;
        }

        public HttpStatusCode StatusCode { get; }
    }
}
