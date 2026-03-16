namespace AppMobileCPM.Integrations.Journey;

public interface IJourneyProviderOpportunityService
{
    JourneyProviderOpportunityContext GetOpportunityContext(string token, string action, DateTime nowUtc);

    JourneyProviderOpportunityActionResult ConfirmAction(string token, string action, DateTime nowUtc);

    bool TrackOpen(string token, DateTime nowUtc);
}
