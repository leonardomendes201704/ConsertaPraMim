using ConsertaPraMim.Web.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminApplicationsController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

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
        var displayTimeZone = ResolveDisplayTimeZone();
        var fileserverBaseUrl = ResolveFileserverApkBaseUrl();
        var webAccessUrlsByKind = ResolveWebAccessUrlsByAppKind();
        var applications = BuildCards(fileserverBaseUrl, webAccessUrlsByKind).ToArray();
        await PopulatePublicationMetadataFromApiAsync(applications, HttpContext.RequestAborted);
        await PopulatePublicationMetadataFromFileserverJsonAsync(applications, fileserverBaseUrl, HttpContext.RequestAborted);
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
            LatestPublishedAtUtc = latestPublishedAtUtc,
            DisplayTimeZoneId = displayTimeZone.Id
        };

        return View(model);
    }

    private TimeZoneInfo ResolveDisplayTimeZone()
    {
        var configuredTimeZoneId = (_configuration["Display:TimeZoneId"] ?? string.Empty).Trim();
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(configuredTimeZoneId))
        {
            candidates.Add(configuredTimeZoneId);
        }

        // Linux/IANA default for Brazil.
        candidates.Add("America/Sao_Paulo");
        // Windows fallback for local development.
        candidates.Add("E. South America Standard Time");

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(candidate);
            }
            catch (TimeZoneNotFoundException)
            {
                // Try next candidate.
            }
            catch (InvalidTimeZoneException)
            {
                // Try next candidate.
            }
        }

        return TimeZoneInfo.Utc;
    }

    private string ResolveFileserverApkBaseUrl()
    {
        var requestHost = (HttpContext.Request.Host.Host ?? string.Empty).Trim();
        var apkEnvironmentChannel = ResolveApkEnvironmentChannel();
        var configuredBaseUrl = (_configuration["Fileserver:ApkBaseUrl"] ?? string.Empty).Trim();
        if (Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var configuredBaseUri))
        {
            var configuredBuilder = new UriBuilder(configuredBaseUri);
            configuredBuilder.Path = NormalizeApkBasePath(configuredBuilder.Path, apkEnvironmentChannel);
            if (!string.IsNullOrWhiteSpace(requestHost) &&
                !IsLocalhost(requestHost) &&
                IsLocalhost(configuredBuilder.Host))
            {
                configuredBuilder.Host = requestHost;
            }

            return configuredBuilder.Uri.ToString().TrimEnd('/');
        }

        var apiBaseUrl = (_configuration["BrowserApiBaseUrl"] ?? _configuration["ApiBaseUrl"] ?? string.Empty).Trim();
        if (Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var apiBaseUri))
        {
            var fileserverUriBuilder = new UriBuilder(apiBaseUri)
            {
                Port = 8080,
                Path = NormalizeApkBasePath("/files/apks", apkEnvironmentChannel),
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
        var fallbackPath = NormalizeApkBasePath("/files/apks", apkEnvironmentChannel);
        if (!string.IsNullOrWhiteSpace(fallbackHost))
        {
            return $"{HttpContext.Request.Scheme}://{fallbackHost}:8080{fallbackPath}";
        }

        return $"http://localhost:8080{fallbackPath}";
    }

    private string? ResolveApkEnvironmentChannel()
    {
        var configuredChannel = (_configuration["Fileserver:ApkChannel"] ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(configuredChannel))
        {
            return NormalizeApkChannel(configuredChannel);
        }

        var deployProfile = (_configuration["DEPLOY_PROFILE"] ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(deployProfile))
        {
            return NormalizeApkChannel(deployProfile);
        }

        return null;
    }

    private static string NormalizeApkBasePath(string? originalPath, string? apkChannel)
    {
        var rawPath = string.IsNullOrWhiteSpace(originalPath) ? "/files/apks" : originalPath.Trim();
        var segments = rawPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        if (segments.Count == 0)
        {
            segments.Add("files");
            segments.Add("apks");
        }

        var isFilesApksPath =
            segments.Count >= 2 &&
            segments[0].Equals("files", StringComparison.OrdinalIgnoreCase) &&
            segments[1].Equals("apks", StringComparison.OrdinalIgnoreCase);

        if (!isFilesApksPath || string.IsNullOrWhiteSpace(apkChannel))
        {
            return $"/{string.Join('/', segments)}";
        }

        if (segments.Count == 2)
        {
            segments.Add(apkChannel);
            return $"/{string.Join('/', segments)}";
        }

        if (segments[2].Equals("hml", StringComparison.OrdinalIgnoreCase) ||
            segments[2].Equals("prd", StringComparison.OrdinalIgnoreCase))
        {
            segments[2] = apkChannel;
            return $"/{string.Join('/', segments)}";
        }

        return $"/{string.Join('/', segments)}";
    }

    private static string? NormalizeApkChannel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "development" or "hml" or "homolog" or "homologacao" => "hml",
            "production" or "prd" or "producao" => "prd",
            _ => null
        };
    }

    private Dictionary<string, string> ResolveWebAccessUrlsByAppKind()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["client"] = ResolveWebAccessUrl("MobileWebViews:ClientUrl", "MOBILE_CLIENT_WEBVIEW_PORT", 5181),
            ["provider"] = ResolveWebAccessUrl("MobileWebViews:ProviderUrl", "MOBILE_PROVIDER_WEBVIEW_PORT", 5182),
            ["admin"] = ResolveWebAccessUrl("MobileWebViews:AdminUrl", "MOBILE_ADMIN_WEBVIEW_PORT", 5183)
        };

        return map;
    }

    private string ResolveWebAccessUrl(string configUrlKey, string configPortKey, int fallbackPort)
    {
        var requestHost = (HttpContext.Request.Host.Host ?? string.Empty).Trim();
        var configuredUrl = (_configuration[configUrlKey] ?? string.Empty).Trim();
        if (Uri.TryCreate(configuredUrl, UriKind.Absolute, out var configuredUri))
        {
            var configuredBuilder = new UriBuilder(configuredUri);
            if (!string.IsNullOrWhiteSpace(requestHost) &&
                !IsLocalhost(requestHost) &&
                IsLocalhost(configuredBuilder.Host))
            {
                configuredBuilder.Host = requestHost;
            }

            return configuredBuilder.Uri.ToString().TrimEnd('/');
        }

        var configuredPortValue = (_configuration[configPortKey] ?? string.Empty).Trim();
        var resolvedPort = int.TryParse(configuredPortValue, out var parsedPort) ? parsedPort : fallbackPort;

        var host = ResolveExternalHostOrFallback();
        return $"http://{host}:{resolvedPort}";
    }

    private string ResolveExternalHostOrFallback()
    {
        var requestHost = (HttpContext.Request.Host.Host ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(requestHost) && !IsLocalhost(requestHost))
        {
            return requestHost;
        }

        var configuredHost = (_configuration["VPS_PUBLIC_HOST"] ?? string.Empty).Trim();
        if (Uri.TryCreate(configuredHost, UriKind.Absolute, out var configuredHostUri))
        {
            configuredHost = configuredHostUri.Host;
        }

        if (!string.IsNullOrWhiteSpace(configuredHost))
        {
            configuredHost = configuredHost.Replace("http://", string.Empty, StringComparison.OrdinalIgnoreCase)
                                           .Replace("https://", string.Empty, StringComparison.OrdinalIgnoreCase);
            configuredHost = configuredHost.Split('/', StringSplitOptions.RemoveEmptyEntries)[0];
            configuredHost = configuredHost.Split(':')[0];
        }

        return string.IsNullOrWhiteSpace(configuredHost) ? "localhost" : configuredHost;
    }

    private static IReadOnlyList<AdminApplicationCardViewModel> BuildCards(
        string fileserverBaseUrl,
        IReadOnlyDictionary<string, string> webAccessUrlsByKind)
    {
        var files = new (string AppKind, string AppName, string Variant, string RelativePath)[]
        {
            ("client", "Cliente", "Compat", "ConsertaPraMim-Cliente-compat.apk"),
            ("provider", "Prestador", "Compat", "ConsertaPraMim-Prestador-compat.apk"),
            ("admin", "Admin", "Compat", "ConsertaPraMim-Admin-compat.apk")
        };

        return files
            .Select(item => new AdminApplicationCardViewModel
            {
                AppKind = item.AppKind,
                AppName = item.AppName,
                Variant = item.Variant,
                RelativePath = item.RelativePath,
                FileName = Path.GetFileName(item.RelativePath),
                DownloadUrl = BuildDownloadUrl(fileserverBaseUrl, item.RelativePath),
                WebAccessUrl = webAccessUrlsByKind.TryGetValue(item.AppKind, out var webAccessUrl)
                    ? webAccessUrl
                    : null,
                IsDebug = item.Variant.Equals("Debug", StringComparison.OrdinalIgnoreCase)
            })
            .ToArray();
    }

    private async Task PopulatePublicationMetadataFromApiAsync(
        IReadOnlyList<AdminApplicationCardViewModel> applications,
        CancellationToken cancellationToken)
    {
        if (applications.Count == 0)
        {
            return;
        }

        var metadataUrl = ResolveMetadataUrl();
        if (string.IsNullOrWhiteSpace(metadataUrl))
        {
            return;
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

            var httpClient = _httpClientFactory.CreateClient();
            using var response = await httpClient.GetAsync(metadataUrl, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
            var payload = await JsonSerializer.DeserializeAsync<ApkPublicationMetadataResponse>(stream, JsonOptions, timeoutCts.Token);
            if (payload?.Items is null || payload.Items.Count == 0)
            {
                return;
            }

            var latestByKey = payload.Items
                .Where(item =>
                    !string.IsNullOrWhiteSpace(item.AppKind) &&
                    !string.IsNullOrWhiteSpace(item.FileName) &&
                    item.PublishedAtUtc.HasValue)
                .GroupBy(item => BuildMetadataKey(item.AppKind!, item.FileName!))
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(item => item.PublishedAtUtc!.Value)
                        .OrderByDescending(value => value)
                        .First());

            foreach (var application in applications)
            {
                var key = BuildMetadataKey(application.AppKind, application.FileName);
                if (latestByKey.TryGetValue(key, out var publishedAt))
                {
                    application.LastPublishedAtUtc = publishedAt.ToUniversalTime();
                }
            }
        }
        catch (HttpRequestException)
        {
            // Ignora falhas do endpoint de metadado para nao quebrar a tela.
        }
        catch (TaskCanceledException)
        {
            // Ignora timeout para nao quebrar a tela.
        }
        catch (JsonException)
        {
            // Ignora payload invalido para nao quebrar a tela.
        }
    }

    private async Task PopulatePublicationMetadataAsync(
        IReadOnlyList<AdminApplicationCardViewModel> applications,
        CancellationToken cancellationToken)
    {
        var pendingApplications = applications
            .Where(card => !card.LastPublishedAtUtc.HasValue)
            .ToArray();

        if (pendingApplications.Length == 0)
        {
            return;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(12));

        var httpClient = _httpClientFactory.CreateClient();
        var tasks = pendingApplications.Select(application => PopulateCardPublicationMetadataAsync(httpClient, application, timeoutCts.Token));
        await Task.WhenAll(tasks);
    }

    private async Task PopulatePublicationMetadataFromFileserverJsonAsync(
        IReadOnlyList<AdminApplicationCardViewModel> applications,
        string fileserverBaseUrl,
        CancellationToken cancellationToken)
    {
        var pendingByAppKind = applications
            .Where(card => !card.LastPublishedAtUtc.HasValue && !string.IsNullOrWhiteSpace(card.AppKind))
            .Select(card => card.AppKind)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (pendingByAppKind.Length == 0)
        {
            return;
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

            var httpClient = _httpClientFactory.CreateClient();
            foreach (var appKind in pendingByAppKind)
            {
                var metadataUrl = BuildDownloadUrl(fileserverBaseUrl, $"apk-publication-{appKind.ToLowerInvariant()}.json");
                using var response = await httpClient.GetAsync(metadataUrl, timeoutCts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
                var payload = await JsonSerializer.DeserializeAsync<ApkPublicationMetadataItemResponse>(stream, JsonOptions, timeoutCts.Token);
                if (payload?.PublishedAtUtc is null)
                {
                    continue;
                }

                var normalizedAppKind = (payload.AppKind ?? appKind).Trim().ToLowerInvariant();
                var normalizedFileName = (payload.FileName ?? string.Empty).Trim();
                var publishedAtUtc = payload.PublishedAtUtc.Value.ToUniversalTime();

                foreach (var application in applications.Where(card =>
                             !card.LastPublishedAtUtc.HasValue &&
                             card.AppKind.Equals(normalizedAppKind, StringComparison.OrdinalIgnoreCase) &&
                             (string.IsNullOrWhiteSpace(normalizedFileName) ||
                              card.FileName.Equals(normalizedFileName, StringComparison.OrdinalIgnoreCase))))
                {
                    application.LastPublishedAtUtc = publishedAtUtc;
                }
            }
        }
        catch (HttpRequestException)
        {
            // Ignora falhas para nao quebrar a tela.
        }
        catch (TaskCanceledException)
        {
            // Ignora timeout para nao quebrar a tela.
        }
        catch (JsonException)
        {
            // Ignora payload invalido para nao quebrar a tela.
        }
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

    private string? ResolveMetadataUrl()
    {
        var apiBaseUrl = (_configuration["ApiBaseUrl"] ?? string.Empty).Trim();
        if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var apiBaseUri))
        {
            return null;
        }

        var metadataUriBuilder = new UriBuilder(apiBaseUri)
        {
            Path = "/api/internal/deploy/apk-publication",
            Query = string.Empty,
            Fragment = string.Empty
        };

        var requestHost = (HttpContext.Request.Host.Host ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(requestHost) &&
            !IsLocalhost(requestHost) &&
            IsLocalhost(metadataUriBuilder.Host))
        {
            metadataUriBuilder.Host = requestHost;
        }

        return metadataUriBuilder.Uri.ToString();
    }

    private static string BuildMetadataKey(string appKind, string fileName)
        => $"{appKind.Trim().ToLowerInvariant()}|{fileName.Trim().ToLowerInvariant()}";

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

    private sealed class ApkPublicationMetadataResponse
    {
        public List<ApkPublicationMetadataItemResponse> Items { get; set; } = [];
    }

    private sealed class ApkPublicationMetadataItemResponse
    {
        public string? AppKind { get; set; }
        public string? FileName { get; set; }
        public DateTimeOffset? PublishedAtUtc { get; set; }
    }
}
