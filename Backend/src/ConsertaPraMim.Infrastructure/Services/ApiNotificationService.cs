using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ConsertaPraMim.Application.Interfaces;
using System.Net.Http.Headers;

namespace ConsertaPraMim.Infrastructure.Services;

public class ApiNotificationService : INotificationService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiNotificationService> _logger;

    public ApiNotificationService(HttpClient httpClient, IConfiguration configuration, ILogger<ApiNotificationService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendNotificationAsync(string recipient, string subject, string message, string? actionUrl = null)
    {
        var baseUrl = _configuration["ApiBaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            _logger.LogWarning("ApiBaseUrl is not configured. Notification not sent.");
            return;
        }

        var url = $"{baseUrl.TrimEnd('/')}/api/notifications";
        var payload = new { recipient, subject, message, actionUrl };
        var apiKey = _configuration["InternalNotifications:ApiKey"]
            ?? _configuration["JwtSettings:SecretKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("InternalNotifications:ApiKey is not configured. Notification not sent.");
            return;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.Add("X-Internal-Api-Key", apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Notification API responded with status {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call Notification API.");
        }
    }
}
