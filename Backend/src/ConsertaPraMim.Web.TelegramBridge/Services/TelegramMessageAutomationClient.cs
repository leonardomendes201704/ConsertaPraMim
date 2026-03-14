using System.Net.Http.Json;
using System.Text.Json;
using ConsertaPraMim.Web.TelegramBridge.Models;
using ConsertaPraMim.Web.TelegramBridge.Options;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public sealed class TelegramMessageAutomationClient : ITelegramMessageAutomationClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly TelegramAutomationOptions _options;
    private readonly ILogger<TelegramMessageAutomationClient> _logger;

    public TelegramMessageAutomationClient(
        HttpClient httpClient,
        IOptions<TelegramAutomationOptions> options,
        ILogger<TelegramMessageAutomationClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<TelegramInboundMessageAutomationResult> MirrorInboundMessageAsync(
        TelegramInboundMessageAutomationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_options.Enabled || !_options.MirrorMessagesEnabled)
        {
            return TelegramInboundMessageAutomationResult.Disabled("Espelhamento Telegram desabilitado no ambiente atual.");
        }

        if (_httpClient.BaseAddress is null)
        {
            return TelegramInboundMessageAutomationResult.Failed(
                StatusCodes.Status503ServiceUnavailable,
                "URL do CPM Full nao configurada para espelhamento Telegram.");
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/integrations/telegram/automation/message")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.TryAddWithoutValidation("Accept", "application/json");
        message.Headers.TryAddWithoutValidation("User-Agent", "ConsertaPraMim.Web.TelegramBridge/1.0");
        message.Headers.TryAddWithoutValidation("X-Telegram-Automation-Key", _options.SharedSecret);

        try
        {
            using var response = await _httpClient.SendAsync(message, cancellationToken);
            var payload = await response.Content.ReadFromJsonAsync<TelegramInboundMessageAutomationApiResponse>(JsonOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var failureMessage = payload?.Message;
                if (string.IsNullOrWhiteSpace(failureMessage))
                {
                    failureMessage = "Falha ao espelhar mensagem Telegram no CPM Full.";
                }

                _logger.LogWarning(
                    "Automacao Telegram retornou erro HTTP {StatusCode} no espelhamento da mensagem {ChannelMessageId}. Message={Message}",
                    (int)response.StatusCode,
                    request.ChannelMessageId,
                    failureMessage);

                return TelegramInboundMessageAutomationResult.Failed((int)response.StatusCode, failureMessage);
            }

            return new TelegramInboundMessageAutomationResult
            {
                Success = payload?.Success ?? true,
                HttpStatusCode = (int)response.StatusCode,
                LeadId = payload?.LeadId ?? 0,
                QueueStatus = payload?.QueueStatus ?? string.Empty,
                Duplicate = payload?.Duplicate ?? false,
                Message = payload?.Message ?? "Mensagem Telegram enviada ao CPM Full para espelhamento."
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Falha ao chamar automacao de espelhamento Telegram do CPM Full para a mensagem {ChannelMessageId}.",
                request.ChannelMessageId);
            return TelegramInboundMessageAutomationResult.Failed(
                StatusCodes.Status502BadGateway,
                "Falha ao comunicar com o CPM Full para espelhamento Telegram.");
        }
    }

    private sealed class TelegramInboundMessageAutomationApiResponse
    {
        public bool Success { get; init; }
        public int LeadId { get; init; }
        public string QueueStatus { get; init; } = string.Empty;
        public bool Duplicate { get; init; }
        public string Message { get; init; } = string.Empty;
    }
}
