namespace ConsertaPraMim.Web.Landing.Models;

public sealed class LandingSiteOptions
{
    public const string SectionName = "LandingSite";

    public string? CanonicalUrl { get; set; }
    public string? ClientPortalUrl { get; set; }
    public string? ProviderPortalUrl { get; set; }
    public string? AdminPortalUrl { get; set; }
    public string? ApiBaseUrl { get; set; }
    public string? ApiSwaggerUrl { get; set; }

    public static string NormalizeUrl(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim().TrimEnd('/');
    }

    public static string ResolveApiBaseUrl(string? apiBaseUrl, string? apiSwaggerUrl, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            return NormalizeUrl(apiBaseUrl, fallback);
        }

        if (!string.IsNullOrWhiteSpace(apiSwaggerUrl))
        {
            var normalizedSwagger = NormalizeUrl(apiSwaggerUrl, fallback + "/swagger");
            if (normalizedSwagger.EndsWith("/swagger", StringComparison.OrdinalIgnoreCase))
            {
                return normalizedSwagger[..^"/swagger".Length];
            }

            return normalizedSwagger;
        }

        return fallback;
    }

    public static string? ResolveOrigin(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return null;
        }

        if (!Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out var uri))
        {
            return null;
        }

        return uri.GetLeftPart(UriPartial.Authority);
    }
}
