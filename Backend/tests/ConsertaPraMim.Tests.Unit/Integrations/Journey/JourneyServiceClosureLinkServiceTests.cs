using AppMobileCPM.Integrations.Journey;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Tests.Unit.Integrations.Journey;

public sealed class JourneyServiceClosureLinkServiceTests
{
    [Fact(DisplayName = "Journey Service Closure Link | Deve gerar e validar token assinado")]
    public void GenerateToken_DeveGerarEValidarTokenAssinado()
    {
        var expiresAtUtc = new DateTime(2026, 3, 25, 18, 0, 0, DateTimeKind.Utc);
        var providerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var sut = CreateSut();

        var token = sut.GenerateToken(
            JourneyServiceClosureTokenPurposes.ClientReview,
            JourneyServiceClosureAudiences.Client,
            leadId: 901,
            journeyId: 9901,
            providerId,
            expiresAtUtc);
        var result = sut.ValidateToken(
            token,
            JourneyServiceClosureTokenPurposes.ClientReview,
            JourneyServiceClosureAudiences.Client,
            expiresAtUtc.AddMinutes(-10));

        Assert.True(result.Success);
        Assert.False(result.Expired);
        Assert.NotNull(result.Payload);
        Assert.Equal(901, result.Payload!.LeadId);
        Assert.Equal(9901, result.Payload.JourneyId);
        Assert.Equal(providerId, result.Payload.ProviderId);
    }

    [Fact(DisplayName = "Journey Service Closure Link | Deve invalidar token expirado")]
    public void ValidateToken_DeveInvalidarTokenExpirado()
    {
        var expiresAtUtc = new DateTime(2026, 3, 25, 18, 0, 0, DateTimeKind.Utc);
        var sut = CreateSut();

        var token = sut.GenerateToken(
            JourneyServiceClosureTokenPurposes.ProviderCompletion,
            JourneyServiceClosureAudiences.Provider,
            leadId: 902,
            journeyId: 9902,
            providerId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            expiresAtUtc);
        var result = sut.ValidateToken(
            token,
            JourneyServiceClosureTokenPurposes.ProviderCompletion,
            JourneyServiceClosureAudiences.Provider,
            expiresAtUtc.AddSeconds(1));

        Assert.False(result.Success);
        Assert.True(result.Expired);
        Assert.Equal("O link da jornada expirou.", result.Message);
    }

    private static JourneyServiceClosureLinkService CreateSut()
    {
        return new JourneyServiceClosureLinkService(Options.Create(new JourneyProviderNotificationOptions
        {
            Enabled = true,
            PublicBaseUrl = "https://www.consertapramim.com",
            LinkSigningSecret = "12345678901234567890123456789012"
        }));
    }
}
