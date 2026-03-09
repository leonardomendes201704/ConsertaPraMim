using System.Net.Http.Json;
using ConsertaPraMim.Web.Landing.Models;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Web.Landing.Services;

public sealed class LandingAdminNotificationsClient : ILandingAdminNotificationsClient
{
    private const string DefaultInternalApiBaseUrl = "http://cpm-api:8080";

    private readonly HttpClient _httpClient;
    private readonly LandingSiteOptions _options;
    private readonly ILogger<LandingAdminNotificationsClient> _logger;

    public LandingAdminNotificationsClient(
        HttpClient httpClient,
        IOptions<LandingSiteOptions> options,
        ILogger<LandingAdminNotificationsClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task NotifyLandingAccessAsync(
        LandingAccessNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var webhookToken = _options.InternalWebhookToken?.Trim();
        if (string.IsNullOrWhiteSpace(webhookToken))
        {
            _logger.LogDebug("LandingSite:InternalWebhookToken nao configurado. Push interno de acesso nao sera enviado.");
            return;
        }

        var internalApiBaseUrl = LandingSiteOptions.NormalizeUrl(_options.InternalApiBaseUrl, DefaultInternalApiBaseUrl);
        var endpointUrl = $"{internalApiBaseUrl.TrimEnd('/')}/api/internal/landing/access";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpointUrl)
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.TryAddWithoutValidation("X-Deploy-Token", webhookToken);

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Webhook interno de acesso da landing respondeu {StatusCode}.",
                    (int)response.StatusCode);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Timeout ao publicar acesso da landing no webhook interno.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Falha HTTP ao publicar acesso da landing no webhook interno.");
        }
    }
}
