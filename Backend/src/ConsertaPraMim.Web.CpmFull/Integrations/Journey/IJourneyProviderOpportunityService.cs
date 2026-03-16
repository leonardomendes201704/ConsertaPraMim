namespace AppMobileCPM.Integrations.Journey;

public interface IJourneyProviderOpportunityService
{
    JourneyProviderOpportunityContext GetOpportunityContext(string token, string action, DateTime nowUtc);

    Task<JourneyProviderOpportunityActionResult> ConfirmActionAsync(string token, string action, DateTime nowUtc, CancellationToken cancellationToken = default);

    bool TrackOpen(string token, DateTime nowUtc);
}
