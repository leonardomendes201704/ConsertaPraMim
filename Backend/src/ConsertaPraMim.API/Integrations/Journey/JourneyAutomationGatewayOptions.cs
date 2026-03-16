namespace ConsertaPraMim.API.Integrations.Journey;

public sealed class JourneyAutomationGatewayOptions
{
    public const string SectionName = "JourneyAutomationGateway";

    public bool Enabled { get; init; }
    public bool ClientsAutomationEnabled { get; init; } = true;
    public bool ProvidersAutomationEnabled { get; init; } = true;
    public string BaseUrl { get; init; } = string.Empty;
    public string SharedSecret { get; init; } = string.Empty;
    public int RequestTimeoutSeconds { get; init; } = 15;

    public TimeSpan GetRequestTimeout() => TimeSpan.FromSeconds(RequestTimeoutSeconds <= 0 ? 15 : RequestTimeoutSeconds);
}
