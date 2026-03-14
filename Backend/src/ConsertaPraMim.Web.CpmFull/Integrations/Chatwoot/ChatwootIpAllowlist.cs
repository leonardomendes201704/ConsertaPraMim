using System.Net;
using System.Net.Sockets;

namespace AppMobileCPM.Integrations.Chatwoot;

internal static class ChatwootIpAllowlist
{
    public static bool IsAllowed(string? ipText, IReadOnlyList<string> allowlist)
    {
        if (allowlist.Count == 0)
        {
            return true;
        }

        if (!TryParseIp(ipText, out var candidate))
        {
            return false;
        }

        foreach (var entry in allowlist)
        {
            if (TryMatch(candidate, entry))
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryValidateEntry(string? entry, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(entry))
        {
            return false;
        }

        var trimmed = entry.Trim();
        if (trimmed.Contains('/', StringComparison.Ordinal))
        {
            var parts = trimmed.Split('/', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 ||
                !TryParseIp(parts[0], out var network) ||
                !int.TryParse(parts[1], out var prefixLength) ||
                prefixLength < 0 ||
                prefixLength > GetBitCount(network))
            {
                return false;
            }

            normalized = $"{network}/{prefixLength}";
            return true;
        }

        if (!TryParseIp(trimmed, out var ip))
        {
            return false;
        }

        normalized = ip.ToString();
        return true;
    }

    public static string ResolveCandidateIp(string? forwardedFor, string? remoteIp)
    {
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            var candidate = forwardedFor
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        return remoteIp?.Trim() ?? string.Empty;
    }

    private static bool TryMatch(IPAddress candidate, string entry)
    {
        if (!TryValidateEntry(entry, out var normalized))
        {
            return false;
        }

        if (!normalized.Contains('/', StringComparison.Ordinal))
        {
            return TryParseIp(normalized, out var ip) && ip.Equals(candidate);
        }

        var parts = normalized.Split('/', 2, StringSplitOptions.TrimEntries);
        return TryParseIp(parts[0], out var network) &&
               int.TryParse(parts[1], out var prefixLength) &&
               IsInCidrRange(candidate, network, prefixLength);
    }

    private static bool IsInCidrRange(IPAddress candidate, IPAddress network, int prefixLength)
    {
        if (candidate.AddressFamily != network.AddressFamily)
        {
            return false;
        }

        var candidateBytes = candidate.GetAddressBytes();
        var networkBytes = network.GetAddressBytes();
        var fullBytes = prefixLength / 8;
        var remainingBits = prefixLength % 8;

        for (var i = 0; i < fullBytes; i++)
        {
            if (candidateBytes[i] != networkBytes[i])
            {
                return false;
            }
        }

        if (remainingBits == 0)
        {
            return true;
        }

        var mask = (byte)(0xFF << (8 - remainingBits));
        return (candidateBytes[fullBytes] & mask) == (networkBytes[fullBytes] & mask);
    }

    private static bool TryParseIp(string? value, out IPAddress ipAddress)
    {
        ipAddress = IPAddress.None;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim().Trim('"');
        if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.Contains(']'))
        {
            trimmed = trimmed[1..trimmed.IndexOf(']')];
        }
        else if (trimmed.Count(ch => ch == ':') == 1 && trimmed.Contains('.'))
        {
            var separatorIndex = trimmed.LastIndexOf(':');
            trimmed = trimmed[..separatorIndex];
        }

        if (IPAddress.TryParse(trimmed, out var parsedIpAddress))
        {
            ipAddress = parsedIpAddress;
            return true;
        }

        return false;
    }

    private static int GetBitCount(IPAddress address) =>
        address.AddressFamily switch
        {
            AddressFamily.InterNetwork => 32,
            AddressFamily.InterNetworkV6 => 128,
            _ => 0
        };
}
