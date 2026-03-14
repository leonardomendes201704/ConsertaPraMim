using System.Net.Http.Json;
using System.Text.Json;
using AppMobileCPM.Observability;
using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Telegram;

public sealed class TelegramBridgeObservabilityClient : ITelegramBridgeObservabilityClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly TelegramAutomationOptions _options;
    private readonly ILogger<TelegramBridgeObservabilityClient> _logger;

    public TelegramBridgeObservabilityClient(
        HttpClient httpClient,
        IOptions<TelegramAutomationOptions> options,
        ILogger<TelegramBridgeObservabilityClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<TelegramBridgeObservabilityResult> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return TelegramBridgeObservabilityResult.Failed(
                StatusCodes.Status409Conflict,
                "Automacao Telegram desabilitada no ambiente atual.");
        }

        if (_httpClient.BaseAddress is null)
        {
            return TelegramBridgeObservabilityResult.Failed(
                StatusCodes.Status503ServiceUnavailable,
                "URL do Telegram Bridge nao configurada para diagnostico.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/internal/telegram/observability/dashboard");
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        request.Headers.TryAddWithoutValidation("User-Agent", "ConsertaPraMim.Web.CpmFull/1.0");
        request.Headers.TryAddWithoutValidation(TelegramLeadAutomationService.SharedSecretHeaderName, _options.SharedSecret);
        request.Headers.TryAddWithoutValidation(
            ChatwootCorrelationContext.HeaderName,
            ChatwootCorrelationContext.GetOrCreate("telegram-diag"));

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var payload = await response.Content.ReadFromJsonAsync<TelegramBridgeObservabilityApiResponse>(JsonOptions, cancellationToken);
            if (!response.IsSuccessStatusCode || payload?.Success != true || payload.Snapshot is null)
            {
                var message = payload?.Message;
                if (string.IsNullOrWhiteSpace(message))
                {
                    message = "Nao foi possivel consultar o diagnostico do Telegram Bridge.";
                }

                _logger.LogWarning(
                    "Telegram Bridge retornou erro HTTP {StatusCode} ao carregar diagnostico. Message={Message}",
                    (int)response.StatusCode,
                    message);

                return TelegramBridgeObservabilityResult.Failed((int)response.StatusCode, message);
            }

            return TelegramBridgeObservabilityResult.Ok(payload.Snapshot);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao consultar o diagnostico interno do Telegram Bridge.");
            return TelegramBridgeObservabilityResult.Failed(
                StatusCodes.Status502BadGateway,
                "Falha ao comunicar com o Telegram Bridge para diagnostico.");
        }
    }

    private sealed class TelegramBridgeObservabilityApiResponse
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;
        public TelegramBridgeObservabilitySnapshotDto? Snapshot { get; init; }
    }
}
