using ConsertaPraMim.Web.Admin.Services;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminPublicUrlResolverTests
{
    [Fact(DisplayName = "Admin public url resolver | Portal url | Deve preservar URL HTTPS publica configurada")]
    public void ResolvePortalUrl_ShouldPreserveConfiguredHttpsDomain()
    {
        var resolved = AdminPublicUrlResolver.ResolvePortalUrl(
            "https://cliente.consertapramim.com",
            "admin.consertapramim.com",
            "cliente",
            "http://localhost:5069/");

        Assert.Equal("https://cliente.consertapramim.com/", resolved);
    }

    [Fact(DisplayName = "Admin public url resolver | Portal url | Deve inferir subdominio publico quando candidato usa IP legado")]
    public void ResolvePortalUrl_ShouldInferSiblingDomain_WhenCandidateUsesLegacyIp()
    {
        var resolved = AdminPublicUrlResolver.ResolvePortalUrl(
            "http://187.77.48.150:5069",
            "admin.consertapramim.com",
            "cliente",
            "http://localhost:5069/");

        Assert.Equal("https://cliente.consertapramim.com/", resolved);
    }

    [Fact(DisplayName = "Admin public url resolver | Swagger url | Deve inferir host publico da API quando base usa IP legado")]
    public void ResolveSwaggerUrl_ShouldInferApiSiblingDomain_WhenCandidateUsesLegacyIp()
    {
        var resolved = AdminPublicUrlResolver.ResolveSwaggerUrl(
            "http://187.77.48.150:5193",
            "admin.consertapramim.com",
            "http://localhost:5193");

        Assert.Equal("https://api.consertapramim.com/swagger", resolved);
    }

    [Fact(DisplayName = "Admin public url resolver | Portal url | Deve manter localhost em ambiente local")]
    public void ResolvePortalUrl_ShouldKeepLocalhost_WhenRequestIsLocal()
    {
        var resolved = AdminPublicUrlResolver.ResolvePortalUrl(
            "http://localhost:5140/",
            "localhost",
            "prestador",
            "http://localhost:5140/");

        Assert.Equal("http://localhost:5140/", resolved);
    }
}
