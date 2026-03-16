namespace AppMobileCPM.Integrations.Journey;

public interface IJourneyProviderDispatchNotificationService
{
    Task<JourneyProviderDispatchNotificationResult> SendOpportunityAsync(
        JourneyProviderDispatchNotificationRequest request,
        CancellationToken cancellationToken = default);
}
