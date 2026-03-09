using System.Net;

namespace ConsertaPraMim.Web.Client.Services;

public static class ClientPublicUrlResolver
{
    public static string ResolveApiBaseUrl(
        string? candidateBaseUrl,
        string? requestHost,
        string fallbackBaseUrl)
        => TrimTrailingSlash(
            ResolvePortalUrl(candidateBaseUrl, requestHost, "api", fallbackBaseUrl));

    private static string ResolvePortalUrl(
        string? candidateUrl,
        string? requestHost,
        string expectedSubdomain,
        string fallbackUrl)
    {
        var normalizedCandidate = NormalizeAbsoluteUrl(candidateUrl);
        if (IsPreferredPublicHttpsUrl(normalizedCandidate))
        {
            return normalizedCandidate!;
        }

        var inferred = TryBuildSiblingSubdomainUrl(requestHost, expectedSubdomain);
        if (!string.IsNullOrWhiteSpace(inferred))
        {
            return inferred;
        }

        var normalizedFallback = NormalizeAbsoluteUrl(fallbackUrl);
        if (IsPreferredPublicHttpsUrl(normalizedFallback))
        {
            return normalizedFallback!;
        }

        if (IsPreferredPublicUrl(normalizedCandidate))
        {
            return normalizedCandidate!;
        }

        if (!string.IsNullOrWhiteSpace(normalizedCandidate))
        {
            return normalizedCandidate!;
        }

        return normalizedFallback ?? fallbackUrl;
    }

    private static string? NormalizeAbsoluteUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            ? uri.ToString()
            : null;
    }

    private static bool IsPreferredPublicHttpsUrl(string? value)
    {
        if (!IsPreferredPublicUrl(value) || !Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPreferredPublicUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return !IsLocalOrIpHost(uri.Host);
    }

    private static bool IsLocalOrIpHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return true;
        }

        var trimmed = host.Trim();
        if (trimmed.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("::1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(trimmed, out _);
    }

    private static string? TryBuildSiblingSubdomainUrl(string? requestHost, string expectedSubdomain)
    {
        if (string.IsNullOrWhiteSpace(requestHost) || string.IsNullOrWhiteSpace(expectedSubdomain))
        {
            return null;
        }

        var trimmedHost = requestHost.Trim();
        if (IsLocalOrIpHost(trimmedHost))
        {
            return null;
        }

        var dotIndex = trimmedHost.IndexOf('.');
        if (dotIndex <= 0 || dotIndex == trimmedHost.Length - 1)
        {
            return null;
        }

        var rootDomain = trimmedHost[(dotIndex + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(rootDomain))
        {
            return null;
        }

        return $"https://{expectedSubdomain.Trim()}.{rootDomain}/";
    }

    private static string TrimTrailingSlash(string value)
        => string.IsNullOrWhiteSpace(value) ? value : value.TrimEnd('/');
}
