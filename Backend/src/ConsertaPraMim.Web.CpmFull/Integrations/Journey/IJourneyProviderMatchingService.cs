namespace AppMobileCPM.Integrations.Journey;

public interface IJourneyProviderMatchingService
{
    Task<JourneyProviderMatchingRunResult> RunOnceAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
}
