using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ConsertaPraMim.LoadTest.Wpf.Services;

public sealed class AdminLoadTestPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly Action<string> _logger;

    public AdminLoadTestPublisher(Action<string>? logger = null)
    {
        _logger = logger ?? (_ => { });
    }

    public async Task<LoadTestPublishResult> PublishAsync(
        LoadTestReport report,
        LoadTestRunOptions options,
        CancellationToken cancellationToken)
    {
        var endpoint = ResolveAbsoluteUrl(
            options.AdminPublish.ImportUrl,
            options.BaseUrl,
            "/api/admin/loadtests/import");

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return new LoadTestPublishResult
            {
                Attempted = true,
                Succeeded = false,
                Endpoint = string.Empty,
                Message = "Nao foi possivel resolver endpoint de importacao admin."
            };
        }

        var token = ResolveCredential("CPM_LOADTEST_ADMIN_BEARER_TOKEN", options.AdminPublish.BearerToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            token = await TryAuthenticateAsync(options, cancellationToken).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return new LoadTestPublishResult
            {
                Attempted = true,
                Succeeded = false,
                Endpoint = endpoint,
                Message = "Falha ao obter token admin para publicar. Configure adminPublish (bearerToken ou email/senha)."
            };
        }

        using var httpClient = CreateHttpClient(options.InsecureTls, options.TimeoutSeconds);
        var payload = new
        {
            source = ResolveSource(options.AdminPublish.Source),
            report = JsonSerializer.SerializeToElement(report, JsonOptions)
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-Client-Id", "LT-WPF-PUBLISHER");
        request.Headers.Add("X-Correlation-Id", Guid.NewGuid().ToString("D"));

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return new LoadTestPublishResult
            {
                Attempted = true,
                Succeeded = false,
                Endpoint = endpoint,
                Message = $"HTTP {(int)response.StatusCode} ao publicar no admin: {Truncate(body, 240)}"
            };
        }

        return new LoadTestPublishResult
        {
            Attempted = true,
            Succeeded = true,
            Endpoint = endpoint,
            Message = BuildSuccessMessage(body)
        };
    }

    private async Task<string?> TryAuthenticateAsync(LoadTestRunOptions options, CancellationToken cancellationToken)
    {
        var email = ResolveCredential("CPM_LOADTEST_ADMIN_EMAIL", options.AdminPublish.Email);
        var password = ResolveCredential("CPM_LOADTEST_ADMIN_PASSWORD", options.AdminPublish.Password);
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var loginUrl = ResolveAbsoluteUrl(options.AdminPublish.LoginUrl, options.BaseUrl, "/api/auth/login");
        if (string.IsNullOrWhiteSpace(loginUrl))
        {
            return null;
        }

        using var httpClient = CreateHttpClient(options.InsecureTls, options.TimeoutSeconds);
        var payload = new
        {
            email,
            password
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, loginUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Client-Id", "LT-WPF-PUBLISHER");
        request.Headers.Add("X-Correlation-Id", Guid.NewGuid().ToString("D"));

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger($"Falha no login admin para publish. HTTP {(int)response.StatusCode}: {Truncate(body, 180)}");
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!TryGetPropertyIgnoreCase(doc.RootElement, options.AdminPublish.TokenField, out var tokenElement))
            {
                if (!TryGetPropertyIgnoreCase(doc.RootElement, "token", out tokenElement))
                {
                    _logger("Resposta de login admin sem token.");
                    return null;
                }
            }

            var token = tokenElement.GetString();
            return string.IsNullOrWhiteSpace(token) ? null : token.Trim();
        }
        catch (JsonException)
        {
            _logger("Resposta de login admin invalida (JSON).");
            return null;
        }
    }

    private static HttpClient CreateHttpClient(bool insecureTls, double timeoutSeconds)
    {
        var handler = new HttpClientHandler();
        if (insecureTls)
        {
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(timeoutSeconds, 1))
        };
    }

    private static string ResolveSource(string configuredSource)
    {
        return string.IsNullOrWhiteSpace(configuredSource)
            ? "wpf_loadtest_runner"
            : configuredSource.Trim();
    }

    private static string ResolveCredential(string environmentName, string configuredValue)
    {
        var environmentValue = Environment.GetEnvironmentVariable(environmentName);
        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            return environmentValue.Trim();
        }

        return configuredValue?.Trim() ?? string.Empty;
    }

    private static string ResolveAbsoluteUrl(string configuredUrl, string baseUrl, string defaultRelativePath)
    {
        var raw = string.IsNullOrWhiteSpace(configuredUrl)
            ? defaultRelativePath
            : configuredUrl.Trim();

        if (Uri.TryCreate(raw, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.ToString();
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            return string.Empty;
        }

        if (!raw.StartsWith("/", StringComparison.Ordinal))
        {
            raw = "/" + raw;
        }

        return new Uri(baseUri, raw).ToString();
    }

    private static string BuildSuccessMessage(string rawResponseBody)
    {
        if (string.IsNullOrWhiteSpace(rawResponseBody))
        {
            return "Run publicado com sucesso.";
        }

        try
        {
            using var doc = JsonDocument.Parse(rawResponseBody);
            var root = doc.RootElement;
            var id = TryGetStringIgnoreCase(root, "id");
            var externalRunId = TryGetStringIgnoreCase(root, "externalRunId");
            var created = TryGetStringIgnoreCase(root, "created");

            var details = new List<string>();
            if (!string.IsNullOrWhiteSpace(id))
            {
                details.Add($"id={id}");
            }

            if (!string.IsNullOrWhiteSpace(externalRunId))
            {
                details.Add($"runId={externalRunId}");
            }

            if (!string.IsNullOrWhiteSpace(created))
            {
                details.Add($"created={created}");
            }

            return details.Count == 0
                ? "Run publicado com sucesso."
                : $"Run publicado com sucesso ({string.Join(", ", details)}).";
        }
        catch (JsonException)
        {
            return "Run publicado com sucesso.";
        }
    }

    private static string? TryGetStringIgnoreCase(JsonElement source, string propertyName)
    {
        if (TryGetPropertyIgnoreCase(source, propertyName, out var value))
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Number => value.ToString(),
                _ => value.ToString()
            };
        }

        return null;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement source, string propertyName, out JsonElement value)
    {
        if (source.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in source.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
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
