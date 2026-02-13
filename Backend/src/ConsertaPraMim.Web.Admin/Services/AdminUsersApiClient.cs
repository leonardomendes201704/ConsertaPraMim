using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Admin.Models;
using Microsoft.AspNetCore.WebUtilities;

namespace ConsertaPraMim.Web.Admin.Services;

public class AdminUsersApiClient : IAdminUsersApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminUsersApiClient> _logger;

    public AdminUsersApiClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AdminUsersApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AdminApiResult<AdminUsersListResponseDto>> GetUsersAsync(
        AdminUsersFilterModel filters,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return AdminApiResult<AdminUsersListResponseDto>.Fail("Sessao expirada. Faca login novamente.", "unauthorized", (int)HttpStatusCode.Unauthorized);
        }

        var baseUrl = _configuration["ApiBaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return AdminApiResult<AdminUsersListResponseDto>.Fail("ApiBaseUrl nao configurada para o portal admin.");
        }

        var url = BuildUsersUrl(baseUrl.TrimEnd('/'), filters);
        var response = await SendAsync(HttpMethod.Get, url, accessToken, null, cancellationToken);
        if (!response.Success || response.HttpResponse == null)
        {
            return AdminApiResult<AdminUsersListResponseDto>.Fail(
                response.ErrorMessage ?? "Falha ao consultar usuarios.",
                response.ErrorCode,
                response.StatusCode);
        }

        var payload = await response.HttpResponse.Content.ReadFromJsonAsync<AdminUsersListResponseDto>(JsonOptions, cancellationToken);
        if (payload == null)
        {
            return AdminApiResult<AdminUsersListResponseDto>.Fail("Resposta vazia da API de usuarios.");
        }

        return AdminApiResult<AdminUsersListResponseDto>.Ok(payload);
    }

    public async Task<AdminApiResult<AdminUserDetailsDto>> GetUserByIdAsync(
        Guid userId,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return AdminApiResult<AdminUserDetailsDto>.Fail("Sessao expirada. Faca login novamente.", "unauthorized", (int)HttpStatusCode.Unauthorized);
        }

        var baseUrl = _configuration["ApiBaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return AdminApiResult<AdminUserDetailsDto>.Fail("ApiBaseUrl nao configurada para o portal admin.");
        }

        var url = $"{baseUrl.TrimEnd('/')}/api/admin/users/{userId:D}";
        var response = await SendAsync(HttpMethod.Get, url, accessToken, null, cancellationToken);
        if (!response.Success || response.HttpResponse == null)
        {
            return AdminApiResult<AdminUserDetailsDto>.Fail(
                response.ErrorMessage ?? "Falha ao consultar detalhe do usuario.",
                response.ErrorCode,
                response.StatusCode);
        }

        var payload = await response.HttpResponse.Content.ReadFromJsonAsync<AdminUserDetailsDto>(JsonOptions, cancellationToken);
        if (payload == null)
        {
            return AdminApiResult<AdminUserDetailsDto>.Fail("Resposta vazia da API de usuarios.");
        }

        return AdminApiResult<AdminUserDetailsDto>.Ok(payload);
    }

    public async Task<AdminApiResult<AdminUpdateUserStatusResultDto>> UpdateUserStatusAsync(
        Guid userId,
        bool isActive,
        string? reason,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return AdminApiResult<AdminUpdateUserStatusResultDto>.Fail("Sessao expirada. Faca login novamente.", "unauthorized", (int)HttpStatusCode.Unauthorized);
        }

        var baseUrl = _configuration["ApiBaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return AdminApiResult<AdminUpdateUserStatusResultDto>.Fail("ApiBaseUrl nao configurada para o portal admin.");
        }

        var url = $"{baseUrl.TrimEnd('/')}/api/admin/users/{userId:D}/status";
        var payload = new AdminUpdateUserStatusRequestDto(isActive, reason);
        var response = await SendAsync(HttpMethod.Put, url, accessToken, payload, cancellationToken);

        if (!response.Success || response.HttpResponse == null)
        {
            var errorPayload = response.ErrorPayload;
            var message = errorPayload?.ErrorMessage ?? response.ErrorMessage ?? "Nao foi possivel alterar o status do usuario.";
            return AdminApiResult<AdminUpdateUserStatusResultDto>.Fail(
                message,
                errorPayload?.ErrorCode ?? response.ErrorCode,
                response.StatusCode);
        }

        var result = await response.HttpResponse.Content.ReadFromJsonAsync<AdminUpdateUserStatusResultDto>(JsonOptions, cancellationToken);
        if (result == null)
        {
            return AdminApiResult<AdminUpdateUserStatusResultDto>.Fail("Resposta vazia ao atualizar status do usuario.");
        }

        return AdminApiResult<AdminUpdateUserStatusResultDto>.Ok(result);
    }

    private async Task<ApiCallResult> SendAsync(
        HttpMethod method,
        string url,
        string accessToken,
        object? payload,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (payload != null)
        {
            request.Content = JsonContent.Create(payload);
        }

        try
        {
            var response = await client.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return ApiCallResult.Ok(response);
            }

            var error = await TryReadStatusErrorAsync(response, cancellationToken);
            return ApiCallResult.Fail(error.Message, error.ErrorCode, (int)response.StatusCode, error.Payload);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao chamar API admin de usuarios.");
            return ApiCallResult.Fail("Falha de comunicacao com a API administrativa.");
        }
    }

    private static async Task<(string Message, string? ErrorCode, AdminUpdateUserStatusResultDto? Payload)> TryReadStatusErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        AdminUpdateUserStatusResultDto? payload = null;
        try
        {
            payload = await response.Content.ReadFromJsonAsync<AdminUpdateUserStatusResultDto>(JsonOptions, cancellationToken);
        }
        catch
        {
            // ignore body parsing issue and return fallback message
        }

        var fallback = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Sessao expirada. Faca login novamente.",
            HttpStatusCode.Forbidden => "Acesso negado ao endpoint administrativo.",
            HttpStatusCode.NotFound => "Usuario nao encontrado.",
            HttpStatusCode.Conflict => payload?.ErrorMessage ?? "A operacao nao pode ser concluida para este usuario.",
            _ => $"Falha ao consultar API admin de usuarios ({(int)response.StatusCode})."
        };

        return (payload?.ErrorMessage ?? fallback, payload?.ErrorCode, payload);
    }

    private static string BuildUsersUrl(string baseUrl, AdminUsersFilterModel filters)
    {
        var normalizedRole = NormalizeRole(filters.Role);
        var query = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["searchTerm"] = string.IsNullOrWhiteSpace(filters.SearchTerm) ? null : filters.SearchTerm.Trim(),
            ["role"] = normalizedRole == "all" ? null : normalizedRole,
            ["isActive"] = filters.IsActive.HasValue ? filters.IsActive.Value.ToString().ToLowerInvariant() : null,
            ["page"] = Math.Max(1, filters.Page).ToString(CultureInfo.InvariantCulture),
            ["pageSize"] = Math.Clamp(filters.PageSize, 1, 100).ToString(CultureInfo.InvariantCulture)
        };

        var nonEmptyQuery = query
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
            .ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value, StringComparer.OrdinalIgnoreCase);

        return QueryHelpers.AddQueryString($"{baseUrl}/api/admin/users", nonEmptyQuery);
    }

    private static string NormalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return "all";
        }

        var normalized = role.Trim().ToLowerInvariant();
        return normalized switch
        {
            "client" => "Client",
            "provider" => "Provider",
            "admin" => "Admin",
            _ => "all"
        };
    }

    private class ApiCallResult
    {
        public bool Success { get; init; }
        public HttpResponseMessage? HttpResponse { get; init; }
        public string? ErrorMessage { get; init; }
        public string? ErrorCode { get; init; }
        public int? StatusCode { get; init; }
        public AdminUpdateUserStatusResultDto? ErrorPayload { get; init; }

        public static ApiCallResult Ok(HttpResponseMessage response)
            => new() { Success = true, HttpResponse = response };

        public static ApiCallResult Fail(string message, string? errorCode = null, int? statusCode = null, AdminUpdateUserStatusResultDto? payload = null)
            => new()
            {
                Success = false,
                ErrorMessage = message,
                ErrorCode = errorCode,
                StatusCode = statusCode,
                ErrorPayload = payload
            };
    }
}
