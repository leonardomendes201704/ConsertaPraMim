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
