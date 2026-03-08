using ConsertaPraMim.Web.Landing.Models;
using ConsertaPraMim.Web.Landing.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Web.Landing.Controllers;

public sealed class HomeController : Controller
{
    private const string DefaultCanonicalUrl = "https://www.consertapramim.com";
    private const string DefaultOgTitle = "ConsertaPraMim – Encontre profissionais de confiança";
    private const string DefaultOgDescription = "Conectamos você a profissionais de manutenção e reparos perto de você.";
    private const string DefaultOgImagePath = "/og-logo-consertapramim.png";

    private readonly LandingSiteOptions _options;

    public HomeController(IOptions<LandingSiteOptions> options)
    {
        _options = options.Value;
    }

    public IActionResult Index()
    {
        return RenderLanding();
    }

    public IActionResult Cliente()
    {
        return RenderLanding("client");
    }

    public IActionResult Prestador()
    {
        return RenderLanding("provider");
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

    private IActionResult RenderLanding(string? initialLeadOrigin = null)
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

        return View("Index", model);
    }
}
