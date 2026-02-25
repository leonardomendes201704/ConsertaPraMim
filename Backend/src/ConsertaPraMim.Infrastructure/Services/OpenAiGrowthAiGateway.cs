using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConsertaPraMim.Infrastructure.Services;

public class OpenAiGrowthAiGateway : IAdminGrowthAiGateway
{
    private static readonly Uri ResponsesUri = new("https://api.openai.com/v1/responses");
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiGrowthAiGateway> _logger;

    public OpenAiGrowthAiGateway(
        HttpClient httpClient,
        ILogger<OpenAiGrowthAiGateway> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<AdminGrowthAiGatewayResult> GenerateAnalysisAsync(
        AdminGrowthAiGatewayRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return new AdminGrowthAiGatewayResult(
                Success: false,
                ErrorCode: "openai_api_key_missing",
                ErrorMessage: "OpenAI API key nao configurada.");
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, ResponsesUri);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey.Trim());
        message.Content = JsonContent.Create(new
        {
            model = request.Model,
            temperature = decimal.ToDouble(request.Temperature),
            max_output_tokens = request.MaxOutputTokens,
            input = new object[]
            {
                new
                {
                    role = "system",
                    content = request.SystemPrompt
                },
                new
                {
                    role = "user",
                    content = request.UserPrompt
                }
            }
        });

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha de rede ao chamar OpenAI Responses API.");
            return new AdminGrowthAiGatewayResult(
                Success: false,
                ErrorCode: "openai_http_error",
                ErrorMessage: "Falha de rede ao acessar OpenAI.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        if (!response.IsSuccessStatusCode)
        {
            var (errorCode, errorMessage) = ParseError(root);
            return new AdminGrowthAiGatewayResult(
                Success: false,
                ErrorCode: string.IsNullOrWhiteSpace(errorCode) ? "openai_request_failed" : errorCode,
                ErrorMessage: string.IsNullOrWhiteSpace(errorMessage)
                    ? $"Falha na OpenAI API (HTTP {(int)response.StatusCode})."
                    : errorMessage);
        }

        var outputText = ParseOutputText(root);
        if (string.IsNullOrWhiteSpace(outputText))
        {
            return new AdminGrowthAiGatewayResult(
                Success: false,
                ErrorCode: "openai_empty_output",
                ErrorMessage: "OpenAI retornou resposta sem conteudo textual.");
        }

        var (inputTokens, outputTokens, totalTokens) = ParseUsage(root);
        return new AdminGrowthAiGatewayResult(
            Success: true,
            OutputText: outputText.Trim(),
            InputTokens: inputTokens,
            OutputTokens: outputTokens,
            TotalTokens: totalTokens);
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

        if (chunks.Count == 0)
        {
            return null;
        }

        return string.Join(Environment.NewLine, chunks);
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
