namespace AppMobileCPM.Integrations.Journey;

public interface IJourneyProviderDispatchLinkService
{
    string GenerateToken(
        string purpose,
        int leadId,
        int journeyId,
        Guid providerId,
        string targetKey,
        DateTime expiresAtUtc);

    JourneyProviderDispatchTokenValidationResult ValidateToken(string token, string expectedPurpose, DateTime nowUtc);

    Uri BuildResponsePageUrl(string token, string action);

    Uri BuildOpenTrackingUrl(string token);
}
