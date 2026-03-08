using System.IO;
using System.Linq;
using Xunit;

namespace ConsertaPraMim.Tests.Unit.Frontend;

public class LandingPageRegressionTests
{
    [Fact(DisplayName = "Landing | Layout | Nao deve usar script inline para configuracao da captura")]
    public void Layout_ShouldAvoidInlineScriptConfiguration()
    {
        var layoutContent = File.ReadAllText(GetProjectPath(
            "Backend",
            "src",
            "ConsertaPraMim.Web.Landing",
            "Views",
            "Shared",
            "_Layout.cshtml"));

        Assert.DoesNotContain("window.landingConfig", layoutContent);
        Assert.Contains("data-lead-capture-url=", layoutContent);
        Assert.DoesNotContain("<script>", layoutContent);
    }

    [Fact(DisplayName = "Landing | CSS | Deve respeitar hidden na secao de captacao")]
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

    [Fact(DisplayName = "Landing | Captacao | Deve abrir sem toggles e com heading oculto ate o CTA")]
    public void Index_ShouldKeepLeadHeadingInsideHiddenShell()
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
        Assert.Contains("class=\"section-heading lead-heading\"", indexContent);
        Assert.Contains("Conte-nos rapidamente um pouco sobre você e o que você precisa.", indexContent);
        Assert.Contains("Conte rapidamente um pouco sobre você e como deseja atuar.", indexContent);
        Assert.DoesNotContain("O formulario abaixo aparece apenas quando um dos CTAs principais e acionado.", indexContent);
        Assert.DoesNotContain("Lead cliente", indexContent);
        Assert.DoesNotContain("Lead prestador", indexContent);

        var shellIndex = indexContent.IndexOf("data-lead-shell", StringComparison.Ordinal);
        var headingIndex = indexContent.IndexOf("class=\"section-heading lead-heading\"", StringComparison.Ordinal);

        Assert.True(shellIndex >= 0);
        Assert.True(headingIndex > shellIndex);
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
