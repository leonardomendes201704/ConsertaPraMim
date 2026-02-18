using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Web.Client.Security;

namespace ConsertaPraMim.Web.Client.Services;

public class ClientApiPaymentWebhookService : IPaymentWebhookService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ClientApiPaymentWebhookService> _logger;

    public ClientApiPaymentWebhookService(
        IHttpClientFactory httpClientFactory,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        ILogger<ClientApiPaymentWebhookService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<PaymentWebhookProcessResultDto> ProcessWebhookAsync(
        PaymentWebhookRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryBuildAbsoluteUrl($"/api/payments/webhook/{request.Provider}", out var url))
        {
            return new PaymentWebhookProcessResultDto(false, ErrorCode: "config_error", ErrorMessage: "ApiBaseUrl nao configurada.");
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            if (TryGetApiToken(out var token))
            {
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            if (!string.IsNullOrWhiteSpace(request.Signature))
            {
                httpRequest.Headers.TryAddWithoutValidation("X-Payment-Signature", request.Signature);
            }

            if (!string.IsNullOrWhiteSpace(request.EventId))
            {
                httpRequest.Headers.TryAddWithoutValidation("X-Payment-Event-Id", request.EventId);
            }

            httpRequest.Content = new StringContent(request.RawBody ?? string.Empty, Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(httpRequest, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return new PaymentWebhookProcessResultDto(true);
                }

                var successPayload = JsonSerializer.Deserialize<PaymentWebhookProcessResultDto>(raw, JsonOptions);
                return successPayload ?? new PaymentWebhookProcessResultDto(true);
            }

            var errorPayload = string.IsNullOrWhiteSpace(raw)
                ? null
                : JsonSerializer.Deserialize<PaymentWebhookProcessResultDto>(raw, JsonOptions);

            return errorPayload ?? new PaymentWebhookProcessResultDto(
                false,
                ErrorCode: "api_error",
                ErrorMessage: $"Falha ao processar webhook ({(int)response.StatusCode}).");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao encaminhar webhook de pagamento via API.");
            return new PaymentWebhookProcessResultDto(false, ErrorCode: "communication_error", ErrorMessage: "Falha de comunicacao com a API.");
        }
    }

    private bool TryBuildAbsoluteUrl(string relativePath, out string absoluteUrl)
    {
        var baseUrl = _configuration["ApiBaseUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            absoluteUrl = string.Empty;
            return false;
        }

        absoluteUrl = $"{baseUrl.TrimEnd('/')}{relativePath}";
        return true;
    }

    private bool TryGetApiToken(out string token)
    {
        token = _httpContextAccessor.HttpContext?.User.FindFirst(WebClientClaimTypes.ApiToken)?.Value ?? string.Empty;
        return !string.IsNullOrWhiteSpace(token);
    }
}
