using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ConsertaPraMim.Web.TelegramBridge.Models;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public sealed class TelegramChatbotApiClient : ITelegramChatbotApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TelegramChatbotApiClient> _logger;

    public TelegramChatbotApiClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<TelegramChatbotApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Guid?> OpenOrResumeSessionAsync(
        string apiToken,
        long chatId,
        string? title,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            channel = "telegram",
            channelConversationId = chatId.ToString(),
            lastStep = "bridge_open_conversation",
            metadataJson = BuildMetadataJson(title),
            interactionAtUtc = DateTime.UtcNow
        };

        var conversation = await PostAsync<TelegramChatbotConversationApiResponse>(
            apiToken,
            "/api/telegram-chatbot/session",
            payload,
            cancellationToken);

        return conversation?.Id;
    }

    public async Task<bool> RegisterOutgoingMessageAsync(
        string apiToken,
        long chatId,
        ChatMessageDto message,
        CancellationToken cancellationToken = default)
    {
        return await RegisterMessageAsync(
            apiToken,
            chatId,
            message,
            direction: 2,
            source: "telegram_bridge_panel",
            cancellationToken);
    }

    public async Task<bool> RegisterIncomingMessageAsync(
        string apiToken,
        long chatId,
        ChatMessageDto message,
        CancellationToken cancellationToken = default)
    {
        return await RegisterMessageAsync(
            apiToken,
            chatId,
            message,
            direction: 1,
            source: "telegram_bridge_client",
            cancellationToken);
    }

    private async Task<bool> RegisterMessageAsync(
        string apiToken,
        long chatId,
        ChatMessageDto message,
        int direction,
        string source,
        CancellationToken cancellationToken)
    {
        var conversationId = await OpenOrResumeSessionAsync(apiToken, chatId, message.SenderDisplayName, cancellationToken);
        if (!conversationId.HasValue)
        {
            return false;
        }

        var payload = new
        {
            conversationId = conversationId.Value,
            direction,
            source,
            channelMessageId = message.Id,
            content = message.Text,
            sentAtUtc = message.SentAtUtc.UtcDateTime,
            metadataJson = BuildMessageMetadataJson(message)
        };

        var response = await PostAsync<JsonElement>(
            apiToken,
            "/api/telegram-chatbot/messages",
            payload,
            cancellationToken);

        return response.ValueKind != JsonValueKind.Undefined;
    }

    private async Task<TResponse?> PostAsync<TResponse>(
        string apiToken,
        string relativePath,
        object payload,
        CancellationToken cancellationToken)
    {
        var apiBaseUrl = _configuration["ApiBaseUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            _logger.LogWarning("ApiBaseUrl nao configurada para TelegramChatbotApiClient.");
            return default;
        }

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

        try
        {
            using var response = await client.PostAsJsonAsync(
                $"{apiBaseUrl.TrimEnd('/')}{relativePath}",
                payload,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Falha ao sincronizar chatbot na API. Path: {Path}. Status: {StatusCode}. Body: {Body}",
                    relativePath,
                    (int)response.StatusCode,
                    content);
                return default;
            }

            return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Erro de comunicacao com API do chatbot no path {Path}.", relativePath);
            return default;
        }
    }

    private static string BuildMetadataJson(string? title)
    {
        var payload = new
        {
            bridge = "telegram",
            title = title,
            capturedAtUtc = DateTime.UtcNow
        };

        return JsonSerializer.Serialize(payload);
    }

    private static string BuildMessageMetadataJson(ChatMessageDto message)
    {
        var payload = new
        {
            bridge = "telegram",
            senderDisplayName = message.SenderDisplayName,
            attachments = message.Attachments.Count,
            isOutgoing = message.IsOutgoing
        };

        return JsonSerializer.Serialize(payload);
    }

    private sealed class TelegramChatbotConversationApiResponse
    {
        public Guid Id { get; set; }
    }
}
