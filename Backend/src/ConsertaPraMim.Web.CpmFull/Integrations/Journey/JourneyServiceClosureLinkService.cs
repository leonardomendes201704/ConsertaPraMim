using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyServiceClosureLinkService : IJourneyServiceClosureLinkService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly JourneyProviderNotificationOptions _notificationOptions;
    private readonly byte[] _secretBytes;

    public JourneyServiceClosureLinkService(IOptions<JourneyProviderNotificationOptions> notificationOptions)
    {
        _notificationOptions = notificationOptions.Value;
        _secretBytes = Encoding.UTF8.GetBytes(_notificationOptions.LinkSigningSecret.Trim());
    }

    public string GenerateToken(string purpose, string audience, int leadId, int journeyId, Guid providerId, DateTime expiresAtUtc)
    {
        var payload = new JourneyServiceClosureSignedTokenPayload
        {
            Purpose = purpose?.Trim() ?? string.Empty,
            Audience = JourneyServiceClosureAudiences.Normalize(audience),
            LeadId = leadId,
            JourneyId = journeyId,
            ProviderId = providerId,
            ExpiresAtUtc = NormalizeUtc(expiresAtUtc) ?? DateTime.UtcNow.AddHours(24)
        };

        var payloadBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions));
        var signatureBytes = Sign(payloadBytes);
        return $"{WebEncoders.Base64UrlEncode(payloadBytes)}.{WebEncoders.Base64UrlEncode(signatureBytes)}";
    }

    public JourneyServiceClosureTokenValidationResult ValidateToken(string token, string expectedPurpose, string expectedAudience, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new JourneyServiceClosureTokenValidationResult { Message = "Token ausente para a jornada." };
        }

        var segments = token.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2)
        {
            return new JourneyServiceClosureTokenValidationResult { Message = "Token invalido para a jornada." };
        }

        try
        {
            var payloadBytes = WebEncoders.Base64UrlDecode(segments[0]);
            var signatureBytes = WebEncoders.Base64UrlDecode(segments[1]);
            var expectedSignature = Sign(payloadBytes);
            if (!CryptographicOperations.FixedTimeEquals(signatureBytes, expectedSignature))
            {
                return new JourneyServiceClosureTokenValidationResult { Message = "Assinatura invalida para a jornada." };
            }

            var payload = JsonSerializer.Deserialize<JourneyServiceClosureSignedTokenPayload>(payloadBytes, JsonOptions);
            if (payload is null ||
                payload.LeadId <= 0 ||
                payload.JourneyId <= 0 ||
                string.IsNullOrWhiteSpace(payload.Purpose) ||
                string.IsNullOrWhiteSpace(payload.Audience))
            {
                return new JourneyServiceClosureTokenValidationResult { Message = "Token incompleto para a jornada." };
            }

            if (!string.Equals(payload.Purpose, expectedPurpose?.Trim(), StringComparison.Ordinal) ||
                !string.Equals(payload.Audience, JourneyServiceClosureAudiences.Normalize(expectedAudience), StringComparison.Ordinal))
            {
                return new JourneyServiceClosureTokenValidationResult
                {
                    Payload = payload,
                    Message = "Token usado em contexto incorreto."
                };
            }

            var normalizedNowUtc = NormalizeUtc(nowUtc) ?? DateTime.UtcNow;
            if (payload.ExpiresAtUtc <= normalizedNowUtc)
            {
                return new JourneyServiceClosureTokenValidationResult
                {
                    Payload = payload,
                    Expired = true,
                    Message = "O link da jornada expirou."
                };
            }

            return new JourneyServiceClosureTokenValidationResult
            {
                Success = true,
                Payload = payload
            };
        }
        catch
        {
            return new JourneyServiceClosureTokenValidationResult
            {
                Message = "Nao foi possivel validar o link da jornada."
            };
        }
    }

    public Uri BuildProviderCompletionUrl(string token) =>
        BuildUrl($"/jornada/encerramento/prestador?token={Uri.EscapeDataString(token)}");

    public Uri BuildClientCompletionUrl(string token, string action) =>
        BuildUrl($"/jornada/encerramento/cliente?token={Uri.EscapeDataString(token)}&acao={Uri.EscapeDataString(JourneyServiceClosureReviewActions.Normalize(action))}");

    public Uri BuildReviewUrl(string token) =>
        BuildUrl($"/jornada/avaliacoes/responder?token={Uri.EscapeDataString(token)}");

    private Uri BuildUrl(string relativePathAndQuery)
    {
        var baseUri = new Uri(_notificationOptions.PublicBaseUrl.Trim(), UriKind.Absolute);
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
