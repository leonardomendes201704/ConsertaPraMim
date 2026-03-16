namespace AppMobileCPM.Integrations.Journey;

public interface IJourneyProviderDispatchService
{
    Task<JourneyProviderDispatchRunResult> RunOnceAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
}
