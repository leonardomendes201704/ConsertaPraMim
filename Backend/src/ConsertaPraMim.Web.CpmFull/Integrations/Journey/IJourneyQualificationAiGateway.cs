namespace AppMobileCPM.Integrations.Journey;

public interface IJourneyQualificationAiGateway
{
    Task<JourneyQualificationAiResult> ExtractAsync(
        JourneyQualificationAiRequest request,
        CancellationToken cancellationToken = default);
}
