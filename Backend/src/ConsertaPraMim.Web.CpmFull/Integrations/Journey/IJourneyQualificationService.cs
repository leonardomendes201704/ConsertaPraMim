namespace AppMobileCPM.Integrations.Journey;

public interface IJourneyQualificationService
{
    Task<JourneyQualificationResult> QualifyAsync(
        JourneyQualificationInput input,
        CancellationToken cancellationToken = default);
}
