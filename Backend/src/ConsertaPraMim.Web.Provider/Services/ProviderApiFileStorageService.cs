using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Web.Provider.Security;

namespace ConsertaPraMim.Web.Provider.Services;

public class ProviderApiFileStorageService : IFileStorageService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProviderApiFileStorageService> _logger;

    public ProviderApiFileStorageService(
        IHttpClientFactory httpClientFactory,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        ILogger<ProviderApiFileStorageService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string fileName, string folder)
    {
        if (fileStream == null)
        {
            throw new ArgumentNullException(nameof(fileStream));
        }

        if (!TryBuildAbsoluteUrl("/api/files/upload", out var url))
        {
            throw new InvalidOperationException("ApiBaseUrl nao configurada para upload de arquivos.");
        }

        if (!TryGetApiToken(out var token))
        {
            throw new InvalidOperationException("Sessao expirada. Faca login novamente.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var multipart = new MultipartFormDataContent();
        multipart.Add(new StringContent(folder ?? string.Empty, Encoding.UTF8), "folder");

        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        multipart.Add(streamContent, "file", Path.GetFileName(fileName));
        request.Content = multipart;

        var client = _httpClientFactory.CreateClient();
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        var raw = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            var message = ExtractErrorMessage(raw, (int)response.StatusCode);
            throw new InvalidOperationException(message);
        }

        using var document = JsonDocument.Parse(raw);
        if (!document.RootElement.TryGetProperty("relativeUrl", out var relativeUrlElement) ||
            relativeUrlElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("Resposta invalida ao salvar arquivo na API.");
        }

        var relativeUrl = relativeUrlElement.GetString();
        if (string.IsNullOrWhiteSpace(relativeUrl))
        {
            throw new InvalidOperationException("Resposta invalida ao salvar arquivo na API.");
        }

        return relativeUrl;
    }

    public void DeleteFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        if (!TryBuildAbsoluteUrl($"/api/files?filePath={Uri.EscapeDataString(filePath.Trim())}", out var url))
        {
            return;
        }

        if (!TryGetApiToken(out var token))
        {
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Delete, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = client.SendAsync(request).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Falha ao remover arquivo via API. FilePath={FilePath}, StatusCode={StatusCode}",
                    filePath,
                    (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao remover arquivo via API. FilePath={FilePath}", filePath);
        }
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
            return $"Falha ao salvar arquivo na API ({statusCode}).";
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                {
                    return message.GetString() ?? $"Falha ao salvar arquivo na API ({statusCode}).";
                }
            }
        }
        catch
        {
            // ignore invalid payload
        }

        return raw.Length > 400 ? raw[..400] : raw;
    }
}
