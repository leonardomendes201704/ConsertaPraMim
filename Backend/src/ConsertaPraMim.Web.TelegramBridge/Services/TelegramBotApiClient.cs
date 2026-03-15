using System.Net.Http.Json;
using System.Text.Json;
using ConsertaPraMim.Web.TelegramBridge.Models;
using ConsertaPraMim.Web.TelegramBridge.Options;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public sealed class TelegramBotApiClient : ITelegramBotApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<TelegramBotApiClient> _logger;
    private readonly string _botToken;

    public TelegramBotApiClient(
        IHttpClientFactory httpClientFactory,
        IOptions<TelegramBridgeOptions> options,
        ILogger<TelegramBotApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("TelegramBotApi");
        _logger = logger;
        _botToken = options.Value.BotToken?.Trim() ?? string.Empty;

        if (!IsConfigured)
        {
            _logger.LogWarning("TelegramBridge.BotToken nao configurado. O painel sobe, mas sem integracao Telegram.");
        }
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_botToken);

    public async Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(
        long offset,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var payload = new
        {
            offset,
            timeout = Math.Clamp(timeoutSeconds, 1, 50),
            allowed_updates = new[] { "message" }
        };

        var result = await PostForResultAsync<IReadOnlyList<TelegramUpdate>>("getUpdates", payload, cancellationToken);
        return result ?? [];
    }

    public async Task SetWebhookAsync(
        string webhookUrl,
        string secretToken,
        bool dropPendingUpdates,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            throw new InvalidOperationException("WebhookUrl invalida para registro do Telegram.");
        }

        if (string.IsNullOrWhiteSpace(secretToken))
        {
            throw new InvalidOperationException("WebhookSecretToken invalido para registro do Telegram.");
        }

        await PostForResultAsync<JsonElement>(
            "setWebhook",
            new
            {
                url = webhookUrl.Trim(),
                secret_token = secretToken.Trim(),
                drop_pending_updates = dropPendingUpdates,
                allowed_updates = new[] { "message" }
            },
            cancellationToken);
    }

    public async Task DeleteWebhookAsync(bool dropPendingUpdates, CancellationToken cancellationToken)
    {
        EnsureConfigured();

        await PostForResultAsync<JsonElement>(
            "deleteWebhook",
            new
            {
                drop_pending_updates = dropPendingUpdates
            },
            cancellationToken);
    }

    public async Task<TelegramWebhookInfo?> GetWebhookInfoAsync(CancellationToken cancellationToken)
    {
        EnsureConfigured();

        return await PostForResultAsync<TelegramWebhookInfo>(
            "getWebhookInfo",
            new { },
            cancellationToken);
    }

    public async Task SendMessageAsync(
        long chatId,
        string? text,
        IReadOnlyList<StoredLocalFile> attachments,
        CancellationToken cancellationToken,
        TelegramMessageSendOptions? options = null)
    {
        EnsureConfigured();

        var safeText = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        var replyMarkup = BuildReplyMarkup(options);

        if (attachments.Count == 0)
        {
            if (string.IsNullOrWhiteSpace(safeText))
            {
                throw new InvalidOperationException("Informe uma mensagem ou adicione anexos.");
            }

            await PostForResultAsync<JsonElement>(
                "sendMessage",
                BuildSendMessagePayload(chatId, safeText, replyMarkup),
                cancellationToken);

            return;
        }

        var captionToUse = safeText;

        foreach (var attachment in attachments)
        {
            var method = attachment.MediaKind switch
            {
                "image" => "sendPhoto",
                "video" => "sendVideo",
                _ => "sendDocument"
            };

            var fileFieldName = attachment.MediaKind switch
            {
                "image" => "photo",
                "video" => "video",
                _ => "document"
            };

            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(chatId.ToString()), "chat_id");

            if (!string.IsNullOrWhiteSpace(captionToUse))
            {
                content.Add(new StringContent(captionToUse), "caption");
            }

            if (replyMarkup is not null)
            {
                content.Add(new StringContent(JsonSerializer.Serialize(replyMarkup, JsonOptions)), "reply_markup");
            }

            await using var stream = File.OpenRead(attachment.PhysicalPath);
            var streamContent = new StreamContent(stream);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(attachment.ContentType);
            content.Add(streamContent, fileFieldName, attachment.FileName);

            await PostMultipartAsync(method, content, cancellationToken);
            captionToUse = null;
        }

        if (!string.IsNullOrWhiteSpace(captionToUse))
        {
            await PostForResultAsync<JsonElement>(
                "sendMessage",
                BuildSendMessagePayload(chatId, captionToUse, replyMarkup),
                cancellationToken);
        }
    }

    public async Task<string?> GetFilePathAsync(string fileId, CancellationToken cancellationToken)
    {
        EnsureConfigured();

        if (string.IsNullOrWhiteSpace(fileId))
        {
            return null;
        }

        var result = await PostForResultAsync<TelegramFileReference>(
            "getFile",
            new { file_id = fileId },
            cancellationToken);

        return result?.FilePath;
    }

    public async Task DownloadFileAsync(string filePath, Stream destination, CancellationToken cancellationToken)
    {
        EnsureConfigured();

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("file_path invalido para download.");
        }

        using var response = await _httpClient.GetAsync(
            BuildFileUrl(filePath),
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Falha no download de arquivo do Telegram. Status {(int)response.StatusCode}. Body: {responseBody}");
        }

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await contentStream.CopyToAsync(destination, cancellationToken);
    }

    private async Task<T?> PostForResultAsync<T>(string method, object payload, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(BuildApiUrl(method), payload, JsonOptions, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Falha HTTP no metodo Telegram {method}. Status {(int)response.StatusCode}. Body: {body}");
        }

        var envelope = JsonSerializer.Deserialize<TelegramApiEnvelope<T>>(body, JsonOptions)
            ?? throw new InvalidOperationException($"Resposta Telegram invalida em {method}.");

        if (!envelope.Ok)
        {
            throw new InvalidOperationException(
                $"Telegram retornou erro em {method}: {envelope.Description ?? "sem descricao"} (code: {envelope.ErrorCode?.ToString() ?? "n/a"}).");
        }

        return envelope.Result;
    }

    private async Task PostMultipartAsync(string method, MultipartFormDataContent content, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildApiUrl(method))
        {
            Content = content
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Falha HTTP no metodo Telegram {method}. Status {(int)response.StatusCode}. Body: {body}");
        }

        var envelope = JsonSerializer.Deserialize<TelegramApiEnvelope<JsonElement>>(body, JsonOptions)
            ?? throw new InvalidOperationException($"Resposta Telegram invalida em {method}.");

        if (!envelope.Ok)
        {
            throw new InvalidOperationException(
                $"Telegram retornou erro em {method}: {envelope.Description ?? "sem descricao"} (code: {envelope.ErrorCode?.ToString() ?? "n/a"}).");
        }
    }

    private string BuildApiUrl(string method) => $"https://api.telegram.org/bot{_botToken}/{method}";

    private string BuildFileUrl(string filePath) => $"https://api.telegram.org/file/bot{_botToken}/{filePath}";

    private static object BuildSendMessagePayload(long chatId, string? text, object? replyMarkup)
    {
        if (replyMarkup is null)
        {
            return new
            {
                chat_id = chatId,
                text
            };
        }

        return new
        {
            chat_id = chatId,
            text,
            reply_markup = replyMarkup
        };
    }

    private static object? BuildReplyMarkup(TelegramMessageSendOptions? options)
    {
        if (options is null)
        {
            return null;
        }

        if (options.RemoveReplyKeyboard)
        {
            return new
            {
                remove_keyboard = true
            };
        }

        if (options.RequestContactButton)
        {
            var label = string.IsNullOrWhiteSpace(options.ContactButtonLabel)
                ? "Compartilhar telefone"
                : options.ContactButtonLabel.Trim();

            return new
            {
                keyboard = new object[]
                {
                    new object[]
                    {
                        new
                        {
                            text = label,
                            request_contact = true
                        }
                    }
                },
                resize_keyboard = true,
                one_time_keyboard = true
            };
        }

        return null;
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("TelegramBridge.BotToken nao configurado.");
        }
    }
}
