using ConsertaPraMim.Web.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Headers;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminApplicationsController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public AdminApplicationsController(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var fileserverBaseUrl = ResolveFileserverApkBaseUrl();
        var applications = BuildCards(fileserverBaseUrl).ToArray();
        await PopulatePublicationMetadataAsync(applications, HttpContext.RequestAborted);

        DateTimeOffset? latestPublishedAtUtc = null;
        foreach (var publishedAt in applications.Where(card => card.LastPublishedAtUtc.HasValue).Select(card => card.LastPublishedAtUtc!.Value))
        {
            if (!latestPublishedAtUtc.HasValue || publishedAt > latestPublishedAtUtc.Value)
            {
                latestPublishedAtUtc = publishedAt;
            }
        }

        var model = new AdminApplicationsViewModel
        {
            FileserverBaseUrl = fileserverBaseUrl,
            Applications = applications,
            LatestPublishedAtUtc = latestPublishedAtUtc
        };

        return View(model);
    }

    private string ResolveFileserverApkBaseUrl()
    {
        var requestHost = (HttpContext.Request.Host.Host ?? string.Empty).Trim();
        var configuredBaseUrl = (_configuration["Fileserver:ApkBaseUrl"] ?? string.Empty).Trim();
        if (Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var configuredBaseUri))
        {
            var configuredBuilder = new UriBuilder(configuredBaseUri);
            if (!string.IsNullOrWhiteSpace(requestHost) &&
                !IsLocalhost(requestHost) &&
                IsLocalhost(configuredBuilder.Host))
            {
                configuredBuilder.Host = requestHost;
            }

            return configuredBuilder.Uri.ToString().TrimEnd('/');
        }

        var apiBaseUrl = (_configuration["ApiBaseUrl"] ?? string.Empty).Trim();
        if (Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var apiBaseUri))
        {
            var fileserverUriBuilder = new UriBuilder(apiBaseUri)
            {
                Port = 8080,
                Path = "/files/apks",
                Query = string.Empty,
                Fragment = string.Empty
            };

            if (!string.IsNullOrWhiteSpace(requestHost) &&
                !IsLocalhost(requestHost) &&
                IsLocalhost(fileserverUriBuilder.Host))
            {
                fileserverUriBuilder.Host = requestHost;
            }

            return fileserverUriBuilder.Uri.ToString().TrimEnd('/');
        }

        var fallbackHost = (HttpContext.Request.Host.Host ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(fallbackHost))
        {
            return $"{HttpContext.Request.Scheme}://{fallbackHost}:8080/files/apks";
        }

        return "http://localhost:8080/files/apks";
    }

    private static IReadOnlyList<AdminApplicationCardViewModel> BuildCards(string fileserverBaseUrl)
    {
        var files = new (string AppName, string Variant, string RelativePath)[]
        {
            ("Cliente", "Compat", "ConsertaPraMim-Cliente-compat.apk"),
            ("Prestador", "Compat", "ConsertaPraMim-Prestador-compat.apk"),
            ("Admin", "Compat", "ConsertaPraMim-Admin-compat.apk")
        };

        return files
            .Select(item => new AdminApplicationCardViewModel
            {
                AppName = item.AppName,
                Variant = item.Variant,
                RelativePath = item.RelativePath,
                FileName = Path.GetFileName(item.RelativePath),
                DownloadUrl = BuildDownloadUrl(fileserverBaseUrl, item.RelativePath),
                IsDebug = item.Variant.Equals("Debug", StringComparison.OrdinalIgnoreCase)
            })
            .ToArray();
    }

    private async Task PopulatePublicationMetadataAsync(
        IReadOnlyList<AdminApplicationCardViewModel> applications,
        CancellationToken cancellationToken)
    {
        if (applications.Count == 0)
        {
            return;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(12));

        var httpClient = _httpClientFactory.CreateClient();
        var tasks = applications.Select(application => PopulateCardPublicationMetadataAsync(httpClient, application, timeoutCts.Token));
        await Task.WhenAll(tasks);
    }

    private static async Task PopulateCardPublicationMetadataAsync(
        HttpClient httpClient,
        AdminApplicationCardViewModel application,
        CancellationToken cancellationToken)
    {
        try
        {
            using var headRequest = new HttpRequestMessage(HttpMethod.Head, application.DownloadUrl);
            using var headResponse = await httpClient.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (TryGetLastModified(headResponse, out var lastModified))
            {
                application.LastPublishedAtUtc = lastModified.ToUniversalTime();
                return;
            }

            var shouldFallbackToGet =
                headResponse.StatusCode == HttpStatusCode.MethodNotAllowed ||
                headResponse.StatusCode == HttpStatusCode.NotImplemented ||
                headResponse.IsSuccessStatusCode;

            if (!shouldFallbackToGet)
            {
                return;
            }
        }
        catch (HttpRequestException)
        {
            return;
        }
        catch (TaskCanceledException)
        {
            return;
        }

        try
        {
            using var getRequest = new HttpRequestMessage(HttpMethod.Get, application.DownloadUrl);
            getRequest.Headers.Range = new RangeHeaderValue(0, 0);

            using var getResponse = await httpClient.SendAsync(getRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (TryGetLastModified(getResponse, out var lastModified))
            {
                application.LastPublishedAtUtc = lastModified.ToUniversalTime();
            }
        }
        catch (HttpRequestException)
        {
            // Ignora falhas de metadata para nao quebrar a tela.
        }
        catch (TaskCanceledException)
        {
            // Ignora timeout para nao quebrar a tela.
        }
    }

    private static bool TryGetLastModified(HttpResponseMessage response, out DateTimeOffset lastModified)
    {
        if (response.Content.Headers.LastModified.HasValue)
        {
            lastModified = response.Content.Headers.LastModified.Value;
            return true;
        }

        if (response.Headers.TryGetValues("Last-Modified", out var headerValues))
        {
            var rawValue = headerValues.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(rawValue) && DateTimeOffset.TryParse(rawValue, out var parsedValue))
            {
                lastModified = parsedValue;
                return true;
            }
        }

        lastModified = default;
        return false;
    }

    private static string BuildDownloadUrl(string fileserverBaseUrl, string relativePath)
    {
        var normalizedBase = fileserverBaseUrl.TrimEnd('/');
        var normalizedRelative = relativePath.TrimStart('/');
        return $"{normalizedBase}/{normalizedRelative}";
    }

    private static bool IsLocalhost(string host)
        => host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
           || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
           || host.Equals("::1", StringComparison.OrdinalIgnoreCase);
}
