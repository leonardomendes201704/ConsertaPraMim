using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConsertaPraMim.LoadTest.Wpf.Models;

public sealed class LoadTestConfig
{
    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; } = string.Empty;

    [JsonPropertyName("tenantIds")]
    public List<string> TenantIds { get; set; } = [];

    [JsonPropertyName("auth")]
    public AuthConfig Auth { get; set; } = new();

    [JsonPropertyName("scenarios")]
    public Dictionary<string, ScenarioConfig> Scenarios { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("endpoints")]
    public List<EndpointConfig> Endpoints { get; set; } = [];

    public static LoadTestConfig LoadFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Caminho do arquivo de configuracao invalido.", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Arquivo de configuracao nao encontrado.", path);
        }

        var json = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<LoadTestConfig>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        if (config is null)
        {
            throw new InvalidOperationException("Falha ao desserializar loadtest.config.json.");
        }

        if (config.Endpoints.Count == 0)
        {
            throw new InvalidOperationException("Configuracao invalida: nenhum endpoint definido.");
        }

        if (config.Scenarios.Count == 0)
        {
            throw new InvalidOperationException("Configuracao invalida: nenhum cenario definido.");
        }

        return config;
    }
}

public sealed class AuthConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("loginPath")]
    public string LoginPath { get; set; } = "/api/auth/login";

    [JsonPropertyName("tokenField")]
    public string TokenField { get; set; } = "token";

    [JsonPropertyName("accounts")]
    public List<AuthAccount> Accounts { get; set; } = [];
}

public sealed class AuthAccount
{
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
}

public sealed class ScenarioConfig
{
    [JsonPropertyName("vus")]
    public int Vus { get; set; } = 10;

    [JsonPropertyName("durationSeconds")]
    public int DurationSeconds { get; set; } = 30;

    [JsonPropertyName("rampUpSeconds")]
    public double RampUpSeconds { get; set; }

    [JsonPropertyName("thinkTimeMinMs")]
    public int ThinkTimeMinMs { get; set; } = 100;

    [JsonPropertyName("thinkTimeMaxMs")]
    public int ThinkTimeMaxMs { get; set; } = 600;

    [JsonPropertyName("errorInjectionRatePercent")]
    public double ErrorInjectionRatePercent { get; set; }
}

public sealed class EndpointConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("method")]
    public string Method { get; set; } = "GET";

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("auth")]
    public string Auth { get; set; } = "none";

    [JsonPropertyName("weight")]
    public double Weight { get; set; } = 1;

    [JsonPropertyName("capture")]
    public string? Capture { get; set; }

    [JsonPropertyName("fallbackPath")]
    public string? FallbackPath { get; set; }

    [JsonPropertyName("invalidPath")]
    public string? InvalidPath { get; set; }

    public string EndpointKey => string.IsNullOrWhiteSpace(Name)
        ? $"{Method.ToUpperInvariant()} {Path}"
        : Name;
}
