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
            intentName: null,
            modelName: null,
            promptTokens: null,
            completionTokens: null,
            totalTokens: null,
            metadataJson: BuildMessageMetadataJson(message),
            lastStep: null,
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
            intentName: null,
            modelName: null,
            promptTokens: null,
            completionTokens: null,
            totalTokens: null,
            metadataJson: BuildMessageMetadataJson(message),
            lastStep: null,
            cancellationToken);
    }

    public async Task<bool> RegisterAssistantMessageAsync(
        string apiToken,
        long chatId,
        ChatMessageDto message,
        TelegramChatbotAssistantReply assistantReply,
        CancellationToken cancellationToken = default)
    {
        return await RegisterMessageAsync(
            apiToken,
            chatId,
            message,
            direction: 2,
            source: "telegram_bridge_assistant_ai",
            intentName: assistantReply.Intent,
            modelName: assistantReply.ModelName,
            promptTokens: assistantReply.PromptTokens,
            completionTokens: assistantReply.CompletionTokens,
            totalTokens: assistantReply.TotalTokens,
            metadataJson: BuildAssistantMessageMetadataJson(message, assistantReply),
            lastStep: assistantReply.NextStep,
            cancellationToken);
    }

    public async Task<TelegramChatbotConversationHistoryDto?> GetConversationHistoryAsync(
        string apiToken,
        Guid conversationId,
        int messageTake,
        int snapshotTake,
        int actionTake,
        CancellationToken cancellationToken = default)
    {
        var normalizedMessageTake = Math.Clamp(messageTake, 1, 200);
        var normalizedSnapshotTake = Math.Clamp(snapshotTake, 1, 50);
        var normalizedActionTake = Math.Clamp(actionTake, 1, 50);

        var path =
            $"/api/telegram-chatbot/conversations/{conversationId:D}/history" +
            $"?messageTake={normalizedMessageTake}&snapshotTake={normalizedSnapshotTake}&actionTake={normalizedActionTake}";

        return await GetAsync<TelegramChatbotConversationHistoryDto>(apiToken, path, cancellationToken);
    }

    public async Task<bool> RegisterContextSnapshotAsync(
        string apiToken,
        Guid conversationId,
        string snapshotType,
        string contextJson,
        string? promptVersion,
        string? modelName,
        int? promptTokens,
        int? completionTokens,
        int? totalTokens,
        string? intentName,
        string? lastStep,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            conversationId,
            snapshotType,
            contextJson,
            promptVersion,
            modelName,
            promptTokens,
            completionTokens,
            totalTokens,
            capturedAtUtc = DateTime.UtcNow,
            intentName,
            lastStep
        };

        var response = await PostAsync<JsonElement>(
            apiToken,
            "/api/telegram-chatbot/context-snapshots",
            payload,
            cancellationToken);

        return response.ValueKind != JsonValueKind.Undefined;
    }

    public async Task<bool> RegisterActionAsync(
        string apiToken,
        Guid conversationId,
        string actionType,
        int status,
        string? intentName,
        string? payloadJson,
        string? resultJson,
        string? errorCode,
        string? errorMessage,
        string? correlationId,
        string? metadataJson,
        string? lastStep,
        int? conversationStatus,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            conversationId,
            actionType,
            status,
            intentName,
            payloadJson,
            resultJson,
            errorCode,
            errorMessage,
            correlationId,
            occurredAtUtc = DateTime.UtcNow,
            metadataJson,
            lastStep,
            conversationStatus
        };

        var response = await PostAsync<JsonElement>(
            apiToken,
            "/api/telegram-chatbot/actions",
            payload,
            cancellationToken);

        return response.ValueKind != JsonValueKind.Undefined;
    }

    public async Task<bool> UpdateConversationStateAsync(
        string apiToken,
        Guid conversationId,
        int? status,
        string? lastIntent,
        string? lastStep,
        string? metadataJson,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            status,
            lastIntent,
            lastStep,
            metadataJson,
            interactionAtUtc = DateTime.UtcNow
        };

        var response = await PatchAsync<JsonElement>(
            apiToken,
            $"/api/telegram-chatbot/conversations/{conversationId:D}/state",
            payload,
            cancellationToken);

        return response.ValueKind != JsonValueKind.Undefined;
    }

    private async Task<bool> RegisterMessageAsync(
        string apiToken,
        long chatId,
        ChatMessageDto message,
        int direction,
        string source,
        string? intentName,
        string? modelName,
        int? promptTokens,
        int? completionTokens,
        int? totalTokens,
        string metadataJson,
        string? lastStep,
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
            intentName,
            modelName,
            promptTokens,
            completionTokens,
            totalTokens,
            sentAtUtc = message.SentAtUtc.UtcDateTime,
            metadataJson,
            lastStep
        };

        var response = await PostAsync<JsonElement>(
            apiToken,
            "/api/telegram-chatbot/messages",
            payload,
            cancellationToken);

        return response.ValueKind != JsonValueKind.Undefined;
    }

    private async Task<TResponse?> GetAsync<TResponse>(
        string apiToken,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var endpoint = TryBuildEndpoint(relativePath);
        if (endpoint is null)
        {
            return default;
        }

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

        try
        {
            using var response = await client.GetAsync(endpoint, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Falha ao consultar chatbot na API. Path: {Path}. Status: {StatusCode}. Body: {Body}",
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

    private async Task<TResponse?> PostAsync<TResponse>(
        string apiToken,
        string relativePath,
        object payload,
        CancellationToken cancellationToken)
    {
        var endpoint = TryBuildEndpoint(relativePath);
        if (endpoint is null)
        {
            return default;
        }

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

        try
        {
            using var response = await client.PostAsJsonAsync(endpoint, payload, cancellationToken);

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

    private async Task<TResponse?> PatchAsync<TResponse>(
        string apiToken,
        string relativePath,
        object payload,
        CancellationToken cancellationToken)
    {
        var endpoint = TryBuildEndpoint(relativePath);
        if (endpoint is null)
        {
            return default;
        }

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch, endpoint)
            {
                Content = JsonContent.Create(payload)
            };

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Falha ao sincronizar estado chatbot na API. Path: {Path}. Status: {StatusCode}. Body: {Body}",
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

    private string? TryBuildEndpoint(string relativePath)
    {
        var apiBaseUrl = _configuration["ApiBaseUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            _logger.LogWarning("ApiBaseUrl nao configurada para TelegramChatbotApiClient.");
            return null;
        }

        return $"{apiBaseUrl.TrimEnd('/')}{relativePath}";
    }

    private static string BuildMetadataJson(string? title)
    {
        var payload = new
        {
            bridge = "telegram",
            title,
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

    private static string BuildAssistantMessageMetadataJson(
        ChatMessageDto message,
        TelegramChatbotAssistantReply assistantReply)
    {
        var payload = new
        {
            bridge = "telegram",
            senderDisplayName = message.SenderDisplayName,
            attachments = message.Attachments.Count,
            isOutgoing = message.IsOutgoing,
            intent = assistantReply.Intent,
            nextStep = assistantReply.NextStep,
            confidence = assistantReply.Confidence,
            entities = assistantReply.EntitiesJson,
            usedFallback = assistantReply.UsedFallback,
            usedCache = assistantReply.UsedCache,
            promptVersion = assistantReply.PromptVersion,
            correlationId = assistantReply.CorrelationId,
            latencyMilliseconds = assistantReply.LatencyMilliseconds
        };

        return JsonSerializer.Serialize(payload);
    }

    private sealed class TelegramChatbotConversationApiResponse
    {
        public Guid Id { get; set; }
    }
}
