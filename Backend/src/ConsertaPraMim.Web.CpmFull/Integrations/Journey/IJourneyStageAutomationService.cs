namespace AppMobileCPM.Integrations.Journey;

public interface IJourneyStageAutomationService
{
    Task<JourneyStageAutomationRunResult> RunOnceAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
}
