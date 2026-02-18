using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Client.Security;

namespace ConsertaPraMim.Web.Client.Services;

public class ClientChatApiClient : IClientChatApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ClientChatApiClient> _logger;

    public ClientChatApiClient(
        IHttpClientFactory httpClientFactory,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        ILogger<ClientChatApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<(IReadOnlyList<ChatConversationSummaryDto> Conversations, string? ErrorMessage)> GetConversationsAsync(CancellationToken cancellationToken = default)
    {
        if (!TryBuildUrl("/api/chats/conversations", out var url, out var errorMessage))
        {
            return (Array.Empty<ChatConversationSummaryDto>(), errorMessage);
        }

        if (!TryGetApiToken(out var token))
        {
            return (Array.Empty<ChatConversationSummaryDto>(), "Sessao expirada. Faca login novamente.");
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
                return (Array.Empty<ChatConversationSummaryDto>(),
                    string.IsNullOrWhiteSpace(apiError)
                        ? $"Falha ao carregar conversas ({(int)response.StatusCode})."
                        : apiError);
            }

            var payload = await response.Content.ReadFromJsonAsync<List<ChatConversationSummaryDto>>(JsonOptions, cancellationToken);
            return (payload ?? new List<ChatConversationSummaryDto>(), null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao carregar conversas do cliente via API.");
            return (Array.Empty<ChatConversationSummaryDto>(), "Falha de comunicacao com a API.");
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
