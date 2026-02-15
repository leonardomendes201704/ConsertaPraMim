using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Enums;
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

    public async Task<AdminNoShowDashboardApiResult> GetNoShowDashboardAsync(
        AdminDashboardFilterModel filters,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return AdminNoShowDashboardApiResult.Fail("Sessao expirada. Faca login novamente.", (int)HttpStatusCode.Unauthorized);
        }

        var baseUrl = _configuration["ApiBaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return AdminNoShowDashboardApiResult.Fail("ApiBaseUrl nao configurada para o portal admin.");
        }

        var url = BuildNoShowDashboardUrl(baseUrl.TrimEnd('/'), filters);
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
                    HttpStatusCode.Forbidden => "Acesso negado ao endpoint de no-show.",
                    _ => $"Falha ao consultar dashboard de no-show na API ({(int)response.StatusCode})."
                };

                return AdminNoShowDashboardApiResult.Fail(message, (int)response.StatusCode);
            }

            var payload = await response.Content.ReadFromJsonAsync<AdminNoShowDashboardDto>(JsonOptions, cancellationToken);
            if (payload == null)
            {
                return AdminNoShowDashboardApiResult.Fail("Resposta vazia da API de no-show.");
            }

            return AdminNoShowDashboardApiResult.Ok(payload);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao chamar endpoint admin no-show dashboard.");
            return AdminNoShowDashboardApiResult.Fail("Nao foi possivel carregar o dashboard de no-show.");
        }
    }

    public async Task<AdminNoShowAlertThresholdApiResult> GetNoShowAlertThresholdsAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return AdminNoShowAlertThresholdApiResult.Fail("Sessao expirada. Faca login novamente.", (int)HttpStatusCode.Unauthorized);
        }

        var baseUrl = _configuration["ApiBaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return AdminNoShowAlertThresholdApiResult.Fail("ApiBaseUrl nao configurada para o portal admin.");
        }

        var url = $"{baseUrl.TrimEnd('/')}/api/admin/no-show-alert-thresholds";
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
                    HttpStatusCode.Forbidden => "Acesso negado ao endpoint de threshold de no-show.",
                    HttpStatusCode.NotFound => "Configuracao ativa de threshold de no-show nao encontrada.",
                    _ => $"Falha ao consultar thresholds de no-show na API ({(int)response.StatusCode})."
                };

                return AdminNoShowAlertThresholdApiResult.Fail(message, (int)response.StatusCode);
            }

            var payload = await response.Content.ReadFromJsonAsync<AdminNoShowAlertThresholdDto>(JsonOptions, cancellationToken);
            if (payload == null)
            {
                return AdminNoShowAlertThresholdApiResult.Fail("Resposta vazia da API de thresholds de no-show.");
            }

            return AdminNoShowAlertThresholdApiResult.Ok(payload);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao chamar endpoint admin no-show alert thresholds.");
            return AdminNoShowAlertThresholdApiResult.Fail("Nao foi possivel carregar os thresholds de no-show.");
        }
    }

    public async Task<AdminNoShowAlertThresholdApiResult> UpdateNoShowAlertThresholdsAsync(
        AdminUpdateNoShowAlertThresholdRequestDto requestDto,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return AdminNoShowAlertThresholdApiResult.Fail("Sessao expirada. Faca login novamente.", (int)HttpStatusCode.Unauthorized);
        }

        var baseUrl = _configuration["ApiBaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return AdminNoShowAlertThresholdApiResult.Fail("ApiBaseUrl nao configurada para o portal admin.");
        }

        var url = $"{baseUrl.TrimEnd('/')}/api/admin/no-show-alert-thresholds";
        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Put, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(requestDto);

        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var message = response.StatusCode switch
                {
                    HttpStatusCode.BadRequest => "Dados invalidos para atualizar thresholds de no-show.",
                    HttpStatusCode.Unauthorized => "Sessao de API expirada. Faca login novamente.",
                    HttpStatusCode.Forbidden => "Acesso negado ao endpoint de threshold de no-show.",
                    HttpStatusCode.NotFound => "Configuracao ativa de threshold de no-show nao encontrada.",
                    _ => $"Falha ao atualizar thresholds de no-show na API ({(int)response.StatusCode})."
                };

                try
                {
                    var errorPayload = await response.Content.ReadFromJsonAsync<AdminNoShowAlertThresholdUpdateResultDto>(JsonOptions, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(errorPayload?.ErrorMessage))
                    {
                        message = errorPayload.ErrorMessage;
                    }
                }
                catch
                {
                    // Ignore payload parse failures and keep fallback message.
                }

                return AdminNoShowAlertThresholdApiResult.Fail(message, (int)response.StatusCode);
            }

            var payload = await response.Content.ReadFromJsonAsync<AdminNoShowAlertThresholdUpdateResultDto>(JsonOptions, cancellationToken);
            if (payload?.Configuration == null)
            {
                return AdminNoShowAlertThresholdApiResult.Fail("Resposta vazia da API na atualizacao de thresholds.");
            }

            return AdminNoShowAlertThresholdApiResult.Ok(payload.Configuration);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar thresholds de no-show.");
            return AdminNoShowAlertThresholdApiResult.Fail("Nao foi possivel atualizar os thresholds de no-show.");
        }
    }

    private static string BuildDashboardUrl(string baseUrl, AdminDashboardFilterModel filters)
    {
        var query = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["page"] = Math.Max(1, filters.Page).ToString(CultureInfo.InvariantCulture),
            ["pageSize"] = Math.Clamp(filters.PageSize, 1, 100).ToString(CultureInfo.InvariantCulture),
            ["eventType"] = NormalizeEventType(filters.EventType),
            ["operationalStatus"] = NormalizeOperationalStatus(filters.OperationalStatus),
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

    private static string BuildNoShowDashboardUrl(string baseUrl, AdminDashboardFilterModel filters)
    {
        var query = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["fromUtc"] = filters.FromUtc?.ToString("o", CultureInfo.InvariantCulture),
            ["toUtc"] = filters.ToUtc?.ToString("o", CultureInfo.InvariantCulture),
            ["city"] = string.IsNullOrWhiteSpace(filters.NoShowCity) ? null : filters.NoShowCity.Trim(),
            ["category"] = string.IsNullOrWhiteSpace(filters.NoShowCategory) ? null : filters.NoShowCategory.Trim(),
            ["riskLevel"] = NormalizeNoShowRiskLevel(filters.NoShowRiskLevel),
            ["queueTake"] = Math.Clamp(filters.NoShowQueueTake, 1, 500).ToString(CultureInfo.InvariantCulture),
            ["cancellationNoShowWindowHours"] = Math.Clamp(filters.NoShowCancellationWindowHours, 1, 168).ToString(CultureInfo.InvariantCulture)
        };

        var nonEmptyQuery = query
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
            .ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value, StringComparer.OrdinalIgnoreCase);

        return QueryHelpers.AddQueryString($"{baseUrl}/api/admin/no-show-dashboard", nonEmptyQuery);
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

    private static string NormalizeOperationalStatus(string? rawOperationalStatus)
    {
        if (string.IsNullOrWhiteSpace(rawOperationalStatus))
        {
            return "all";
        }

        return ServiceAppointmentOperationalStatusExtensions.TryParseFlexible(rawOperationalStatus, out var parsed)
            ? parsed.ToString()
            : "all";
    }

    private static string? NormalizeNoShowRiskLevel(string? rawNoShowRiskLevel)
    {
        if (string.IsNullOrWhiteSpace(rawNoShowRiskLevel))
        {
            return null;
        }

        var normalized = rawNoShowRiskLevel.Trim();
        if (string.Equals(normalized, "all", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Enum.TryParse<ServiceAppointmentNoShowRiskLevel>(normalized, ignoreCase: true, out var parsed)
            ? parsed.ToString()
            : null;
    }
}
