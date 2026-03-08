using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ConsertaPraMim.Tests.Unit.Frontend;

public class LandingPageRegressionTests
{
    [Fact(DisplayName = "Landing | Layout | Deve carregar Bootstrap local, OG tags, wordmark e footer sem links operacionais")]
    public void Layout_ShouldLoadLocalBootstrapOpenGraphWordmarkAndKeepFooterWithoutOperationalLinks()
    {
        var layoutContent = File.ReadAllText(GetProjectPath(
            "Backend",
            "src",
            "ConsertaPraMim.Web.Landing",
            "Views",
            "Shared",
            "_Layout.cshtml"));

        Assert.Contains("~/lib/bootstrap/dist/css/bootstrap.min.css", layoutContent);
        Assert.Contains("~/lib/bootstrap/dist/js/bootstrap.bundle.min.js", layoutContent);
        Assert.Contains("property=\"og:title\"", layoutContent);
        Assert.Contains("property=\"og:description\"", layoutContent);
        Assert.Contains("property=\"og:image\"", layoutContent);
        Assert.Contains("property=\"og:url\"", layoutContent);
        Assert.Contains("property=\"og:type\"", layoutContent);
        Assert.Contains("name=\"twitter:card\"", layoutContent);
        Assert.Contains("data-lead-capture-url=", layoutContent);
        Assert.Contains("data-initial-lead-origin=", layoutContent);
        Assert.Contains("data-visitor-id=", layoutContent);
        Assert.Contains("~/images/logo-top-bar-consertapramim.png", layoutContent);
        Assert.Contains("~/og-logo-consertapramim.png", layoutContent);
        Assert.Contains("© @DateTime.UtcNow.Year ConsertaPraMim. Todos os direitos reservados.", layoutContent);
        Assert.Contains("LandingPublicUrlResolver.ResolveApiBaseUrl", layoutContent);
        Assert.DoesNotContain("LandingPublicUrlResolver.ResolvePortalUrl", layoutContent);
        Assert.DoesNotContain("class=\"footer-links\"", layoutContent);
        Assert.DoesNotContain(">Cliente</a>", layoutContent);
        Assert.DoesNotContain(">Prestador</a>", layoutContent);
        Assert.DoesNotContain(">Admin</a>", layoutContent);
        Assert.DoesNotContain(">Swagger</a>", layoutContent);
        Assert.DoesNotContain("~/favicon.ico", layoutContent);
        Assert.DoesNotContain("<span class=\"brand-copy\">", layoutContent);
        Assert.DoesNotContain("class=\"brand-mark\"", layoutContent);
        Assert.DoesNotContain("window.landingConfig", layoutContent);
        Assert.DoesNotContain("<script>", layoutContent);
    }

    [Fact(DisplayName = "Landing | JS | Deve usar mensagens amigáveis e fechar modal após sucesso")]
    public void SiteJs_ShouldUseFriendlyMessagesAndCloseModalAfterSuccess()
    {
        var jsContent = File.ReadAllText(GetProjectPath(
            "Backend",
            "src",
            "ConsertaPraMim.Web.Landing",
            "wwwroot",
            "js",
            "site.js"));
        var indexContent = File.ReadAllText(GetProjectPath(
            "Backend",
            "src",
            "ConsertaPraMim.Web.Landing",
            "Views",
            "Home",
            "Index.cshtml"));

        Assert.Contains("Dados enviados com sucesso!", jsContent);
        Assert.Contains("Verifique sua conexão e tente novamente em instantes.", jsContent);
        Assert.Contains("leadModal.hide()", jsContent);
        Assert.Contains("data-lead-toast", indexContent);
        Assert.DoesNotContain("Failed to fetch", jsContent);
    }

    [Fact(DisplayName = "Landing | CSS | Deve respeitar hidden na seção de captação")]
    public void SiteCss_ShouldForceHiddenElementsToRemainInvisible()
    {
        var cssContent = File.ReadAllText(GetProjectPath(
            "Backend",
            "src",
            "ConsertaPraMim.Web.Landing",
            "wwwroot",
            "css",
            "site.css"));

        Assert.Contains("[hidden]", cssContent);
        Assert.Contains("display: none !important;", cssContent);
    }

    [Fact(DisplayName = "Landing | Captação | Deve renderizar modal Bootstrap sem seção no fim da página")]
    public void Index_ShouldRenderLeadCaptureInsideBootstrapModal()
    {
        var indexContent = File.ReadAllText(GetProjectPath(
            "Backend",
            "src",
            "ConsertaPraMim.Web.Landing",
            "Views",
            "Home",
            "Index.cshtml"));

        Assert.DoesNotContain("data-lead-tab", indexContent);
        Assert.DoesNotContain("role=\"tablist\"", indexContent);
        Assert.Contains("id=\"leadCaptureModal\"", indexContent);
        Assert.Contains("class=\"modal fade lead-modal\"", indexContent);
        Assert.Contains("modal-dialog modal-xl modal-dialog-centered modal-dialog-scrollable", indexContent);
        Assert.Contains("class=\"section-heading lead-heading mb-0\"", indexContent);
        Assert.Contains("Conte-nos rapidamente um pouco sobre você e o que você precisa.", indexContent);
        Assert.Contains("Conte rapidamente um pouco sobre você e como deseja atuar.", indexContent);
        Assert.DoesNotContain("O formulario abaixo aparece apenas quando um dos CTAs principais e acionado.", indexContent);
        Assert.DoesNotContain("Lead cliente", indexContent);
        Assert.DoesNotContain("Lead prestador", indexContent);
        Assert.DoesNotContain("id=\"captacao\"", indexContent);

        var shellIndex = indexContent.IndexOf("data-lead-shell", StringComparison.Ordinal);
        var headingIndex = indexContent.IndexOf("class=\"section-heading lead-heading mb-0\"", StringComparison.Ordinal);

        Assert.True(shellIndex >= 0);
        Assert.True(headingIndex > shellIndex);
    }

    [Fact(DisplayName = "Landing | Testemunhos | Deve renderizar 5 clientes e 5 prestadores")]
    public void Index_ShouldRenderTenTestimonialsWithBalancedOrigins()
    {
        var indexContent = File.ReadAllText(GetProjectPath(
            "Backend",
            "src",
            "ConsertaPraMim.Web.Landing",
            "Views",
            "Home",
            "Index.cshtml"));

        Assert.Contains("Quem entra na plataforma percebe a diferença logo no primeiro contato.", indexContent);

        var clientBlock = Regex.Match(
            indexContent,
            @"var clientTestimonials = new\[\]\s*\{(?<items>.*?)\};\s*var providerTestimonials",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        var providerBlock = Regex.Match(
            indexContent,
            @"var providerTestimonials = new\[\]\s*\{(?<items>.*?)\};\s*\}",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);

        Assert.True(clientBlock.Success);
        Assert.True(providerBlock.Success);

        var clientCount = Regex.Matches(clientBlock.Groups["items"].Value, @"new\s*\{\s*Name\s*=", RegexOptions.CultureInvariant).Count;
        var providerCount = Regex.Matches(providerBlock.Groups["items"].Value, @"new\s*\{\s*Name\s*=", RegexOptions.CultureInvariant).Count;

        Assert.Equal(5, clientCount);
        Assert.Equal(5, providerCount);
    }

    private static string GetProjectPath(params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !Directory.Exists(Path.Combine(current.FullName, "Backend")))
        {
            current = current.Parent;
        }

        Assert.NotNull(current);
        return Path.Combine(new[] { current!.FullName }.Concat(segments).ToArray());
    }
}
