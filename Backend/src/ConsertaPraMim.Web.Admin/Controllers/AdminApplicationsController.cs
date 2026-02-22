using ConsertaPraMim.Web.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminApplicationsController : Controller
{
    private readonly IConfiguration _configuration;

    public AdminApplicationsController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var fileserverBaseUrl = ResolveFileserverApkBaseUrl();
        var model = new AdminApplicationsViewModel
        {
            FileserverBaseUrl = fileserverBaseUrl,
            Applications = BuildCards(fileserverBaseUrl)
        };

        return View(model);
    }

    private string ResolveFileserverApkBaseUrl()
    {
        var configuredBaseUrl = (_configuration["Fileserver:ApkBaseUrl"] ?? string.Empty).Trim();
        if (Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var configuredBaseUri))
        {
            return configuredBaseUri.ToString().TrimEnd('/');
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

            var requestHost = (HttpContext.Request.Host.Host ?? string.Empty).Trim();
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
            ("Cliente", "Debug", "debug/ConsertaPraMim-Cliente-debug.apk"),
            ("Prestador", "Compat", "ConsertaPraMim-Prestador-compat.apk"),
            ("Prestador", "Debug", "debug/ConsertaPraMim-Prestador-debug.apk"),
            ("Admin", "Compat", "ConsertaPraMim-Admin-compat.apk"),
            ("Admin", "Debug", "debug/ConsertaPraMim-Admin-debug.apk")
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
