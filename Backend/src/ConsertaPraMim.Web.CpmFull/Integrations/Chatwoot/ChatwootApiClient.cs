using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Chatwoot;

public sealed class ChatwootApiClient : IChatwootApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(5);

    private readonly HttpClient _httpClient;
    private readonly ChatwootOptions _options;
    private readonly ILogger<ChatwootApiClient> _logger;

    public ChatwootApiClient(
        HttpClient httpClient,
        IOptions<ChatwootOptions> options,
        ILogger<ChatwootApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ChatwootConnectionCheckResult> CheckConnectionAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendAsync<ChatwootInboxListResponse>(
            HttpMethod.Get,
            $"api/v1/accounts/{_options.AccountId}/inboxes",
            body: null,
            cancellationToken);

        var inboxes = response.Payload
            .Select(inbox => new ChatwootInboxSummary
            {
                Id = inbox.Id,
                Name = inbox.Name ?? string.Empty,
                ChannelType = inbox.ChannelType ?? string.Empty
            })
            .ToList();

        return new ChatwootConnectionCheckResult
        {
            IsReachable = true,
            Inboxes = inboxes
        };
    }

    public async Task<ChatwootContactSummary?> GetContactAsync(long contactId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await SendAsync<ChatwootContactPayloadEnvelope>(
                HttpMethod.Get,
                $"api/v1/accounts/{_options.AccountId}/contacts/{contactId}",
                body: null,
                cancellationToken);

            return MapContact(response.Payload);
        }
        catch (ChatwootApiException ex) when (ex.StatusCode == 404)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<ChatwootContactSummary>> SearchContactsAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var encodedQuery = Uri.EscapeDataString(query.Trim());
        var response = await SendAsync<ChatwootContactSearchResponse>(
            HttpMethod.Get,
            $"api/v1/accounts/{_options.AccountId}/contacts/search?q={encodedQuery}",
            body: null,
            cancellationToken);

        return response.Payload
            .Select(MapContact)
            .ToList();
    }

    public async Task<ChatwootContactSummary> CreateContactAsync(ChatwootUpsertContactRequest request, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync<ChatwootCreateContactResponse>(
            HttpMethod.Post,
            $"api/v1/accounts/{_options.AccountId}/contacts",
            new
            {
                inbox_id = request.InboxId,
                name = request.Name,
                email = NullIfWhiteSpace(request.Email),
                phone_number = NullIfWhiteSpace(request.PhoneNumber),
                identifier = request.Identifier,
                additional_attributes = request.AdditionalAttributes,
                custom_attributes = request.CustomAttributes
            },
            cancellationToken);

        var contact = MapContact(response.Payload.Contact);
        if (response.Payload.ContactInbox is null)
        {
            return contact;
        }

        var mappedInbox = MapContactInbox(response.Payload.ContactInbox);
        var contactInboxes = contact.ContactInboxes
            .Where(item => item.InboxId != mappedInbox.InboxId)
            .Concat([mappedInbox])
            .ToList();

        return new ChatwootContactSummary
        {
            Id = contact.Id,
            Name = contact.Name,
            Email = contact.Email,
            PhoneNumber = contact.PhoneNumber,
            Identifier = contact.Identifier,
            ContactInboxes = contactInboxes
        };
    }

    public async Task<ChatwootContactSummary> UpdateContactAsync(long contactId, ChatwootUpsertContactRequest request, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync<ChatwootContactPayloadEnvelope>(
            HttpMethod.Patch,
            $"api/v1/accounts/{_options.AccountId}/contacts/{contactId}",
            new
            {
                name = request.Name,
                email = NullIfWhiteSpace(request.Email),
                phone_number = NullIfWhiteSpace(request.PhoneNumber),
                identifier = request.Identifier,
                additional_attributes = request.AdditionalAttributes,
                custom_attributes = request.CustomAttributes
            },
            cancellationToken);

        return MapContact(response.Payload);
    }

    public async Task<ChatwootContactInboxSummary> CreateContactInboxAsync(long contactId, ChatwootCreateContactInboxRequest request, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync<ChatwootContactInboxResponse>(
            HttpMethod.Post,
            $"api/v1/accounts/{_options.AccountId}/contacts/{contactId}/contact_inboxes",
            new
            {
                inbox_id = request.InboxId,
                source_id = request.SourceId
            },
            cancellationToken);

        return MapContactInbox(response);
    }

    public async Task<IReadOnlyList<string>> ListContactLabelsAsync(long contactId, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync<ChatwootContactLabelsResponse>(
            HttpMethod.Get,
            $"api/v1/accounts/{_options.AccountId}/contacts/{contactId}/labels",
            body: null,
            cancellationToken);

        return response.Payload
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Select(label => label.Trim())
            .ToList();
    }

    public async Task<IReadOnlyList<string>> ReplaceContactLabelsAsync(long contactId, IReadOnlyList<string> labels, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync<ChatwootContactLabelsResponse>(
            HttpMethod.Post,
            $"api/v1/accounts/{_options.AccountId}/contacts/{contactId}/labels",
            new
            {
                labels
            },
            cancellationToken);

        return response.Payload
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Select(label => label.Trim())
            .ToList();
    }

    public async Task<ChatwootConversationSummary> CreateConversationAsync(ChatwootCreateConversationRequest request, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync<ChatwootConversationResponse>(
            HttpMethod.Post,
            $"api/v1/accounts/{_options.AccountId}/conversations",
            new
            {
                source_id = request.SourceId,
                inbox_id = request.InboxId,
                contact_id = request.ContactId,
                status = request.Status
            },
            cancellationToken);

        return new ChatwootConversationSummary
        {
            Id = response.Id,
            InboxId = response.InboxId,
            Status = response.Status ?? string.Empty
        };
    }

    public async Task<ChatwootMessageSummary> CreateMessageAsync(long conversationId, ChatwootCreateMessageRequest request, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync<ChatwootMessageResponse>(
            HttpMethod.Post,
            $"api/v1/accounts/{_options.AccountId}/conversations/{conversationId}/messages",
            new
            {
                content = request.Content,
                message_type = request.MessageType,
                @private = request.Private
            },
            cancellationToken);

        return new ChatwootMessageSummary
        {
            Id = response.Id,
            Private = response.Private
        };
    }

    public async Task<IReadOnlyList<string>> ListConversationLabelsAsync(long conversationId, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync<ChatwootConversationLabelsResponse>(
            HttpMethod.Get,
            $"api/v1/accounts/{_options.AccountId}/conversations/{conversationId}/labels",
            body: null,
            cancellationToken);

        return response.Payload
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Select(label => label.Trim())
            .ToList();
    }

    public async Task<IReadOnlyList<string>> ReplaceConversationLabelsAsync(long conversationId, IReadOnlyList<string> labels, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync<ChatwootConversationLabelsResponse>(
            HttpMethod.Post,
            $"api/v1/accounts/{_options.AccountId}/conversations/{conversationId}/labels",
            new
            {
                labels = labels
            },
            cancellationToken);

        return response.Payload
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Select(label => label.Trim())
            .ToList();
    }

    public async Task UpdateConversationCustomAttributesAsync(long conversationId, IReadOnlyDictionary<string, object?> customAttributes, CancellationToken cancellationToken = default)
    {
        await SendAsync<ChatwootConversationCustomAttributesResponse>(
            HttpMethod.Post,
            $"api/v1/accounts/{_options.AccountId}/conversations/{conversationId}/custom_attributes",
            new
            {
                custom_attributes = customAttributes
            },
            cancellationToken);
    }

    public async Task<string> UpdateConversationStatusAsync(long conversationId, string status, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync<ChatwootConversationStatusResponse>(
            HttpMethod.Post,
            $"api/v1/accounts/{_options.AccountId}/conversations/{conversationId}/toggle_status",
            new
            {
                status
            },
            cancellationToken);

        return response.Payload?.CurrentStatus ?? string.Empty;
    }

    private async Task<TResponse> SendAsync<TResponse>(
        HttpMethod method,
        string relativePath,
        object? body,
        CancellationToken cancellationToken)
    {
        var attempts = Math.Max(1, _options.MaxRetryAttempts);

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            using var request = new HttpRequestMessage(method, relativePath);
            request.Headers.TryAddWithoutValidation("api_access_token", _options.ApiAccessToken);

            if (body is not null)
            {
                request.Content = JsonContent.Create(body, options: JsonOptions);
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.GetRequestTimeout());

            try
            {
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);

                if (IsTransientStatusCode(response.StatusCode) && attempt < attempts)
                {
                    await DelayBeforeRetryAsync(attempt, response.StatusCode, cancellationToken);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new ChatwootApiException(
                        $"Chatwoot retornou erro HTTP {(int)response.StatusCode} ao acessar '{relativePath}'. Resposta: {responseBody}",
                        (int)response.StatusCode);
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var payload = await JsonSerializer.DeserializeAsync<TResponse>(stream, JsonOptions, cancellationToken);
                return payload ?? throw new ChatwootApiException("Chatwoot retornou payload vazio.");
            }
            catch (Exception ex) when (IsTransientException(ex) && attempt < attempts)
            {
                _logger.LogWarning(ex, "Falha transiente ao acessar Chatwoot na tentativa {Attempt}/{MaxAttempts}.", attempt, attempts);
                await DelayBeforeRetryAsync(attempt, null, cancellationToken);
            }
        }

        throw new ChatwootApiException("Falha ao acessar Chatwoot apos esgotar as tentativas configuradas.");
    }

    private async Task DelayBeforeRetryAsync(int attempt, HttpStatusCode? statusCode, CancellationToken cancellationToken)
    {
        var delayMs = _options.RetryBaseDelayMs * Math.Pow(2, attempt - 1);
        var delay = TimeSpan.FromMilliseconds(Math.Min(delayMs, MaxRetryDelay.TotalMilliseconds));

        if (statusCode.HasValue)
        {
            _logger.LogWarning(
                "Chatwoot respondeu com status transiente {StatusCode}. Nova tentativa em {DelayMs} ms.",
                (int)statusCode.Value,
                delay.TotalMilliseconds);
        }

        await Task.Delay(delay, cancellationToken);
    }

    private static ChatwootContactSummary MapContact(ChatwootContactResponse response) =>
        new()
        {
            Id = response.Id,
            Name = response.Name ?? string.Empty,
            Email = response.Email ?? string.Empty,
            PhoneNumber = response.PhoneNumber ?? string.Empty,
            Identifier = response.Identifier ?? string.Empty,
            ContactInboxes = response.ContactInboxes
                .Select(MapContactInbox)
                .ToList()
        };

    private static ChatwootContactInboxSummary MapContactInbox(ChatwootContactInboxResponse response) =>
        new()
        {
            InboxId = response.Inbox?.Id ?? 0,
            InboxName = response.Inbox?.Name ?? string.Empty,
            SourceId = response.SourceId ?? string.Empty
        };

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsTransientException(Exception exception) =>
        exception is HttpRequestException or TaskCanceledException;

    private static bool IsTransientStatusCode(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.RequestTimeout ||
        statusCode == HttpStatusCode.TooManyRequests ||
        (int)statusCode >= 500;

    private sealed class ChatwootInboxListResponse
    {
        public List<ChatwootInboxItem> Payload { get; init; } = [];
    }

    private sealed class ChatwootInboxItem
    {
        public long Id { get; init; }
        public string? Name { get; init; }

        [JsonPropertyName("channel_type")]
        public string? ChannelType { get; init; }
    }

    private sealed class ChatwootContactSearchResponse
    {
        public List<ChatwootContactResponse> Payload { get; init; } = [];
    }

    private sealed class ChatwootCreateContactResponse
    {
        public required ChatwootCreateContactPayload Payload { get; init; }
    }

    private sealed class ChatwootCreateContactPayload
    {
        public required ChatwootContactResponse Contact { get; init; }

        [JsonPropertyName("contact_inbox")]
        public ChatwootContactInboxResponse? ContactInbox { get; init; }
    }

    private sealed class ChatwootContactPayloadEnvelope
    {
        public required ChatwootContactResponse Payload { get; init; }
    }

    private sealed class ChatwootContactResponse
    {
        public long Id { get; init; }
        public string? Name { get; init; }
        public string? Email { get; init; }
        public string? Identifier { get; init; }

        [JsonPropertyName("phone_number")]
        public string? PhoneNumber { get; init; }

        [JsonPropertyName("contact_inboxes")]
        public List<ChatwootContactInboxResponse> ContactInboxes { get; init; } = [];
    }

    private sealed class ChatwootContactInboxResponse
    {
        [JsonPropertyName("source_id")]
        public string? SourceId { get; init; }

        public ChatwootInboxReference? Inbox { get; init; }
    }

    private sealed class ChatwootInboxReference
    {
        public long Id { get; init; }
        public string? Name { get; init; }
    }

    private sealed class ChatwootConversationResponse
    {
        public long Id { get; init; }

        [JsonPropertyName("inbox_id")]
        public long InboxId { get; init; }

        public string? Status { get; init; }
    }

    private sealed class ChatwootMessageResponse
    {
        public long Id { get; init; }

        [JsonPropertyName("private")]
        public bool Private { get; init; }
    }

    private sealed class ChatwootConversationLabelsResponse
    {
        public List<string> Payload { get; init; } = [];
    }

    private sealed class ChatwootContactLabelsResponse
    {
        public List<string> Payload { get; init; } = [];
    }

    private sealed class ChatwootConversationCustomAttributesResponse
    {
        [JsonPropertyName("custom_attributes")]
        public Dictionary<string, JsonElement> CustomAttributes { get; init; } = [];
    }

    private sealed class ChatwootConversationStatusResponse
    {
        public ChatwootConversationStatusPayload? Payload { get; init; }
    }

    private sealed class ChatwootConversationStatusPayload
    {
        [JsonPropertyName("current_status")]
        public string? CurrentStatus { get; init; }
    }
}
