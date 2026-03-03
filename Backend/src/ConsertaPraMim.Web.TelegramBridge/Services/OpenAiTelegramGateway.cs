using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ConsertaPraMim.Web.TelegramBridge.Models;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public sealed class OpenAiTelegramGateway : ITelegramAiGateway
{
    private static readonly Uri ResponsesUri = new("https://api.openai.com/v1/responses");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OpenAiTelegramGateway> _logger;

    public OpenAiTelegramGateway(
        IHttpClientFactory httpClientFactory,
        ILogger<OpenAiTelegramGateway> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<TelegramAiGatewayResult> GenerateReplyAsync(
        TelegramAiGatewayRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return new TelegramAiGatewayResult(
                Success: false,
                ErrorCode: "openai_api_key_missing",
                ErrorMessage: "OpenAI API key nao configurada.");
        }

        if (request.Messages.Count == 0)
        {
            return new TelegramAiGatewayResult(
                Success: false,
                ErrorCode: "openai_prompt_missing",
                ErrorMessage: "Prompt da IA nao informado.");
        }

        var maxRetries = Math.Clamp(request.MaxRetries, 0, 5);
        var timeoutSeconds = Math.Clamp(request.RequestTimeoutSeconds, 5, 90);
        var client = _httpClientFactory.CreateClient();
        var attempt = 0;
        Exception? lastException = null;

        while (attempt <= maxRetries && !cancellationToken.IsCancellationRequested)
        {
            attempt++;
            var startedAt = Stopwatch.StartNew();

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, ResponsesUri);
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey.Trim());
                requestMessage.Content = JsonContent.Create(new
                {
                    model = request.Model,
                    temperature = decimal.ToDouble(request.Temperature),
                    max_output_tokens = request.MaxOutputTokens,
                    input = request.Messages.Select(item => new
                    {
                        role = item.Role,
                        content = item.Content
                    }).ToArray()
                });

                using var response = await client.SendAsync(requestMessage, linkedCts.Token);
                await using var stream = await response.Content.ReadAsStreamAsync(linkedCts.Token);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: linkedCts.Token);
                var root = document.RootElement;

                if (response.IsSuccessStatusCode)
                {
                    var outputText = ParseOutputText(root);
                    if (string.IsNullOrWhiteSpace(outputText))
                    {
                        return new TelegramAiGatewayResult(
                            Success: false,
                            ErrorCode: "openai_empty_output",
                            ErrorMessage: "OpenAI retornou resposta sem conteudo textual.",
                            AttemptCount: attempt,
                            LatencyMilliseconds: startedAt.ElapsedMilliseconds);
                    }

                    var (inputTokens, outputTokens, totalTokens) = ParseUsage(root);
                    return new TelegramAiGatewayResult(
                        Success: true,
                        OutputText: outputText.Trim(),
                        InputTokens: inputTokens,
                        OutputTokens: outputTokens,
                        TotalTokens: totalTokens,
                        AttemptCount: attempt,
                        LatencyMilliseconds: startedAt.ElapsedMilliseconds);
                }

                var (errorCode, errorMessage) = ParseError(root);
                var isTransient = IsTransientStatusCode(response.StatusCode);
                _logger.LogWarning(
                    "OpenAI retornou erro HTTP {StatusCode} na tentativa {Attempt}/{MaxAttempts}. Codigo: {ErrorCode}. Mensagem: {ErrorMessage}",
                    (int)response.StatusCode,
                    attempt,
                    maxRetries + 1,
                    errorCode,
                    errorMessage);

                if (!isTransient || attempt > maxRetries)
                {
                    return new TelegramAiGatewayResult(
                        Success: false,
                        ErrorCode: string.IsNullOrWhiteSpace(errorCode) ? "openai_request_failed" : errorCode,
                        ErrorMessage: string.IsNullOrWhiteSpace(errorMessage)
                            ? $"Falha na OpenAI API (HTTP {(int)response.StatusCode})."
                            : errorMessage,
                        AttemptCount: attempt,
                        LatencyMilliseconds: startedAt.ElapsedMilliseconds);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                lastException = new TimeoutException("Tempo limite excedido ao chamar OpenAI.");
                _logger.LogWarning(
                    "Timeout na chamada OpenAI na tentativa {Attempt}/{MaxAttempts}.",
                    attempt,
                    maxRetries + 1);

                if (attempt > maxRetries)
                {
                    break;
                }
            }
            catch (HttpRequestException exception)
            {
                lastException = exception;
                _logger.LogWarning(
                    exception,
                    "Falha de rede ao chamar OpenAI na tentativa {Attempt}/{MaxAttempts}.",
                    attempt,
                    maxRetries + 1);

                if (attempt > maxRetries)
                {
                    break;
                }
            }

            if (attempt <= maxRetries)
            {
                var retryDelay = TimeSpan.FromMilliseconds(250 * attempt);
                await Task.Delay(retryDelay, cancellationToken);
            }
        }

        _logger.LogError(lastException, "Nao foi possivel obter resposta da OpenAI apos tentativas.");
        return new TelegramAiGatewayResult(
            Success: false,
            ErrorCode: "openai_unavailable",
            ErrorMessage: "Nao foi possivel acessar a OpenAI no momento.",
            AttemptCount: Math.Clamp(maxRetries + 1, 1, 6));
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.RequestTimeout
            || statusCode == (HttpStatusCode)429
            || statusCode == HttpStatusCode.InternalServerError
            || statusCode == HttpStatusCode.BadGateway
            || statusCode == HttpStatusCode.ServiceUnavailable
            || statusCode == HttpStatusCode.GatewayTimeout;
    }

    private static (string? ErrorCode, string? ErrorMessage) ParseError(JsonElement root)
    {
        if (!root.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object)
        {
            return (null, null);
        }

        string? code = null;
        string? message = null;

        if (error.TryGetProperty("code", out var codeElement) && codeElement.ValueKind == JsonValueKind.String)
        {
            code = codeElement.GetString();
        }

        if (error.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.String)
        {
            message = messageElement.GetString();
        }

        return (code, message);
    }

    private static string? ParseOutputText(JsonElement root)
    {
        if (root.TryGetProperty("output_text", out var outputTextElement) &&
            outputTextElement.ValueKind == JsonValueKind.String)
        {
            return outputTextElement.GetString();
        }

        if (!root.TryGetProperty("output", out var outputElement) ||
            outputElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var chunks = new List<string>();
        foreach (var outputItem in outputElement.EnumerateArray())
        {
            if (!outputItem.TryGetProperty("content", out var contentElement) ||
                contentElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var content in contentElement.EnumerateArray())
            {
                if (!content.TryGetProperty("text", out var textElement) ||
                    textElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var text = textElement.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    chunks.Add(text.Trim());
                }
            }
        }

        return chunks.Count == 0
            ? null
            : string.Join(Environment.NewLine, chunks);
    }

    private static (int? InputTokens, int? OutputTokens, int? TotalTokens) ParseUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
        {
            return (null, null, null);
        }

        int? inputTokens = null;
        int? outputTokens = null;
        int? totalTokens = null;

        if (usage.TryGetProperty("input_tokens", out var inputElement) && inputElement.TryGetInt32(out var inputValue))
        {
            inputTokens = inputValue;
        }

        if (usage.TryGetProperty("output_tokens", out var outputElement) && outputElement.TryGetInt32(out var outputValue))
        {
            outputTokens = outputValue;
        }

        if (usage.TryGetProperty("total_tokens", out var totalElement) && totalElement.TryGetInt32(out var totalValue))
        {
            totalTokens = totalValue;
        }

        return (inputTokens, outputTokens, totalTokens);
    }
}
