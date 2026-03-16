namespace AppMobileCPM.Integrations.Journey;

public interface IJourneyAutomationService
{
    Task<JourneyAutomationResult> UpsertJourneyAsync(
        JourneyAutomationRequest request,
        string providedSecret,
        CancellationToken cancellationToken = default);
}
