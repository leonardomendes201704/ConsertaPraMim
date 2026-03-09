using ConsertaPraMim.Web.Landing.Services;

namespace ConsertaPraMim.Tests.Unit.Services;

public class LandingPublicUrlResolverTests
{
    [Fact(DisplayName = "Landing public url resolver | Api base url | Deve preservar URL HTTPS publica configurada")]
    public void ResolveApiBaseUrl_ShouldPreserveConfiguredHttpsDomain()
    {
        var resolved = LandingPublicUrlResolver.ResolveApiBaseUrl(
            "https://api.consertapramim.com",
            "www.consertapramim.com",
            "http://localhost:5193");

        Assert.Equal("https://api.consertapramim.com", resolved);
    }

    [Fact(DisplayName = "Landing public url resolver | Api base url | Deve inferir dominio publico quando candidato usa IP legado")]
    public void ResolveApiBaseUrl_ShouldInferApiSiblingDomain_WhenCandidateUsesLegacyIp()
    {
        var resolved = LandingPublicUrlResolver.ResolveApiBaseUrl(
            "http://187.77.48.150:5193",
            "www.consertapramim.com",
            "http://localhost:5193");

        Assert.Equal("https://api.consertapramim.com", resolved);
    }

    [Fact(DisplayName = "Landing public url resolver | Portal url | Deve inferir subdominio de portal a partir do host publico")]
    public void ResolvePortalUrl_ShouldInferPortalSiblingDomain_FromPublicHost()
    {
        var resolved = LandingPublicUrlResolver.ResolvePortalUrl(
            "http://187.77.48.150:5140",
            "www.consertapramim.com",
            "prestador",
            "http://localhost:5140");

        Assert.Equal("https://prestador.consertapramim.com/", resolved);
    }

    [Fact(DisplayName = "Landing public url resolver | Api base url | Deve manter localhost em ambiente local")]
    public void ResolveApiBaseUrl_ShouldKeepLocalhost_WhenRequestIsLocal()
    {
        var resolved = LandingPublicUrlResolver.ResolveApiBaseUrl(
            "http://localhost:5193",
            "localhost",
            "http://localhost:5193");

        Assert.Equal("http://localhost:5193", resolved);
    }
}
