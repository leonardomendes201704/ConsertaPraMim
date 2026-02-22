using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Admin.Models;
using Microsoft.AspNetCore.WebUtilities;

namespace ConsertaPraMim.Web.Admin.Services;

public class AdminMailboxApiClient : IAdminMailboxApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminMailboxApiClient> _logger;

    public AdminMailboxApiClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AdminMailboxApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AdminApiResult<AdminMailboxSettingsDto>> GetSettingsAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminMailboxSettingsDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var url = $"{baseUrl}/api/admin/mailbox/settings";
        var response = await SendAsync(HttpMethod.Get, url, accessToken, null, cancellationToken);
        if (!response.Success || response.HttpResponse == null)
        {
            return AdminApiResult<AdminMailboxSettingsDto>.Fail(
                response.ErrorMessage ?? "Falha ao consultar configuracoes do webmail.",
                response.ErrorCode,
                response.StatusCode);
        }

        var payload = await response.HttpResponse.Content.ReadFromJsonAsync<AdminMailboxSettingsDto>(JsonOptions, cancellationToken);
        return payload == null
            ? AdminApiResult<AdminMailboxSettingsDto>.Fail("Resposta vazia ao consultar configuracoes do webmail.")
            : AdminApiResult<AdminMailboxSettingsDto>.Ok(payload);
    }

    public async Task<AdminApiResult<AdminOperationResultDto>> UpsertSettingsAsync(
        AdminMailboxUpsertSettingsRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminOperationResultDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var url = $"{baseUrl}/api/admin/mailbox/settings";
        var response = await SendAsync(HttpMethod.Put, url, accessToken, request, cancellationToken);
        if (!response.Success)
        {
            return AdminApiResult<AdminOperationResultDto>.Fail(
                response.ErrorMessage ?? "Falha ao salvar configuracoes do webmail.",
                response.ErrorCode,
                response.StatusCode);
        }

        return AdminApiResult<AdminOperationResultDto>.Ok(new AdminOperationResultDto(true));
    }

    public async Task<AdminApiResult<IReadOnlyList<AdminMailboxRecipientDto>>> GetRecipientsAsync(
        string? role,
        string? search,
        int take,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<IReadOnlyList<AdminMailboxRecipientDto>>.Fail("ApiBaseUrl nao configurada.");
        }

        var query = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["role"] = string.IsNullOrWhiteSpace(role) ? null : role.Trim().ToLowerInvariant(),
            ["search"] = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            ["take"] = Math.Clamp(take, 1, 200).ToString()
        };

        var url = QueryHelpers.AddQueryString($"{baseUrl}/api/admin/mailbox/recipients", FilterQuery(query));
        var response = await SendAsync(HttpMethod.Get, url, accessToken, null, cancellationToken);
        if (!response.Success || response.HttpResponse == null)
        {
            return AdminApiResult<IReadOnlyList<AdminMailboxRecipientDto>>.Fail(
                response.ErrorMessage ?? "Falha ao consultar destinatarios.",
                response.ErrorCode,
                response.StatusCode);
        }

        var payload = await response.HttpResponse.Content.ReadFromJsonAsync<List<AdminMailboxRecipientDto>>(JsonOptions, cancellationToken);
        return payload == null
            ? AdminApiResult<IReadOnlyList<AdminMailboxRecipientDto>>.Fail("Resposta vazia ao consultar destinatarios.")
            : AdminApiResult<IReadOnlyList<AdminMailboxRecipientDto>>.Ok(payload);
    }

    public async Task<AdminApiResult<AdminMailboxListResponseDto>> GetMessagesAsync(
        AdminMailboxListQueryDto query,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminMailboxListResponseDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var queryParams = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["folder"] = string.IsNullOrWhiteSpace(query.Folder) ? "inbox" : query.Folder.Trim().ToLowerInvariant(),
            ["search"] = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            ["page"] = Math.Max(1, query.Page).ToString(),
            ["pageSize"] = Math.Clamp(query.PageSize, 1, 200).ToString()
        };

        var url = QueryHelpers.AddQueryString($"{baseUrl}/api/admin/mailbox/messages", FilterQuery(queryParams));
        var response = await SendAsync(HttpMethod.Get, url, accessToken, null, cancellationToken);
        if (!response.Success || response.HttpResponse == null)
        {
            return AdminApiResult<AdminMailboxListResponseDto>.Fail(
                response.ErrorMessage ?? "Falha ao consultar mensagens.",
                response.ErrorCode,
                response.StatusCode);
        }

        var payload = await response.HttpResponse.Content.ReadFromJsonAsync<AdminMailboxListResponseDto>(JsonOptions, cancellationToken);
        return payload == null
            ? AdminApiResult<AdminMailboxListResponseDto>.Fail("Resposta vazia ao consultar mensagens.")
            : AdminApiResult<AdminMailboxListResponseDto>.Ok(payload);
    }

    public async Task<AdminApiResult<AdminMailboxMessageDetailsDto>> GetMessageByIdAsync(
        string messageId,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return AdminApiResult<AdminMailboxMessageDetailsDto>.Fail("Mensagem invalida.");
        }

        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminMailboxMessageDetailsDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var url = $"{baseUrl}/api/admin/mailbox/messages/{Uri.EscapeDataString(messageId.Trim())}";
        var response = await SendAsync(HttpMethod.Get, url, accessToken, null, cancellationToken);
        if (!response.Success || response.HttpResponse == null)
        {
            return AdminApiResult<AdminMailboxMessageDetailsDto>.Fail(
                response.ErrorMessage ?? "Falha ao consultar mensagem.",
                response.ErrorCode,
                response.StatusCode);
        }

        var payload = await response.HttpResponse.Content.ReadFromJsonAsync<AdminMailboxMessageDetailsDto>(JsonOptions, cancellationToken);
        return payload == null
            ? AdminApiResult<AdminMailboxMessageDetailsDto>.Fail("Resposta vazia ao consultar mensagem.")
            : AdminApiResult<AdminMailboxMessageDetailsDto>.Ok(payload);
    }

    public async Task<AdminApiResult<AdminMailboxMessageDetailsDto>> MarkMessageReadAsync(
        string messageId,
        bool isRead,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return AdminApiResult<AdminMailboxMessageDetailsDto>.Fail("Mensagem invalida.");
        }

        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminMailboxMessageDetailsDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var url = $"{baseUrl}/api/admin/mailbox/messages/{Uri.EscapeDataString(messageId.Trim())}/read";
        var payload = new AdminMailboxMarkReadRequestDto(isRead);
        var response = await SendAsync(HttpMethod.Patch, url, accessToken, payload, cancellationToken);
        if (!response.Success || response.HttpResponse == null)
        {
            return AdminApiResult<AdminMailboxMessageDetailsDto>.Fail(
                response.ErrorMessage ?? "Falha ao atualizar status da mensagem.",
                response.ErrorCode,
                response.StatusCode);
        }

        var result = await response.HttpResponse.Content.ReadFromJsonAsync<AdminMailboxMessageDetailsDto>(JsonOptions, cancellationToken);
        return result == null
            ? AdminApiResult<AdminMailboxMessageDetailsDto>.Fail("Resposta vazia ao atualizar status da mensagem.")
            : AdminApiResult<AdminMailboxMessageDetailsDto>.Ok(result);
    }

    public async Task<AdminApiResult<AdminMailboxMessageDetailsDto>> SendAsync(
        AdminMailboxSendRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminMailboxMessageDetailsDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var url = $"{baseUrl}/api/admin/mailbox/send";
        var response = await SendAsync(HttpMethod.Post, url, accessToken, request, cancellationToken);
        if (!response.Success || response.HttpResponse == null)
        {
            return AdminApiResult<AdminMailboxMessageDetailsDto>.Fail(
                response.ErrorMessage ?? "Falha ao enviar email.",
                response.ErrorCode,
                response.StatusCode);
        }

        var payload = await response.HttpResponse.Content.ReadFromJsonAsync<SendEmailResponse>(JsonOptions, cancellationToken);
        if (payload == null || !payload.Success || payload.Message == null)
        {
            return AdminApiResult<AdminMailboxMessageDetailsDto>.Fail(
                payload?.ErrorMessage ?? "Resposta invalida ao enviar email.",
                payload?.ErrorCode);
        }

        return AdminApiResult<AdminMailboxMessageDetailsDto>.Ok(payload.Message);
    }

    public async Task<AdminApiResult<AdminMailboxSyncResultDto>> SyncAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = GetApiBaseUrl();
        if (baseUrl == null)
        {
            return AdminApiResult<AdminMailboxSyncResultDto>.Fail("ApiBaseUrl nao configurada.");
        }

        var url = $"{baseUrl}/api/admin/mailbox/sync";
        var response = await SendAsync(HttpMethod.Post, url, accessToken, null, cancellationToken);
        if (!response.Success || response.HttpResponse == null)
        {
            return AdminApiResult<AdminMailboxSyncResultDto>.Fail(
                response.ErrorMessage ?? "Falha ao sincronizar inbox.",
                response.ErrorCode,
                response.StatusCode);
        }

        var payload = await response.HttpResponse.Content.ReadFromJsonAsync<AdminMailboxSyncResultDto>(JsonOptions, cancellationToken);
        return payload == null
            ? AdminApiResult<AdminMailboxSyncResultDto>.Fail("Resposta vazia ao sincronizar inbox.")
            : AdminApiResult<AdminMailboxSyncResultDto>.Ok(payload);
    }

    private string? GetApiBaseUrl()
    {
        var apiBaseUrl = _configuration["ApiBaseUrl"];
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            _logger.LogWarning("ApiBaseUrl nao configurada para Web.Admin.");
            return null;
        }

        return apiBaseUrl.TrimEnd('/');
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
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (payload != null)
        {
            request.Content = JsonContent.Create(payload);
        }

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao chamar endpoint admin mailbox {Method} {Url}", method, url);
            return ApiCallResult.Fail("Falha de conexao com a API.", "admin_mailbox_http_error");
        }

        if (response.IsSuccessStatusCode)
        {
            return ApiCallResult.Ok(response);
        }

        string? errorCode = null;
        string? errorMessage = null;
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var operation = await JsonSerializer.DeserializeAsync<OperationResponse>(stream, JsonOptions, cancellationToken);
            if (operation != null)
            {
                errorCode = operation.ErrorCode;
                errorMessage = operation.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Nao foi possivel desserializar erro de admin mailbox.");
        }

        return ApiCallResult.Fail(
            errorMessage ?? $"Falha ao chamar API (HTTP {(int)response.StatusCode}).",
            errorCode,
            (int)response.StatusCode);
    }

    private static Dictionary<string, string?> FilterQuery(Dictionary<string, string?> query)
    {
        return query
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    private class OperationResponse
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
    }

    private sealed class SendEmailResponse : OperationResponse
    {
        public AdminMailboxMessageDetailsDto? Message { get; set; }
    }

    private sealed class ApiCallResult
    {
        public bool Success { get; init; }
        public HttpResponseMessage? HttpResponse { get; init; }
        public string? ErrorCode { get; init; }
        public string? ErrorMessage { get; init; }
        public int? StatusCode { get; init; }

        public static ApiCallResult Ok(HttpResponseMessage response)
            => new()
            {
                Success = true,
                HttpResponse = response
            };

        public static ApiCallResult Fail(string errorMessage, string? errorCode = null, int? statusCode = null)
            => new()
            {
                Success = false,
                ErrorMessage = errorMessage,
                ErrorCode = errorCode,
                StatusCode = statusCode
            };
    }
}
