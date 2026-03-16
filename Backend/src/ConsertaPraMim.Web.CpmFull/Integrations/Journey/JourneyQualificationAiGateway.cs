using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyQualificationAiGateway : IJourneyQualificationAiGateway
{
    private static readonly Uri ResponsesUri = new("https://api.openai.com/v1/responses");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<JourneyQualificationAiGateway> _logger;

    public JourneyQualificationAiGateway(
        IHttpClientFactory httpClientFactory,
        ILogger<JourneyQualificationAiGateway> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<JourneyQualificationAiResult> ExtractAsync(
        JourneyQualificationAiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return new JourneyQualificationAiResult
            {
                Success = false,
                ErrorCode = "openai_api_key_missing",
                ErrorMessage = "OpenAI API key nao configurada para qualificacao."
            };
        }

        var maxRetries = Math.Clamp(request.MaxRetries, 0, 5);
        var timeoutSeconds = Math.Clamp(request.RequestTimeoutSeconds, 5, 90);
        var client = _httpClientFactory.CreateClient();
        Exception? lastException = null;

        for (var attempt = 1; attempt <= maxRetries + 1 && !cancellationToken.IsCancellationRequested; attempt++)
        {
            var startedAt = Stopwatch.StartNew();

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                using var message = new HttpRequestMessage(HttpMethod.Post, ResponsesUri);
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey.Trim());
                message.Content = JsonContent.Create(new
                {
                    model = request.Model,
                    temperature = 0.1,
                    max_output_tokens = 500,
                    input = new[]
                    {
                        new
                        {
                            role = "system",
                            content = BuildSystemPrompt()
                        },
                        new
                        {
                            role = "user",
                            content = BuildUserPrompt(request.Input)
                        }
                    }
                });

                using var response = await client.SendAsync(message, linkedCts.Token);
                await using var stream = await response.Content.ReadAsStreamAsync(linkedCts.Token);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: linkedCts.Token);
                var root = document.RootElement;

                if (response.IsSuccessStatusCode)
                {
                    var output = ParseOutputText(root);
                    if (string.IsNullOrWhiteSpace(output))
                    {
                        return new JourneyQualificationAiResult
                        {
                            Success = false,
                            ErrorCode = "openai_empty_output",
                            ErrorMessage = "OpenAI retornou resposta vazia para qualificacao."
                        };
                    }

                    var payload = ParsePayload(output);
                    return new JourneyQualificationAiResult
                    {
                        Success = true,
                        Payload = payload
                    };
                }

                var (errorCode, errorMessage) = ParseError(root);
                _logger.LogWarning(
                    "OpenAI retornou erro HTTP {StatusCode} na qualificacao da jornada. Attempt={Attempt}. ErrorCode={ErrorCode}. ErrorMessage={ErrorMessage}. LatencyMs={LatencyMs}",
                    (int)response.StatusCode,
                    attempt,
                    errorCode,
                    errorMessage,
                    startedAt.ElapsedMilliseconds);

                if (!IsTransientStatusCode(response.StatusCode) || attempt > maxRetries)
                {
                    return new JourneyQualificationAiResult
                    {
                        Success = false,
                        ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? "openai_request_failed" : errorCode,
                        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage)
                            ? $"Falha na OpenAI API (HTTP {(int)response.StatusCode})."
                            : errorMessage
                    };
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                lastException = new TimeoutException("Tempo limite excedido ao chamar OpenAI para qualificacao.");
            }
            catch (Exception exception)
            {
                lastException = exception;
                _logger.LogWarning(
                    exception,
                    "Falha ao chamar OpenAI para qualificacao da jornada. Attempt={Attempt}",
                    attempt);
            }

            if (attempt <= maxRetries)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), cancellationToken);
            }
        }

        _logger.LogWarning(lastException, "OpenAI indisponivel para qualificacao da jornada; fallback deterministico sera aplicado.");
        return new JourneyQualificationAiResult
        {
            Success = false,
            ErrorCode = "openai_unavailable",
            ErrorMessage = "Nao foi possivel acessar a OpenAI para qualificacao."
        };
    }

    private static string BuildSystemPrompt()
    {
        return
            "Voce qualifica solicitacoes de servico do marketplace ConsertaPraMim. " +
            "Extraia apenas os dados confiaveis do texto informado. " +
            "Nunca invente categoria, endereco ou CEP. " +
            "Responda obrigatoriamente em JSON valido.";
    }

    private static string BuildUserPrompt(JourneyQualificationInput input)
    {
        var payload = JsonSerializer.Serialize(new
        {
            boardType = input.BoardType,
            sourceChannel = input.SourceChannel,
            name = input.Name,
            phone = input.Phone,
            email = input.Email,
            serviceCategory = input.ServiceCategory,
            problemDescription = input.ProblemDescription,
            street = input.Street,
            neighborhood = input.Neighborhood,
            city = input.City,
            state = input.State,
            postalCode = input.PostalCode,
            latitude = input.Latitude,
            longitude = input.Longitude,
            internalNotes = input.InternalNotes
        }, JsonOptions);

        return
            """
            Responda com o JSON:
            {
              "serviceCategoryName": "categoria normalizada ou vazio",
              "problemContext": "resumo curto e objetivo do problema",
              "street": "logradouro",
              "neighborhood": "bairro",
              "city": "cidade",
              "state": "UF",
              "postalCode": "CEP no formato 12345-678",
              "confidenceHint": 0.0
            }

            Regras:
            - portugues-BR.
            - sem markdown.
            - confidenceHint entre 0 e 1.
            - se nao tiver certeza, devolva vazio no campo.

            Payload:
            """ + payload;
    }

    private static JourneyQualificationAiPayload ParsePayload(string output)
    {
        var json = TryExtractJson(output);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new JourneyQualificationAiPayload();
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            return new JourneyQualificationAiPayload
            {
                ServiceCategoryName = ReadString(root, "serviceCategoryName"),
                ProblemContext = ReadString(root, "problemContext"),
                Street = ReadString(root, "street"),
                Neighborhood = ReadString(root, "neighborhood"),
                City = ReadString(root, "city"),
                State = ReadString(root, "state"),
                PostalCode = ReadString(root, "postalCode"),
                ConfidenceHint = ReadDecimal(root, "confidenceHint")
            };
        }
        catch (JsonException)
        {
            return new JourneyQualificationAiPayload();
        }
    }

    private static string ReadString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()?.Trim() ?? string.Empty
            : string.Empty;
    }

    private static decimal ReadDecimal(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return 0;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetDecimal(out var value) => value,
            JsonValueKind.String when decimal.TryParse(property.GetString(), out var parsed) => parsed,
            _ => 0
        };
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout
            or (HttpStatusCode)429;
    }

    private static (string? ErrorCode, string? ErrorMessage) ParseError(JsonElement root)
    {
        if (!root.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object)
        {
            return (null, null);
        }

        var code = error.TryGetProperty("code", out var codeElement) && codeElement.ValueKind == JsonValueKind.String
            ? codeElement.GetString()
            : null;
        var message = error.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.String
            ? messageElement.GetString()
            : null;

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
                if (content.TryGetProperty("text", out var textElement) &&
                    textElement.ValueKind == JsonValueKind.String)
                {
                    var value = textElement.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        chunks.Add(value.Trim());
                    }
                }
            }
        }

        return chunks.Count == 0 ? null : string.Join(Environment.NewLine, chunks);
    }

    private static string? TryExtractJson(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
        {
            return trimmed;
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        return start >= 0 && end > start ? trimmed[start..(end + 1)] : null;
    }
}
