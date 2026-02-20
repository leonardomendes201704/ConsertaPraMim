using ConsertaPraMim.Application.Constants;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace ConsertaPraMim.Infrastructure.Configuration;

public sealed class SystemSettingsConfigurationSource : IConfigurationSource
{
    public SystemSettingsConfigurationSource(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new SystemSettingsConfigurationProvider(ConnectionString);
    }
}

public sealed class SystemSettingsConfigurationProvider : ConfigurationProvider
{
    private readonly string _connectionString;

    public SystemSettingsConfigurationProvider(string connectionString)
    {
        _connectionString = connectionString;
    }

    public override void Load()
    {
        var settings = LoadSystemSettingsFromDatabase();
        var flattenedConfig = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in RuntimeConfigSections.All)
        {
            if (!settings.TryGetValue(definition.SettingKey, out var sectionJson) ||
                string.IsNullOrWhiteSpace(sectionJson))
            {
                continue;
            }

            TryFlattenSectionJson(definition.SectionPath, sectionJson, flattenedConfig);
        }

        // Backward compatibility: dedicated runtime keys still override section values.
        if (settings.TryGetValue(SystemSettingKeys.MonitoringTelemetryEnabled, out var telemetryEnabledRaw) &&
            TryNormalizeBoolean(telemetryEnabledRaw, out var telemetryEnabled))
        {
            flattenedConfig["Monitoring:Enabled"] = telemetryEnabled ? "true" : "false";
        }

        if (settings.TryGetValue(SystemSettingKeys.CorsAllowedOrigins, out var corsOriginsRaw) &&
            TryParseStringArray(corsOriginsRaw, out var corsOrigins))
        {
            for (var i = 0; i < corsOrigins.Count; i++)
            {
                flattenedConfig[$"Cors:AllowedOrigins:{i}"] = corsOrigins[i];
            }
        }

        Data = flattenedConfig;
    }

    private Dictionary<string, string> LoadSystemSettingsFromDatabase()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return result;
        }

        var keys = RuntimeConfigSections.All
            .Select(x => x.SettingKey)
            .Concat(
            [
                SystemSettingKeys.MonitoringTelemetryEnabled,
                SystemSettingKeys.CorsAllowedOrigins
            ])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (keys.Count == 0)
        {
            return result;
        }

        try
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            var parameterNames = new List<string>(keys.Count);
            for (var i = 0; i < keys.Count; i++)
            {
                var parameterName = $"@p{i}";
                parameterNames.Add(parameterName);
                command.Parameters.AddWithValue(parameterName, keys[i]);
            }

            command.CommandText = $"""
            SELECT [Key], [Value]
            FROM [dbo].[SystemSettings]
            WHERE [Key] IN ({string.Join(", ", parameterNames)})
            """;

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (reader.IsDBNull(0) || reader.IsDBNull(1))
                {
                    continue;
                }

                var key = reader.GetString(0);
                var value = reader.GetString(1);
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                result[key] = value;
            }
        }
        catch
        {
            // During first startup/migration, table may not exist yet.
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return result;
    }

    private static void TryFlattenSectionJson(
        string sectionPath,
        string json,
        IDictionary<string, string?> destination)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            Flatten(sectionPath, document.RootElement, destination);
        }
        catch
        {
            // Ignore malformed JSON and keep previous configuration sources.
        }
    }

    private static void Flatten(
        string currentPath,
        JsonElement element,
        IDictionary<string, string?> destination)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var childPath = string.IsNullOrWhiteSpace(currentPath)
                        ? property.Name
                        : $"{currentPath}:{property.Name}";
                    Flatten(childPath, property.Value, destination);
                }
                return;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    Flatten($"{currentPath}:{index}", item, destination);
                    index++;
                }
                return;

            case JsonValueKind.String:
                destination[currentPath] = element.GetString();
                return;

            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                destination[currentPath] = element.GetRawText();
                return;

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
            default:
                return;
        }
    }

    private static bool TryNormalizeBoolean(string? raw, out bool value)
    {
        value = false;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var normalized = raw.Trim();
        if (bool.TryParse(normalized, out var parsed))
        {
            value = parsed;
            return true;
        }

        if (normalized == "1")
        {
            value = true;
            return true;
        }

        if (normalized == "0")
        {
            value = false;
            return true;
        }

        return false;
    }

    private static bool TryParseStringArray(string? raw, out IReadOnlyList<string> values)
    {
        values = [];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(raw);
            if (parsed == null || parsed.Count == 0)
            {
                return false;
            }

            values = parsed
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList();

            return values.Count > 0;
        }
        catch
        {
            return false;
        }
    }
}
