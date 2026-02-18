using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Admin.Models;

namespace ConsertaPraMim.Web.Admin.Services;

public class AdminAuthApiClient : IAdminAuthApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminAuthApiClient> _logger;

    public AdminAuthApiClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AdminAuthApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AdminApiResult<LoginResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = _configuration["ApiBaseUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return AdminApiResult<LoginResponse>.Fail("ApiBaseUrl nao configurada para o portal admin.");
        }

        var url = $"{baseUrl.TrimEnd('/')}/api/auth/login";
        var client = _httpClientFactory.CreateClient();

        try
        {
            using var response = await client.PostAsJsonAsync(url, request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = await TryReadErrorMessageAsync(response, cancellationToken);
                var fallback = response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => "Email ou senha invalidos.",
                    HttpStatusCode.Forbidden => "Acesso negado.",
                    _ => $"Falha ao autenticar ({(int)response.StatusCode})."
                };

                return AdminApiResult<LoginResponse>.Fail(errorMessage ?? fallback, statusCode: (int)response.StatusCode);
            }

            var payload = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions, cancellationToken);
            if (payload == null)
            {
                return AdminApiResult<LoginResponse>.Fail("Resposta vazia ao autenticar.");
            }

            return AdminApiResult<LoginResponse>.Ok(payload);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao autenticar no endpoint da API.");
            return AdminApiResult<LoginResponse>.Fail("Falha de comunicacao com a API de autenticacao.");
        }
    }

    private static async Task<string?> TryReadErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var asString = await response.Content.ReadAsStringAsync(cancellationToken);
            return string.IsNullOrWhiteSpace(asString)
                ? null
                : asString.Trim();
        }
        catch
        {
            return null;
        }
    }
}

