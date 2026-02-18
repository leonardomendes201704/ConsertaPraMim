using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Web.Provider.Security;

namespace ConsertaPraMim.Web.Provider.Services;

public class ProviderApiProviderGalleryMediaProcessor : IProviderGalleryMediaProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;

    public ProviderApiProviderGalleryMediaProcessor(
        IHttpClientFactory httpClientFactory,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
    }

    public async Task<ProcessedProviderGalleryMediaDto> ProcessAndStoreAsync(
        Stream source,
        string originalFileName,
        string contentType,
        long originalSizeBytes,
        CancellationToken cancellationToken = default)
    {
        if (!TryBuildAbsoluteUrl("/api/provider-gallery/process-media", out var url))
        {
            throw new InvalidOperationException("ApiBaseUrl nao configurada para processamento de midia.");
        }

        if (!TryGetApiToken(out var token))
        {
            throw new InvalidOperationException("Sessao expirada. Faca login novamente.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var multipart = new MultipartFormDataContent();

        var streamContent = new StreamContent(source);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        multipart.Add(streamContent, "file", Path.GetFileName(originalFileName));

        request.Content = multipart;

        var client = _httpClientFactory.CreateClient();
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var rawError = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(ExtractErrorMessage(rawError, (int)response.StatusCode));
        }

        var payload = await response.Content.ReadFromJsonAsync<ProcessedProviderGalleryMediaDto>(JsonOptions, cancellationToken);
        if (payload == null)
        {
            throw new InvalidOperationException("Resposta invalida ao processar midia da galeria.");
        }

        return payload;
    }

    private bool TryBuildAbsoluteUrl(string relativePath, out string absoluteUrl)
    {
        var baseUrl = _configuration["ApiBaseUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            absoluteUrl = string.Empty;
            return false;
        }

        absoluteUrl = $"{baseUrl.TrimEnd('/')}{relativePath}";
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
            return $"Falha ao processar midia ({statusCode}).";
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("message", out var messageElement) &&
                messageElement.ValueKind == JsonValueKind.String)
            {
                return messageElement.GetString() ?? $"Falha ao processar midia ({statusCode}).";
            }
        }
        catch
        {
            // ignore invalid payload
        }

        return raw.Length > 400 ? raw[..400] : raw;
    }
}
