using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ConsertaPraMim.Web.Provider.Security;

namespace ConsertaPraMim.Web.Provider.Services;

public class ProviderApiCaller
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProviderApiCaller> _logger;

    public ProviderApiCaller(
        IHttpClientFactory httpClientFactory,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        ILogger<ProviderApiCaller> logger)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ApiResponse<T>> SendAsync<T>(
        HttpMethod method,
        string relativePath,
        object? body = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryBuildAbsoluteUrl(relativePath, out var absoluteUrl, out var urlError))
        {
            return ApiResponse<T>.FromFailure(HttpStatusCode.BadRequest, urlError);
        }

        if (!TryGetApiToken(out var token))
        {
            return ApiResponse<T>.FromFailure(HttpStatusCode.Unauthorized, "Sessao expirada. Faca login novamente.");
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(method, absoluteUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            if (body != null)
            {
                request.Content = JsonContent.Create(body);
            }

            using var response = await client.SendAsync(request, cancellationToken);
            var raw = response.Content == null ? string.Empty : await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                if (typeof(T) == typeof(object) || string.IsNullOrWhiteSpace(raw))
                {
                    return ApiResponse<T>.FromSuccess(response.StatusCode, default);
                }

                var payload = JsonSerializer.Deserialize<T>(raw, JsonOptions);
                return ApiResponse<T>.FromSuccess(response.StatusCode, payload);
            }

            T? failurePayload = default;
            if (!string.IsNullOrWhiteSpace(raw))
            {
                try
                {
                    failurePayload = JsonSerializer.Deserialize<T>(raw, JsonOptions);
                }
                catch
                {
                    // ignore parse failure on error payload
                }
            }

            return new ApiResponse<T>(false, response.StatusCode, failurePayload, ExtractErrorMessage(raw, (int)response.StatusCode));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao chamar endpoint {RelativePath} no portal prestador.", relativePath);
            return ApiResponse<T>.FromFailure(HttpStatusCode.ServiceUnavailable, "Falha de comunicacao com a API.");
        }
    }

    private bool TryBuildAbsoluteUrl(string relativePath, out string absoluteUrl, out string? errorMessage)
    {
        var baseUrl = _configuration["ApiBaseUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            absoluteUrl = string.Empty;
            errorMessage = "ApiBaseUrl nao configurada.";
            return false;
        }

        absoluteUrl = $"{baseUrl.TrimEnd('/')}{relativePath}";
        errorMessage = null;
        return true;
    }

    private bool TryGetApiToken(out string token)
    {
        token = _httpContextAccessor.HttpContext?.User.FindFirst(WebProviderClaimTypes.ApiToken)?.Value ?? string.Empty;
        return !string.IsNullOrWhiteSpace(token);
    }

    private static string ExtractErrorMessage(string raw, int statusCode)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return $"Falha ao chamar API ({statusCode}).";
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.String)
                {
                    return messageElement.GetString() ?? $"Falha ao chamar API ({statusCode}).";
                }

                if (doc.RootElement.TryGetProperty("errorMessage", out var errorMessageElement) && errorMessageElement.ValueKind == JsonValueKind.String)
                {
                    return errorMessageElement.GetString() ?? $"Falha ao chamar API ({statusCode}).";
                }
            }
        }
        catch
        {
            // ignore malformed json
        }

        return raw.Length > 400 ? raw[..400] : raw;
    }
}

public sealed record ApiResponse<T>(bool Success, HttpStatusCode StatusCode, T? Payload, string? ErrorMessage)
{
    public static ApiResponse<T> FromSuccess(HttpStatusCode statusCode, T? payload) =>
        new(true, statusCode, payload, null);

    public static ApiResponse<T> FromFailure(HttpStatusCode statusCode, string? errorMessage) =>
        new(false, statusCode, default, errorMessage);
}

