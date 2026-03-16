using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyProviderDispatchLinkService : IJourneyProviderDispatchLinkService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly JourneyProviderNotificationOptions _options;
    private readonly byte[] _secretBytes;

    public JourneyProviderDispatchLinkService(IOptions<JourneyProviderNotificationOptions> options)
    {
        _options = options.Value;
        _secretBytes = Encoding.UTF8.GetBytes(_options.LinkSigningSecret.Trim());
    }

    public string GenerateToken(
        string purpose,
        int leadId,
        int journeyId,
        Guid providerId,
        string targetKey,
        DateTime expiresAtUtc)
    {
        var normalizedExpiry = NormalizeUtc(expiresAtUtc) ?? DateTime.UtcNow.AddMinutes(_options.LinkExpirationMinutes);
        var payload = new JourneyProviderDispatchSignedTokenPayload
        {
            Purpose = purpose?.Trim() ?? string.Empty,
            LeadId = leadId,
            JourneyId = journeyId,
            ProviderId = providerId,
            TargetKey = targetKey?.Trim() ?? string.Empty,
            ExpiresAtUtc = normalizedExpiry
        };

        var payloadBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions));
        var signatureBytes = Sign(payloadBytes);
        return $"{WebEncoders.Base64UrlEncode(payloadBytes)}.{WebEncoders.Base64UrlEncode(signatureBytes)}";
    }

    public JourneyProviderDispatchTokenValidationResult ValidateToken(string token, string expectedPurpose, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new JourneyProviderDispatchTokenValidationResult
            {
                Success = false,
                Message = "Token de oportunidade ausente."
            };
        }

        var segments = token.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2)
        {
            return new JourneyProviderDispatchTokenValidationResult
            {
                Success = false,
                Message = "Token de oportunidade invalido."
            };
        }

        try
        {
            var payloadBytes = WebEncoders.Base64UrlDecode(segments[0]);
            var signatureBytes = WebEncoders.Base64UrlDecode(segments[1]);
            var expectedSignature = Sign(payloadBytes);
            if (!CryptographicOperations.FixedTimeEquals(signatureBytes, expectedSignature))
            {
                return new JourneyProviderDispatchTokenValidationResult
                {
                    Success = false,
                    Message = "Assinatura do token invalida."
                };
            }

            var payload = JsonSerializer.Deserialize<JourneyProviderDispatchSignedTokenPayload>(payloadBytes, JsonOptions);
            if (payload is null ||
                string.IsNullOrWhiteSpace(payload.Purpose) ||
                payload.LeadId <= 0 ||
                payload.JourneyId <= 0 ||
                payload.ProviderId == Guid.Empty ||
                string.IsNullOrWhiteSpace(payload.TargetKey))
            {
                return new JourneyProviderDispatchTokenValidationResult
                {
                    Success = false,
                    Message = "Token de oportunidade incompleto."
                };
            }

            if (!string.Equals(payload.Purpose, expectedPurpose?.Trim(), StringComparison.Ordinal))
            {
                return new JourneyProviderDispatchTokenValidationResult
                {
                    Success = false,
                    Payload = payload,
                    Message = "Token usado em um contexto incorreto."
                };
            }

            var normalizedNowUtc = NormalizeUtc(nowUtc) ?? DateTime.UtcNow;
            if (payload.ExpiresAtUtc <= normalizedNowUtc)
            {
                return new JourneyProviderDispatchTokenValidationResult
                {
                    Success = false,
                    Expired = true,
                    Payload = payload,
                    Message = "O link da oportunidade expirou."
                };
            }

            return new JourneyProviderDispatchTokenValidationResult
            {
                Success = true,
                Payload = payload
            };
        }
        catch
        {
            return new JourneyProviderDispatchTokenValidationResult
            {
                Success = false,
                Message = "Nao foi possivel validar o link da oportunidade."
            };
        }
    }

    public Uri BuildResponsePageUrl(string token, string action)
    {
        return BuildUrl($"/prestadores/oportunidades/responder?token={Uri.EscapeDataString(token)}&acao={Uri.EscapeDataString(JourneyProviderOpportunityActions.Normalize(action))}");
    }

    public Uri BuildOpenTrackingUrl(string token)
    {
        return BuildUrl($"/prestadores/oportunidades/rastreio-abertura?token={Uri.EscapeDataString(token)}");
    }

    private Uri BuildUrl(string relativePathAndQuery)
    {
        var baseUri = new Uri(_options.PublicBaseUrl.Trim(), UriKind.Absolute);
        return new Uri(baseUri, relativePathAndQuery);
    }

    private byte[] Sign(byte[] payloadBytes)
    {
        using var hmac = new HMACSHA256(_secretBytes);
        return hmac.ComputeHash(payloadBytes);
    }

    private static DateTime? NormalizeUtc(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }
}
