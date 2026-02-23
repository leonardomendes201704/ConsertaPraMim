using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ConsertaPraMim.LoadTest.Wpf.Services;

public sealed class LoadTestAiAnalyzer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly Action<string> _logger;

    public LoadTestAiAnalyzer(Action<string>? logger = null)
    {
        _logger = logger ?? (_ => { });
    }

    public async Task<LoadTestAiAnalysis?> GenerateAsync(
        LoadTestReport report,
        LoadTestRunOptions options,
        CancellationToken cancellationToken)
    {
        var apiKey = ResolveApiKey(options.OpenAiApiKey);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger("Analise IA ignorada: OpenAI API key nao informada.");
            return null;
        }

        var model = string.IsNullOrWhiteSpace(options.OpenAiModel)
            ? "gpt-4.1-mini"
            : options.OpenAiModel.Trim();
        var endpoint = ResolveEndpoint();
        var prompt = BuildPrompt(report);

        try
        {
            using var httpClient = CreateHttpClient(options.TimeoutSeconds);
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Add("Accept", "application/json");

            var payload = new
            {
                model,
                temperature = 0.2,
                max_tokens = 380,
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = "Voce eh um analista senior de performance. Responda sempre em portugues do Brasil, com tom executivo, claro e objetivo."
                    },
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                }
            };

            request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger($"Analise IA falhou. HTTP {(int)response.StatusCode}: {Truncate(responseBody, 180)}");
                return null;
            }

            var summary = TryExtractSummary(responseBody);
            if (string.IsNullOrWhiteSpace(summary))
            {
                _logger("Analise IA falhou: resposta sem texto.");
                return null;
            }

            return new LoadTestAiAnalysis
            {
                Summary = summary.Trim(),
                GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("o"),
                Provider = "openai",
                Model = model
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger($"Analise IA falhou: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static HttpClient CreateHttpClient(double timeoutSeconds)
    {
        return new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(10, timeoutSeconds))
        };
    }

    private static string ResolveApiKey(string configuredApiKey)
    {
        var envApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(envApiKey))
        {
            return envApiKey.Trim();
        }

        return configuredApiKey?.Trim() ?? string.Empty;
    }

    private static string ResolveEndpoint()
    {
        var envBaseUrl = Environment.GetEnvironmentVariable("OPENAI_BASE_URL");
        if (!string.IsNullOrWhiteSpace(envBaseUrl))
        {
            return $"{envBaseUrl.TrimEnd('/')}/chat/completions";
        }

        return "https://api.openai.com/v1/chat/completions";
    }

    private static string BuildPrompt(LoadTestReport report)
    {
        var topEndpoints = report.TopEndpointsByHits
            .Take(3)
            .Select(x => $"{x.Endpoint}: hits={x.Hits}, p95={x.P95LatencyMs}ms, erro={x.ErrorRatePercent}%");
        var topErrors = report.TopErrors
            .Take(3)
            .Select(x => $"{x.Message} ({x.Count}x)");

        var lines = new List<string>
        {
            "Gere um resumo executivo curto do teste de desempenho.",
            "Formato desejado:",
            "- titulo curto",
            "- 2 a 4 frases objetivas",
            "- 2 bullets com icones (ex: 🚀, ⚠️)",
            "- 1 conclusao final",
            string.Empty,
            $"Cenario: {report.Scenario}",
            $"Base URL: {report.BaseUrl}",
            $"Duracao (s): {report.DurationSeconds}",
            $"Total requests: {report.Summary.TotalRequests}",
            $"Sucesso: {report.Summary.SuccessfulRequests}",
            $"Falhas: {report.Summary.FailedRequests}",
            $"Error rate (%): {report.Summary.ErrorRatePercent}",
            $"RPS medio: {report.Summary.RpsAvg}",
            $"RPS pico: {report.Summary.RpsPeak}",
            $"Latencia p95 (ms): {report.LatencyMs.P95}",
            $"Latencia p99 (ms): {report.LatencyMs.P99}",
            "Top endpoints:",
            string.Join(" | ", topEndpoints),
            "Top erros:",
            string.Join(" | ", topErrors)
        };

        return string.Join(Environment.NewLine, lines);
    }

    private static string TryExtractSummary(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return string.Empty;
        }

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("choices", out var choices) &&
                choices.ValueKind == JsonValueKind.Array &&
                choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var message) &&
                    message.ValueKind == JsonValueKind.Object &&
                    message.TryGetProperty("content", out var content))
                {
                    if (content.ValueKind == JsonValueKind.String)
                    {
                        return content.GetString() ?? string.Empty;
                    }

                    if (content.ValueKind == JsonValueKind.Array)
                    {
                        var parts = new List<string>();
                        foreach (var item in content.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.Object &&
                                item.TryGetProperty("type", out var type) &&
                                type.ValueKind == JsonValueKind.String &&
                                string.Equals(type.GetString(), "text", StringComparison.OrdinalIgnoreCase) &&
                                item.TryGetProperty("text", out var textNode) &&
                                textNode.ValueKind == JsonValueKind.String)
                            {
                                parts.Add(textNode.GetString() ?? string.Empty);
                            }
                        }

                        return string.Join(Environment.NewLine, parts.Where(x => !string.IsNullOrWhiteSpace(x)));
                    }
                }
            }
        }
        catch (JsonException)
        {
            return string.Empty;
        }

        return string.Empty;
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
        {
            return text;
        }

        if (maxLength <= 3)
        {
            return text[..maxLength];
        }

        return text[..(maxLength - 3)] + "...";
    }
}
