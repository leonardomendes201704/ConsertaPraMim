using System.Text.Json;
using ConsertaPraMim.Web.TelegramBridge.Models;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public static class TelegramAiResponseParser
{
    public static TelegramAiStructuredResponse Parse(string? rawOutput, string fallbackMessage)
    {
        var safeFallback = NormalizeMessage(fallbackMessage);
        if (string.IsNullOrWhiteSpace(rawOutput))
        {
            return new TelegramAiStructuredResponse(
                MessageToClient: safeFallback,
                Intent: "unknown",
                NextStep: "collect_missing_data",
                Confidence: null,
                EntitiesJson: null);
        }

        var trimmed = rawOutput.Trim();
        var jsonCandidate = TryExtractJson(trimmed);
        if (string.IsNullOrWhiteSpace(jsonCandidate))
        {
            return new TelegramAiStructuredResponse(
                MessageToClient: NormalizeMessage(trimmed, safeFallback),
                Intent: "unknown",
                NextStep: "collect_missing_data",
                Confidence: null,
                EntitiesJson: null);
        }

        try
        {
            using var document = JsonDocument.Parse(jsonCandidate);
            var root = document.RootElement;

            var message = GetString(root, "messageToClient")
                ?? GetString(root, "message")
                ?? safeFallback;

            var intent = GetString(root, "intent") ?? "unknown";
            var nextStep = GetString(root, "nextStep") ?? "collect_missing_data";
            var confidence = GetDecimal(root, "confidence");
            var entitiesJson = GetEntitiesJson(root, "entities");

            return new TelegramAiStructuredResponse(
                MessageToClient: NormalizeMessage(message, safeFallback),
                Intent: NormalizeToken(intent, "unknown"),
                NextStep: NormalizeToken(nextStep, "collect_missing_data"),
                Confidence: confidence,
                EntitiesJson: entitiesJson);
        }
        catch (JsonException)
        {
            return new TelegramAiStructuredResponse(
                MessageToClient: NormalizeMessage(trimmed, safeFallback),
                Intent: "unknown",
                NextStep: "collect_missing_data",
                Confidence: null,
                EntitiesJson: null);
        }
    }

    private static string NormalizeMessage(string value, string? fallback = null)
    {
        var trimmed = value.Trim();
        if (!string.IsNullOrWhiteSpace(trimmed))
        {
            return trimmed.Length <= 1500 ? trimmed : trimmed[..1500];
        }

        if (!string.IsNullOrWhiteSpace(fallback))
        {
            return fallback.Trim();
        }

        return "Entendi. Me passe mais detalhes para eu te ajudar melhor.";
    }

    private static string NormalizeToken(string value, string fallback)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return fallback;
        }

        return trimmed.Length <= 80 ? trimmed : trimmed[..80];
    }

    private static string? GetString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }

    private static decimal? GetDecimal(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var value))
        {
            return Math.Clamp(value, 0, 1);
        }

        return null;
    }

    private static string? GetEntitiesJson(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Null || property.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        return property.GetRawText();
    }

    private static string? TryExtractJson(string rawOutput)
    {
        if (rawOutput.StartsWith('{') && rawOutput.EndsWith('}'))
        {
            return rawOutput;
        }

        var start = rawOutput.IndexOf('{');
        var end = rawOutput.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        return rawOutput[start..(end + 1)];
    }
}
