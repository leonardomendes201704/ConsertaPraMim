using System.Net.Http.Json;
using System.Text.Json;
using ConsertaPraMim.Web.TelegramBridge.Models;
using ConsertaPraMim.Web.TelegramBridge.Options;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public sealed class TelegramLeadAutomationClient : ITelegramLeadAutomationClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly TelegramAutomationOptions _options;
    private readonly ILogger<TelegramLeadAutomationClient> _logger;

    public TelegramLeadAutomationClient(
        HttpClient httpClient,
        IOptions<TelegramAutomationOptions> options,
        ILogger<TelegramLeadAutomationClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<TelegramLeadAutomationUpsertResult> UpsertLeadAsync(
        TelegramLeadAutomationUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_options.Enabled)
        {
            return TelegramLeadAutomationUpsertResult.Disabled("Automacao Telegram desabilitada no ambiente atual.");
        }

        var normalizedBoardType = string.IsNullOrWhiteSpace(request.BoardType)
            ? string.Empty
            : request.BoardType.Trim().ToLowerInvariant();

        if (normalizedBoardType == "clientes" && !_options.ClientsAutomationEnabled)
        {
            return TelegramLeadAutomationUpsertResult.Disabled("Automacao Telegram para clientes desabilitada no ambiente atual.");
        }

        if (normalizedBoardType == "prestadores" && !_options.ProvidersAutomationEnabled)
        {
            return TelegramLeadAutomationUpsertResult.Disabled("Automacao Telegram para prestadores desabilitada no ambiente atual.");
        }

        if (_httpClient.BaseAddress is null)
        {
            return TelegramLeadAutomationUpsertResult.Failed(
                StatusCodes.Status503ServiceUnavailable,
                "URL do CPM Full nao configurada para automacao Telegram.");
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/integrations/telegram/automation/lead")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.TryAddWithoutValidation("Accept", "application/json");
        message.Headers.TryAddWithoutValidation("User-Agent", "ConsertaPraMim.Web.TelegramBridge/1.0");
        message.Headers.TryAddWithoutValidation("X-Telegram-Automation-Key", _options.SharedSecret);

        try
        {
            using var response = await _httpClient.SendAsync(message, cancellationToken);
            var payload = await response.Content.ReadFromJsonAsync<TelegramLeadAutomationApiResponse>(JsonOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var failureMessage = payload?.Message;
                if (string.IsNullOrWhiteSpace(failureMessage))
                {
                    failureMessage = "Falha ao sincronizar lead do Telegram com o CPM Full.";
                }

                _logger.LogWarning(
                    "Automacao Telegram retornou erro HTTP {StatusCode} ao sincronizar conversa {ChatbotConversationId}. Message={Message}",
                    (int)response.StatusCode,
                    request.ChatbotConversationId,
                    failureMessage);

                return TelegramLeadAutomationUpsertResult.Failed((int)response.StatusCode, failureMessage);
            }

            return new TelegramLeadAutomationUpsertResult
            {
                Success = payload?.Success ?? true,
                HttpStatusCode = (int)response.StatusCode,
                LeadId = payload?.LeadId ?? 0,
                Created = payload?.Created ?? false,
                BoardType = payload?.BoardType ?? request.BoardType,
                Message = payload?.Message ?? "Lead sincronizado via automacao Telegram.",
                ChatwootStatus = payload?.Chatwoot?.Status ?? string.Empty,
                ChatwootMessage = payload?.Chatwoot?.Message ?? string.Empty,
                ChatwootContactId = payload?.Chatwoot?.ContactId,
                ChatwootConversationId = payload?.Chatwoot?.ConversationId,
                ChatwootInboxId = payload?.Chatwoot?.InboxId
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
                "Falha ao chamar automacao Telegram do CPM Full para a conversa {ChatbotConversationId}.",
                request.ChatbotConversationId);
            return TelegramLeadAutomationUpsertResult.Failed(
                StatusCodes.Status502BadGateway,
                "Falha ao comunicar com o CPM Full para automacao do Telegram.");
        }
    }

    private sealed class TelegramLeadAutomationApiResponse
    {
        public bool Success { get; init; }
        public int LeadId { get; init; }
        public bool Created { get; init; }
        public string BoardType { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public ChatwootAutomationApiResponse? Chatwoot { get; init; }
    }

    private sealed class ChatwootAutomationApiResponse
    {
        public string Status { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public long? ContactId { get; init; }
        public long? ConversationId { get; init; }
        public long? InboxId { get; init; }
    }
}
