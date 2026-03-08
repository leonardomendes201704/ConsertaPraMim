using ConsertaPraMim.Web.Provider.Services;

namespace ConsertaPraMim.Tests.Unit.Services;

public class ProviderPublicUrlResolverTests
{
    [Fact(DisplayName = "Provider public url resolver | Api base url | Deve preservar URL HTTPS publica configurada")]
    public void ResolveApiBaseUrl_ShouldPreserveConfiguredHttpsDomain()
    {
        var resolved = ProviderPublicUrlResolver.ResolveApiBaseUrl(
            "https://api.consertapramim.com",
            "prestador.consertapramim.com",
            "http://localhost:5193");

        Assert.Equal("https://api.consertapramim.com", resolved);
    }

    [Fact(DisplayName = "Provider public url resolver | Api base url | Deve inferir dominio publico quando candidato usa IP legado")]
    public void ResolveApiBaseUrl_ShouldInferApiSiblingDomain_WhenCandidateUsesLegacyIp()
    {
        var resolved = ProviderPublicUrlResolver.ResolveApiBaseUrl(
            "http://187.77.48.150:5193",
            "prestador.consertapramim.com",
            "http://localhost:5193");

        Assert.Equal("https://api.consertapramim.com", resolved);
    }

    [Fact(DisplayName = "Provider public url resolver | Api base url | Deve manter localhost em ambiente local")]
    public void ResolveApiBaseUrl_ShouldKeepLocalhost_WhenRequestIsLocal()
    {
        var resolved = ProviderPublicUrlResolver.ResolveApiBaseUrl(
            "http://localhost:5193",
            "localhost",
            "http://localhost:5193");

        Assert.Equal("http://localhost:5193", resolved);
    }
}
