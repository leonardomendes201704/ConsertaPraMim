namespace ConsertaPraMim.Web.Landing.Models;

public sealed class LandingSiteOptions
{
    public const string SectionName = "LandingSite";

    public string? CanonicalUrl { get; set; }

    public string? ClientPortalUrl { get; set; }

    public string? ProviderPortalUrl { get; set; }

    public string? AdminPortalUrl { get; set; }

    public string? ApiSwaggerUrl { get; set; }

    public static string NormalizeUrl(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim().TrimEnd('/');
    }
}
