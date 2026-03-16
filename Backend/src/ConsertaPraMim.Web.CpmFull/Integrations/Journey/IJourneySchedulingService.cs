namespace AppMobileCPM.Integrations.Journey;

public interface IJourneySchedulingService
{
    Task<JourneySchedulingTurnResult> ProcessTelegramTurnAsync(
        JourneySchedulingTurnRequest request,
        CancellationToken cancellationToken = default);
}
