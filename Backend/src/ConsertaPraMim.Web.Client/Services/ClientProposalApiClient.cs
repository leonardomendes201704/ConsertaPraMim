using System.Net.Http.Headers;
using ConsertaPraMim.Web.Client.Security;

namespace ConsertaPraMim.Web.Client.Services;

public class ClientProposalApiClient : IClientProposalApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ClientProposalApiClient> _logger;

    public ClientProposalApiClient(
        IHttpClientFactory httpClientFactory,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        ILogger<ClientProposalApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<(bool Success, string? ErrorMessage)> AcceptAsync(Guid proposalId, CancellationToken cancellationToken = default)
    {
        if (!TryBuildUrl($"/api/proposals/{proposalId}/accept", out var url, out var errorMessage))
        {
            return (false, errorMessage);
        }

        if (!TryGetApiToken(out var token))
        {
            return (false, "Sessao expirada. Faca login novamente.");
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Put, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(string.Empty);

            using var response = await client.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var apiError = await response.Content.ReadAsStringAsync(cancellationToken);
            return (false, string.IsNullOrWhiteSpace(apiError)
                ? $"Nao foi possivel aceitar a proposta ({(int)response.StatusCode})."
                : apiError);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao aceitar proposta via API.");
            return (false, "Falha de comunicacao com a API.");
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
