using System.Net;
using System.Text.Json;
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

    private async Task<TResponse> SendAsync<TResponse>(
        HttpMethod method,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var attempts = Math.Max(1, _options.MaxRetryAttempts);

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            using var request = new HttpRequestMessage(method, relativePath);
            request.Headers.TryAddWithoutValidation("api_access_token", _options.ApiAccessToken);

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
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new ChatwootApiException(
                        $"Chatwoot retornou erro HTTP {(int)response.StatusCode} ao acessar '{relativePath}'. Resposta: {body}",
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
        public string? ChannelType { get; init; }
    }
}
