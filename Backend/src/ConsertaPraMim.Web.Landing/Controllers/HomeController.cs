using ConsertaPraMim.Web.Landing.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Web.Landing.Controllers;

public sealed class HomeController : Controller
{
    private readonly LandingSiteOptions _options;

    public HomeController(IOptions<LandingSiteOptions> options)
    {
        _options = options.Value;
    }

    public IActionResult Index()
    {
        var model = new LandingPageViewModel
        {
            CanonicalUrl = LandingSiteOptions.NormalizeUrl(_options.CanonicalUrl, "https://www.consertapramim.com"),
            ClientPortalUrl = LandingSiteOptions.NormalizeUrl(_options.ClientPortalUrl, "https://cliente.consertapramim.com"),
            ProviderPortalUrl = LandingSiteOptions.NormalizeUrl(_options.ProviderPortalUrl, "https://prestador.consertapramim.com"),
            AdminPortalUrl = LandingSiteOptions.NormalizeUrl(_options.AdminPortalUrl, "https://admin.consertapramim.com"),
            ApiSwaggerUrl = LandingSiteOptions.NormalizeUrl(_options.ApiSwaggerUrl, "https://api.consertapramim.com/swagger")
        };

        ViewData["Title"] = "ConsertaPraMim | Reparos domésticos e profissionais qualificados";
        ViewData["Description"] = "A solução inteligente para conectar clientes e profissionais em uma jornada de reparos mais clara, rápida e organizada.";
        ViewData["CanonicalUrl"] = model.CanonicalUrl;

        return View(model);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        Response.StatusCode = StatusCodes.Status500InternalServerError;
        ViewData["Title"] = "Erro interno";
        ViewData["Description"] = "A landing page encontrou um erro inesperado.";
        ViewData["CanonicalUrl"] = LandingSiteOptions.NormalizeUrl(_options.CanonicalUrl, "https://www.consertapramim.com");
        return View();
    }
}
