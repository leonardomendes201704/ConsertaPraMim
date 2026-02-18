using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Client.Security;

namespace ConsertaPraMim.Web.Client.Services;

public class ClientDashboardApiClient : IClientDashboardApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ClientDashboardApiClient> _logger;

    public ClientDashboardApiClient(
        IHttpClientFactory httpClientFactory,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        ILogger<ClientDashboardApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<(IReadOnlyList<ServiceRequestDto> Requests, string? ErrorMessage)> GetMyRequestsAsync(CancellationToken cancellationToken = default)
    {
        if (!TryBuildUrl("/api/service-requests", out var url, out var errorMessage))
        {
            return (Array.Empty<ServiceRequestDto>(), errorMessage);
        }

        if (!TryGetApiToken(out var token))
        {
            return (Array.Empty<ServiceRequestDto>(), "Sessao expirada. Faca login novamente.");
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var apiError = await response.Content.ReadAsStringAsync(cancellationToken);
                return (Array.Empty<ServiceRequestDto>(),
                    string.IsNullOrWhiteSpace(apiError)
                        ? $"Falha ao carregar pedidos ({(int)response.StatusCode})."
                        : apiError);
            }

            var payload = await response.Content.ReadFromJsonAsync<List<ServiceRequestDto>>(JsonOptions, cancellationToken);
            return (payload ?? new List<ServiceRequestDto>(), null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao carregar pedidos do dashboard do cliente via API.");
            return (Array.Empty<ServiceRequestDto>(), "Falha de comunicacao com a API.");
        }
    }

    private bool TryBuildUrl(string relativePath, out string url, out string? errorMessage)
    {
        var baseUrl = _configuration["ApiBaseUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            url = string.Empty;
            errorMessage = "ApiBaseUrl nao configurada.";
            return false;
        }

        url = $"{baseUrl.TrimEnd('/')}{relativePath}";
        errorMessage = null;
        return true;
    }

    private bool TryGetApiToken(out string token)
    {
        token = _httpContextAccessor.HttpContext?.User.FindFirst(WebClientClaimTypes.ApiToken)?.Value ?? string.Empty;
        return !string.IsNullOrWhiteSpace(token);
    }
}
