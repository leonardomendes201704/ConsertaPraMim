using System.Net.Http.Json;
using System.Text.Json;
using AppMobileCPM.Observability;
using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Telegram;

public sealed class TelegramBridgeDeliveryClient : ITelegramBridgeDeliveryClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly TelegramAutomationOptions _options;
    private readonly ILogger<TelegramBridgeDeliveryClient> _logger;

    public TelegramBridgeDeliveryClient(
        HttpClient httpClient,
        IOptions<TelegramAutomationOptions> options,
        ILogger<TelegramBridgeDeliveryClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<TelegramBridgeHumanReplyResult> SendHumanReplyAsync(
        TelegramBridgeHumanReplyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_options.Enabled || !_options.MirrorMessagesEnabled)
        {
            return TelegramBridgeHumanReplyResult.Failed(
                StatusCodes.Status409Conflict,
                "Espelhamento Telegram desabilitado no ambiente atual.");
        }

        if (_httpClient.BaseAddress is null)
        {
            return TelegramBridgeHumanReplyResult.Failed(
                StatusCodes.Status503ServiceUnavailable,
                "URL do Telegram Bridge nao configurada para entrega de mensagens humanas.");
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/internal/telegram/messages/send")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.TryAddWithoutValidation("Accept", "application/json");
        message.Headers.TryAddWithoutValidation("User-Agent", "ConsertaPraMim.Web.CpmFull/1.0");
        message.Headers.TryAddWithoutValidation(TelegramLeadAutomationService.SharedSecretHeaderName, _options.SharedSecret);
        message.Headers.TryAddWithoutValidation(
            ChatwootCorrelationContext.HeaderName,
            ChatwootCorrelationContext.GetOrCreate("telegram-outbound"));

        try
        {
            using var response = await _httpClient.SendAsync(message, cancellationToken);
            var payload = await response.Content.ReadFromJsonAsync<TelegramBridgeHumanReplyResponse>(JsonOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var failureMessage = payload?.Message;
                if (string.IsNullOrWhiteSpace(failureMessage))
                {
                    failureMessage = "Falha ao enviar resposta humana do Chatwoot para o Telegram.";
                }

                _logger.LogWarning(
                    "Telegram Bridge retornou erro HTTP {StatusCode} no envio da conversa {ConversationId} para o chat {TelegramChatId}. Message={Message}",
                    (int)response.StatusCode,
                    request.ChatwootConversationId,
                    TelegramSecuritySanitizer.MaskChatId(request.TelegramChatId),
                    TelegramSecuritySanitizer.SanitizeMessage(failureMessage, 300));

                return TelegramBridgeHumanReplyResult.Failed((int)response.StatusCode, failureMessage);
            }

            return new TelegramBridgeHumanReplyResult
            {
                Success = payload?.Success ?? true,
                HttpStatusCode = (int)response.StatusCode,
                Message = payload?.Message ?? "Mensagem humana enviada para o Telegram.",
                HumanHandoffActivated = payload?.HumanHandoffActivated ?? request.ActivateHumanHandoff
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
                "Falha ao comunicar com o Telegram Bridge para enviar resposta da conversa {ConversationId}.",
                request.ChatwootConversationId);
            return TelegramBridgeHumanReplyResult.Failed(
                StatusCodes.Status502BadGateway,
                "Falha ao comunicar com o Telegram Bridge para entrega da mensagem humana.");
        }
    }

    public async Task<TelegramBridgeResetHandoffResult> ResetHumanHandoffAsync(
        TelegramBridgeResetHandoffRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.TelegramChatId <= 0)
        {
            return TelegramBridgeResetHandoffResult.Failed(
                StatusCodes.Status400BadRequest,
                "TelegramChatId invalido para reset de handoff.");
        }

        if (_httpClient.BaseAddress is null)
        {
            return TelegramBridgeResetHandoffResult.Failed(
                StatusCodes.Status503ServiceUnavailable,
                "URL do Telegram Bridge nao configurada para reset de handoff.");
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/internal/telegram/messages/handoff/reset")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.TryAddWithoutValidation("Accept", "application/json");
        message.Headers.TryAddWithoutValidation("User-Agent", "ConsertaPraMim.Web.CpmFull/1.0");
        message.Headers.TryAddWithoutValidation(TelegramLeadAutomationService.SharedSecretHeaderName, _options.SharedSecret);
        message.Headers.TryAddWithoutValidation(
            ChatwootCorrelationContext.HeaderName,
            ChatwootCorrelationContext.GetOrCreate("telegram-handoff-reset"));

        try
        {
            using var response = await _httpClient.SendAsync(message, cancellationToken);
            var payload = await response.Content.ReadFromJsonAsync<TelegramBridgeResetHandoffResponse>(JsonOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var failureMessage = payload?.Message;
                if (string.IsNullOrWhiteSpace(failureMessage))
                {
                    failureMessage = "Falha ao resetar handoff humano no Telegram Bridge.";
                }

                _logger.LogWarning(
                    "Telegram Bridge retornou erro HTTP {StatusCode} ao resetar handoff do chat {TelegramChatId}. Message={Message}",
                    (int)response.StatusCode,
                    TelegramSecuritySanitizer.MaskChatId(request.TelegramChatId),
                    TelegramSecuritySanitizer.SanitizeMessage(failureMessage, 300));

                return TelegramBridgeResetHandoffResult.Failed((int)response.StatusCode, failureMessage);
            }

            return new TelegramBridgeResetHandoffResult
            {
                Success = payload?.Success ?? true,
                HttpStatusCode = (int)response.StatusCode,
                Message = payload?.Message ?? "Handoff humano do Telegram resetado com sucesso.",
                HandoffWasActive = payload?.HandoffWasActive ?? false
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
                "Falha ao comunicar com o Telegram Bridge para resetar handoff do chat {TelegramChatId}.",
                TelegramSecuritySanitizer.MaskChatId(request.TelegramChatId));
            return TelegramBridgeResetHandoffResult.Failed(
                StatusCodes.Status502BadGateway,
                "Falha ao comunicar com o Telegram Bridge para reset de handoff.");
        }
    }
}
