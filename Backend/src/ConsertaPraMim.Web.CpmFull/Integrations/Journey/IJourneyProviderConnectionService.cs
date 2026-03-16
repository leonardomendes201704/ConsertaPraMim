namespace AppMobileCPM.Integrations.Journey;

public interface IJourneyProviderConnectionService
{
    Task<JourneyProviderConnectionResult> ConnectAsync(
        JourneyProviderConnectionRequest request,
        CancellationToken cancellationToken = default);
}
