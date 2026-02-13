using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Admin.Models;
using Microsoft.AspNetCore.WebUtilities;

namespace ConsertaPraMim.Web.Admin.Services;

public class AdminDashboardApiClient : IAdminDashboardApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminDashboardApiClient> _logger;

    public AdminDashboardApiClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AdminDashboardApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AdminDashboardApiResult> GetDashboardAsync(
        AdminDashboardFilterModel filters,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return AdminDashboardApiResult.Fail("Sessao expirada. Faca login novamente.", (int)HttpStatusCode.Unauthorized);
        }

        var baseUrl = _configuration["ApiBaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return AdminDashboardApiResult.Fail("ApiBaseUrl nao configurada para o portal admin.");
        }

        var url = BuildDashboardUrl(baseUrl.TrimEnd('/'), filters);
        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var message = response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => "Sessao de API expirada. Faca login novamente.",
                    HttpStatusCode.Forbidden => "Acesso negado ao endpoint administrativo.",
                    _ => $"Falha ao consultar dashboard admin na API ({(int)response.StatusCode})."
                };

                return AdminDashboardApiResult.Fail(message, (int)response.StatusCode);
            }

            var payload = await response.Content.ReadFromJsonAsync<AdminDashboardDto>(JsonOptions, cancellationToken);
            if (payload == null)
            {
                return AdminDashboardApiResult.Fail("Resposta vazia da API de dashboard.");
            }

            return AdminDashboardApiResult.Ok(payload);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao chamar endpoint admin dashboard.");
            return AdminDashboardApiResult.Fail("Nao foi possivel carregar o dashboard administrativo.");
        }
    }

    private static string BuildDashboardUrl(string baseUrl, AdminDashboardFilterModel filters)
    {
        var query = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["page"] = Math.Max(1, filters.Page).ToString(CultureInfo.InvariantCulture),
            ["pageSize"] = Math.Clamp(filters.PageSize, 1, 100).ToString(CultureInfo.InvariantCulture),
            ["eventType"] = NormalizeEventType(filters.EventType),
            ["searchTerm"] = string.IsNullOrWhiteSpace(filters.SearchTerm) ? null : filters.SearchTerm.Trim()
        };

        if (filters.FromUtc.HasValue)
        {
            query["fromUtc"] = filters.FromUtc.Value.ToString("o", CultureInfo.InvariantCulture);
        }

        if (filters.ToUtc.HasValue)
        {
            query["toUtc"] = filters.ToUtc.Value.ToString("o", CultureInfo.InvariantCulture);
        }

        var nonEmptyQuery = query
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
            .ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value, StringComparer.OrdinalIgnoreCase);

        return QueryHelpers.AddQueryString($"{baseUrl}/api/admin/dashboard", nonEmptyQuery);
    }

    private static string NormalizeEventType(string? rawEventType)
    {
        if (string.IsNullOrWhiteSpace(rawEventType))
        {
            return "all";
        }

        var normalized = rawEventType.Trim().ToLowerInvariant();
        return normalized switch
        {
            "request" => "request",
            "proposal" => "proposal",
            "chat" => "chat",
            _ => "all"
        };
    }
}
