namespace AppMobileCPM.Integrations.Journey;

public interface IJourneyServiceClosureLinkService
{
    string GenerateToken(string purpose, string audience, int leadId, int journeyId, Guid providerId, DateTime expiresAtUtc);
    JourneyServiceClosureTokenValidationResult ValidateToken(string token, string expectedPurpose, string expectedAudience, DateTime nowUtc);
    Uri BuildProviderCompletionUrl(string token);
    Uri BuildClientCompletionUrl(string token, string action);
    Uri BuildReviewUrl(string token);
}
