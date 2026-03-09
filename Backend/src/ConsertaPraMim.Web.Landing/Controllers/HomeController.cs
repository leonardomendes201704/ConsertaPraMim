using ConsertaPraMim.Web.Landing.Models;
using ConsertaPraMim.Web.Landing.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Web.Landing.Controllers;

public sealed class HomeController : Controller
{
    private const string DefaultCanonicalUrl = "https://www.consertapramim.com";
    private const string DefaultOgTitle = "ConsertaPraMim \u2013 Encontre profissionais de confianca";
    private const string DefaultOgDescription = "Conectamos voce a profissionais de manutencao e reparos perto de voce.";
    private const string DefaultOgImagePath = "/og-logo-consertapramim.png";
    private const string VisitorIdCookieName = "cpm_landing_vid";
    private static readonly TimeSpan VisitorIdCookieLifetime = TimeSpan.FromDays(180);

    private readonly LandingSiteOptions _options;
    private readonly ILandingAdminNotificationsClient _landingAdminNotificationsClient;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        IOptions<LandingSiteOptions> options,
        ILandingAdminNotificationsClient landingAdminNotificationsClient,
        ILogger<HomeController> logger)
    {
        _options = options.Value;
        _landingAdminNotificationsClient = landingAdminNotificationsClient;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var visitorId = EnsureVisitorId();
        var sessionId = GenerateSessionId();
        await TryNotifyLandingAccessAsync(visitorId, sessionId);
        return RenderLanding(visitorId, sessionId);
    }

    public async Task<IActionResult> Cliente()
    {
        var visitorId = EnsureVisitorId();
        var sessionId = GenerateSessionId();
        await TryNotifyLandingAccessAsync(visitorId, sessionId, "client");
        return RenderLanding(visitorId, sessionId, "client");
    }

    public async Task<IActionResult> Prestador()
    {
        var visitorId = EnsureVisitorId();
        var sessionId = GenerateSessionId();
        await TryNotifyLandingAccessAsync(visitorId, sessionId, "provider");
        return RenderLanding(visitorId, sessionId, "provider");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        Response.StatusCode = StatusCodes.Status500InternalServerError;
        ViewData["Title"] = "Erro interno";
        ViewData["Description"] = "A landing page encontrou um erro inesperado.";
        ViewData["CanonicalUrl"] = LandingSiteOptions.NormalizeUrl(_options.CanonicalUrl, DefaultCanonicalUrl);
        return View();
    }

    private IActionResult RenderLanding(string visitorId, string sessionId, string? initialLeadOrigin = null)
    {
        var canonicalUrl = LandingSiteOptions.NormalizeUrl(_options.CanonicalUrl, DefaultCanonicalUrl);
        var canonicalUrlWithSlash = canonicalUrl.TrimEnd('/') + "/";
        var currentUrl = initialLeadOrigin switch
        {
            "client" => canonicalUrl.TrimEnd('/') + "/Cliente",
            "provider" => canonicalUrl.TrimEnd('/') + "/Prestador",
            _ => canonicalUrlWithSlash
        };

        var apiBaseUrl = LandingSiteOptions.ResolveApiBaseUrl(
            _options.ApiBaseUrl,
            _options.ApiSwaggerUrl,
            "https://api.consertapramim.com");
        var requestHost = Request.Host.Host;
        var resolvedApiBaseUrl = LandingPublicUrlResolver.ResolveApiBaseUrl(
            apiBaseUrl,
            requestHost,
            "https://api.consertapramim.com");

        var model = new LandingPageViewModel
        {
            CanonicalUrl = canonicalUrl,
            ClientPortalUrl = LandingPublicUrlResolver.ResolvePortalUrl(_options.ClientPortalUrl, requestHost, "cliente", "https://cliente.consertapramim.com"),
            ProviderPortalUrl = LandingPublicUrlResolver.ResolvePortalUrl(_options.ProviderPortalUrl, requestHost, "prestador", "https://prestador.consertapramim.com"),
            AdminPortalUrl = LandingPublicUrlResolver.ResolvePortalUrl(_options.AdminPortalUrl, requestHost, "admin", "https://admin.consertapramim.com"),
            ApiBaseUrl = resolvedApiBaseUrl,
            ApiSwaggerUrl = LandingPublicUrlResolver.ResolveSwaggerUrl(_options.ApiSwaggerUrl ?? _options.ApiBaseUrl, requestHost, "https://api.consertapramim.com"),
            LeadCaptureUrl = resolvedApiBaseUrl.TrimEnd('/') + "/api/landing-leads/public",
            AnalyticsConfigUrl = resolvedApiBaseUrl.TrimEnd('/') + "/api/landing-analytics/public/config",
            TelemetryUrl = resolvedApiBaseUrl.TrimEnd('/') + "/api/landing-analytics/public/events",
            VisitorId = visitorId,
            SessionId = sessionId,
            InitialLeadOrigin = initialLeadOrigin
        };

        ViewData["Title"] = DefaultOgTitle;
        ViewData["Description"] = DefaultOgDescription;
        ViewData["CanonicalUrl"] = canonicalUrlWithSlash;
        ViewData["OpenGraphTitle"] = DefaultOgTitle;
        ViewData["OpenGraphDescription"] = DefaultOgDescription;
        ViewData["OpenGraphImage"] = canonicalUrl.TrimEnd('/') + DefaultOgImagePath;
        ViewData["OpenGraphUrl"] = currentUrl;
        ViewData["OpenGraphType"] = "website";
        ViewData["TwitterCard"] = "summary_large_image";
        ViewData["InitialLeadOrigin"] = initialLeadOrigin;
        ViewData["LandingVisitorId"] = visitorId;
        ViewData["LandingSessionId"] = sessionId;

        return View("Index", model);
    }

    private async Task TryNotifyLandingAccessAsync(string visitorId, string sessionId, string? initialLeadOrigin = null)
    {
        try
        {
            await _landingAdminNotificationsClient.NotifyLandingAccessAsync(
                new LandingAccessNotificationRequest(
                    VisitorId: visitorId,
                    SessionId: sessionId,
                    CurrentUrl: BuildCurrentAbsoluteUrl(),
                    Path: Request.Path.HasValue ? Request.Path.Value : "/",
                    Host: Request.Host.Value,
                    Scheme: Request.Scheme,
                    InitialLeadOrigin: initialLeadOrigin,
                    IpAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                    ForwardedFor: Request.Headers["X-Forwarded-For"].FirstOrDefault(),
                    UserAgent: Request.Headers.UserAgent.ToString(),
                    AcceptLanguage: Request.Headers.AcceptLanguage.ToString(),
                    RefererUrl: Request.Headers.Referer.ToString()),
                HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao publicar acesso da landing para o canal interno de notificacoes.");
        }
    }

    private static string GenerateSessionId()
        => Guid.NewGuid().ToString("N");

    private string EnsureVisitorId()
    {
        if (Request.Cookies.TryGetValue(VisitorIdCookieName, out var existingVisitorId) &&
            IsValidVisitorId(existingVisitorId))
        {
            return existingVisitorId.Trim();
        }

        var newVisitorId = Guid.NewGuid().ToString("N");
        Response.Cookies.Append(
            VisitorIdCookieName,
            newVisitorId,
            new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.Add(VisitorIdCookieLifetime)
            });

        return newVisitorId;
    }

    private static bool IsValidVisitorId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        return normalized.Length <= 80 && normalized.All(char.IsLetterOrDigit);
    }

    private string BuildCurrentAbsoluteUrl()
    {
        return $"{Request.Scheme}://{Request.Host}{Request.Path}{Request.QueryString}";
    }
}
