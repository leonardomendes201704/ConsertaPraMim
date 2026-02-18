using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Provider.Security;

namespace ConsertaPraMim.Web.Provider.Services;

public class ProviderOnboardingApiClient : IProviderOnboardingApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProviderOnboardingApiClient> _logger;

    public ProviderOnboardingApiClient(
        IHttpClientFactory httpClientFactory,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        ILogger<ProviderOnboardingApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _logger = logger;
    }

    public Task<(ProviderOnboardingStateDto? State, string? ErrorMessage)> GetStateAsync(CancellationToken cancellationToken = default)
    {
        var token = _httpContextAccessor.HttpContext?.User.FindFirst(WebProviderClaimTypes.ApiToken)?.Value;
        return GetStateInternalAsync(token, cancellationToken);
    }

    public Task<(ProviderOnboardingStateDto? State, string? ErrorMessage)> GetStateAsync(string bearerToken, CancellationToken cancellationToken = default)
    {
        return GetStateInternalAsync(bearerToken, cancellationToken);
    }

    private async Task<(ProviderOnboardingStateDto? State, string? ErrorMessage)> GetStateInternalAsync(
        string? bearerToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            return (null, "Token de API ausente na sessao.");
        }

        var baseUrl = _configuration["ApiBaseUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return (null, "ApiBaseUrl nao configurada.");
        }

        var url = $"{baseUrl.TrimEnd('/')}/api/provider-onboarding";
        try
        {
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var apiError = await response.Content.ReadAsStringAsync(cancellationToken);
                return (null, string.IsNullOrWhiteSpace(apiError)
                    ? $"Falha ao carregar onboarding ({(int)response.StatusCode})."
                    : apiError);
            }

            var state = await response.Content.ReadFromJsonAsync<ProviderOnboardingStateDto>(JsonOptions, cancellationToken);
            return (state, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao consultar onboarding via API.");
            return (null, "Falha de comunicacao com a API.");
        }
    }
}
