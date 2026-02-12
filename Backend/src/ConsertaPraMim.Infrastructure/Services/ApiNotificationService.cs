using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ConsertaPraMim.Application.Interfaces;

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

    public async Task SendNotificationAsync(string recipient, string subject, string message)
    {
        var baseUrl = _configuration["ApiBaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            _logger.LogWarning("ApiBaseUrl is not configured. Notification not sent.");
            return;
        }

        var url = $"{baseUrl.TrimEnd('/')}/api/notifications";
        var payload = new { recipient, subject, message };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, payload);
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
