using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Provider.Security;

namespace ConsertaPraMim.Web.Provider.Services;

public class ProviderBackendApiClient : IProviderBackendApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProviderBackendApiClient> _logger;

    public ProviderBackendApiClient(
        IHttpClientFactory httpClientFactory,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        ILogger<ProviderBackendApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<(IReadOnlyList<ServiceRequestDto> Requests, string? ErrorMessage)> GetRequestsAsync(
        string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        var relativePath = "/api/service-requests";
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            relativePath += $"?searchTerm={Uri.EscapeDataString(searchTerm.Trim())}";
        }

        var result = await GetListAsync<ServiceRequestDto>(relativePath, cancellationToken);
        return (result.Items, result.ErrorMessage);
    }

    public async Task<(IReadOnlyList<ServiceRequestDto> Requests, string? ErrorMessage)> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetListAsync<ServiceRequestDto>("/api/service-requests/provider/history", cancellationToken);
        return (result.Items, result.ErrorMessage);
    }

    public async Task<(IReadOnlyList<ProposalDto> Proposals, string? ErrorMessage)> GetMyProposalsAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetListAsync<ProposalDto>("/api/proposals/my-proposals", cancellationToken);
        return (result.Items, result.ErrorMessage);
    }

    public async Task<(IReadOnlyList<ServiceAppointmentDto> Appointments, string? ErrorMessage)> GetMyAppointmentsAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetListAsync<ServiceAppointmentDto>("/api/service-appointments/mine", cancellationToken);
        return (result.Items, result.ErrorMessage);
    }

    public async Task<(UserProfileDto? Profile, string? ErrorMessage)> GetProfileAsync(CancellationToken cancellationToken = default)
    {
        var result = await SendAsync<UserProfileDto>(HttpMethod.Get, "/api/profile", null, cancellationToken);
        return (result.Payload, result.ErrorMessage);
    }

    public async Task<(MobileProviderCoverageMapDto? CoverageMap, string? ErrorMessage)> GetCoverageMapAsync(
        string? categoryFilter = null,
        double? maxDistanceKm = null,
        int pinPage = 1,
        int pinPageSize = 120,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"pinPage={Math.Max(1, pinPage)}",
            $"pinPageSize={Math.Max(1, pinPageSize)}"
        };

        if (!string.IsNullOrWhiteSpace(categoryFilter))
        {
            query.Add($"categoryFilter={Uri.EscapeDataString(categoryFilter.Trim())}");
        }

        if (maxDistanceKm.HasValue && maxDistanceKm.Value > 0)
        {
            query.Add($"maxDistanceKm={maxDistanceKm.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }

        var relativePath = $"/api/mobile/provider/coverage-map?{string.Join("&", query)}";
        var result = await SendAsync<MobileProviderCoverageMapDto>(HttpMethod.Get, relativePath, null, cancellationToken);
        return (result.Payload, result.ErrorMessage);
    }

    public async Task<(MobileProviderSupportTicketListResponseDto? Response, string? ErrorMessage)> GetSupportTicketsAsync(
        string? status = null,
        string? priority = null,
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"page={Math.Max(1, page)}",
            $"pageSize={Math.Max(1, pageSize)}"
        };

        if (!string.IsNullOrWhiteSpace(status))
        {
            query.Add($"status={Uri.EscapeDataString(status.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            query.Add($"priority={Uri.EscapeDataString(priority.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search.Trim())}");
        }

        var relativePath = $"/api/mobile/provider/support/tickets?{string.Join("&", query)}";
        var result = await SendAsync<MobileProviderSupportTicketListResponseDto>(HttpMethod.Get, relativePath, null, cancellationToken);
        return (result.Payload, result.ErrorMessage);
    }

    public async Task<(MobileProviderSupportTicketDetailsDto? Ticket, string? ErrorMessage)> CreateSupportTicketAsync(
        MobileProviderCreateSupportTicketRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var result = await SendAsync<MobileProviderSupportTicketDetailsDto>(
            HttpMethod.Post,
            "/api/mobile/provider/support/tickets",
            request,
            cancellationToken);
        return (result.Payload, result.ErrorMessage);
    }

    public async Task<(MobileProviderSupportTicketDetailsDto? Ticket, string? ErrorMessage)> GetSupportTicketDetailsAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        var result = await SendAsync<MobileProviderSupportTicketDetailsDto>(
            HttpMethod.Get,
            $"/api/mobile/provider/support/tickets/{ticketId:D}",
            null,
            cancellationToken);
        return (result.Payload, result.ErrorMessage);
    }

    public async Task<(MobileProviderSupportTicketDetailsDto? Ticket, string? ErrorMessage)> AddSupportTicketMessageAsync(
        Guid ticketId,
        MobileProviderSupportTicketMessageRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var result = await SendAsync<MobileProviderSupportTicketDetailsDto>(
            HttpMethod.Post,
            $"/api/mobile/provider/support/tickets/{ticketId:D}/messages",
            request,
            cancellationToken);
        return (result.Payload, result.ErrorMessage);
    }

    public async Task<(MobileProviderSupportTicketDetailsDto? Ticket, string? ErrorMessage)> CloseSupportTicketAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        var result = await SendAsync<MobileProviderSupportTicketDetailsDto>(
            HttpMethod.Post,
            $"/api/mobile/provider/support/tickets/{ticketId:D}/close",
            null,
            cancellationToken);
        return (result.Payload, result.ErrorMessage);
    }

    public async Task<(bool Success, string? ErrorMessage)> SubmitProposalAsync(CreateProposalDto dto, CancellationToken cancellationToken = default)
    {
        var result = await SendAsync<object>(HttpMethod.Post, "/api/proposals", dto, cancellationToken);
        return (result.Success, result.ErrorMessage);
    }

    public async Task<(IReadOnlyList<ChatConversationSummaryDto> Conversations, string? ErrorMessage)> GetConversationsAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetListAsync<ChatConversationSummaryDto>("/api/chats/conversations", cancellationToken);
        return (result.Items, result.ErrorMessage);
    }

    private async Task<(IReadOnlyList<T> Items, string? ErrorMessage)> GetListAsync<T>(string relativePath, CancellationToken cancellationToken)
    {
        var result = await SendAsync<List<T>>(HttpMethod.Get, relativePath, null, cancellationToken);
        if (result.Payload == null)
        {
            return (Array.Empty<T>(), result.ErrorMessage);
        }

        return (result.Payload, result.ErrorMessage);
    }

    private async Task<(bool Success, T? Payload, string? ErrorMessage)> SendAsync<T>(
        HttpMethod method,
        string relativePath,
        object? body,
        CancellationToken cancellationToken)
    {
        if (!TryBuildAbsoluteUrl(relativePath, out var absoluteUrl, out var urlError))
        {
            return (false, default, urlError);
        }

        if (!TryGetApiToken(out var token))
        {
            return (false, default, "Sessao expirada. Faca login novamente.");
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
            if (!response.IsSuccessStatusCode)
            {
                var apiError = await response.Content.ReadAsStringAsync(cancellationToken);
                return (false, default, string.IsNullOrWhiteSpace(apiError)
                    ? $"Falha ao chamar API ({(int)response.StatusCode})."
                    : apiError);
            }

            if (typeof(T) == typeof(object))
            {
                return (true, default, null);
            }

            var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            return (true, payload, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao chamar endpoint {RelativePath} da API no portal prestador.", relativePath);
            return (false, default, "Falha de comunicacao com a API.");
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
}
