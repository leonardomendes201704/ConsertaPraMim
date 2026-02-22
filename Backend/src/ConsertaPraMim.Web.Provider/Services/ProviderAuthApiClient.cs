using System.Net.Http.Json;
using System.Text.Json;
using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Web.Provider.Services;

public class ProviderAuthApiClient : IProviderAuthApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProviderAuthApiClient> _logger;

    public ProviderAuthApiClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ProviderAuthApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public Task<(LoginResponse? Response, string? ErrorMessage)> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
        => SendAsync("/api/auth/login", request, cancellationToken);

    public Task<(LoginResponse? Response, string? ErrorMessage)> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
        => SendAsync("/api/auth/register", request, cancellationToken);

    public async Task<(LegalTermsDocumentDto? Terms, string? ErrorMessage)> GetActiveLegalTermsAsync(
        string audience,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = _configuration["ApiBaseUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return (null, "ApiBaseUrl nao configurada.");
        }

        var normalizedAudience = string.IsNullOrWhiteSpace(audience)
            ? "provider"
            : audience.Trim().ToLowerInvariant();

        var url = $"{baseUrl.TrimEnd('/')}/api/legal-terms/active?audience={Uri.EscapeDataString(normalizedAudience)}";
        var client = _httpClientFactory.CreateClient();

        try
        {
            using var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = await response.Content.ReadAsStringAsync(cancellationToken);
                return (null, string.IsNullOrWhiteSpace(errorMessage)
                    ? "Nao foi possivel carregar o termo de aceite."
                    : errorMessage);
            }

            var payload = await response.Content.ReadFromJsonAsync<LegalTermsDocumentDto>(JsonOptions, cancellationToken);
            return payload == null
                ? (null, "Resposta vazia ao carregar termo de aceite.")
                : (payload, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao buscar termo ativo para portal prestador.");
            return (null, "Falha de comunicacao com a API ao carregar termo.");
        }
    }

    private async Task<(LoginResponse? Response, string? ErrorMessage)> SendAsync<TRequest>(
        string relativePath,
        TRequest request,
        CancellationToken cancellationToken)
    {
        var baseUrl = _configuration["ApiBaseUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return (null, "ApiBaseUrl nao configurada.");
        }

        var url = $"{baseUrl.TrimEnd('/')}{relativePath}";
        var client = _httpClientFactory.CreateClient();

        try
        {
            using var response = await client.PostAsJsonAsync(url, request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = await response.Content.ReadAsStringAsync(cancellationToken);
                return (null, string.IsNullOrWhiteSpace(errorMessage) ? "Falha ao autenticar na API." : errorMessage);
            }

            var payload = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions, cancellationToken);
            return payload == null
                ? (null, "Resposta vazia da API de autenticacao.")
                : (payload, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao chamar API de autenticacao do portal prestador.");
            return (null, "Falha de comunicacao com a API.");
        }
    }
}
