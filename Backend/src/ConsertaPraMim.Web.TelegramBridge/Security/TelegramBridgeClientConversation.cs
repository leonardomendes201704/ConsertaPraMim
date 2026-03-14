using System.Security.Claims;
using System.Security.Cryptography;

namespace ConsertaPraMim.Web.TelegramBridge.Security;

public static class TelegramBridgeClientConversation
{
    public static bool TryGetAuthenticatedUserId(ClaimsPrincipal user, out Guid userId)
    {
        var rawUserId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out userId);
    }

    public static bool TryGetClientId(ClaimsPrincipal user, out Guid clientId)
    {
        return TryGetAuthenticatedUserId(user, out clientId);
    }

    public static long BuildChatId(Guid clientId)
    {
        var clientBytes = clientId.ToByteArray();
        var hash = SHA256.HashData(clientBytes);
        var value = BitConverter.ToInt64(hash, 0);

        if (value == long.MinValue)
        {
            return long.MaxValue;
        }

        var normalized = Math.Abs(value);
        return normalized == 0 ? 1 : normalized;
    }

    public static string BuildTitle(ClaimsPrincipal user)
    {
        var displayName = user.Identity?.Name?.Trim();
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return $"Atendimento {displayName}";
        }

        var email = user.FindFirstValue(ClaimTypes.Email)?.Trim();
        if (!string.IsNullOrWhiteSpace(email))
        {
            return $"Atendimento {email}";
        }

        return "Atendimento ConsertaPraMim";
    }
}
