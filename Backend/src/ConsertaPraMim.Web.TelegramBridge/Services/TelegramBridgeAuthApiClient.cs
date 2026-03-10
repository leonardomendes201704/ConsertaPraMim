using System.Net.Http.Json;
using System.Text.Json;
using ConsertaPraMim.Web.TelegramBridge.Models;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public sealed class TelegramBridgeAuthApiClient : ITelegramBridgeAuthApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TelegramBridgeAuthApiClient> _logger;

    public TelegramBridgeAuthApiClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<TelegramBridgeAuthApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<(TelegramBridgeLoginResponse? Response, string? ErrorMessage)> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var apiBaseUrl = _configuration["ApiBaseUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            return (null, "ApiBaseUrl nao configurada para login.");
        }

        var client = _httpClientFactory.CreateClient();
        var url = $"{apiBaseUrl.TrimEnd('/')}/api/auth/login";
        var payload = new
        {
            Email = email,
            Password = password
        };

        try
        {
            using var response = await client.PostAsJsonAsync(url, payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                return (null, string.IsNullOrWhiteSpace(responseBody)
                    ? "Email ou senha invalidos."
                    : responseBody);
            }

            var loginResponse = await response.Content.ReadFromJsonAsync<TelegramBridgeLoginResponse>(JsonOptions, cancellationToken);
            return loginResponse == null
                ? (null, "Resposta vazia da API de autenticacao.")
                : (loginResponse, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao chamar endpoint de login da API no Telegram Bridge.");
            return (null, "Falha ao comunicar com a API de autenticacao.");
        }
    }
}
