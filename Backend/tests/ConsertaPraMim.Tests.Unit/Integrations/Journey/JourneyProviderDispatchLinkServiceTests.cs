using AppMobileCPM.Integrations.Journey;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Tests.Unit.Integrations.Journey;

public sealed class JourneyProviderDispatchLinkServiceTests
{
    [Fact(DisplayName = "Journey Provider Dispatch Link | Deve gerar e validar token assinado")]
    public void GenerateToken_DeveGerarEValidarTokenAssinado()
    {
        var expiresAtUtc = new DateTime(2026, 3, 20, 18, 0, 0, DateTimeKind.Utc);
        var providerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var sut = CreateSut();

        var token = sut.GenerateToken(
            JourneyProviderDispatchLinkPurposes.ResponsePage,
            leadId: 501,
            journeyId: 9001,
            providerId,
            targetKey: "lead:501:wave:1:provider:11111111111111111111111111111111",
            expiresAtUtc);
        var result = sut.ValidateToken(
            token,
            JourneyProviderDispatchLinkPurposes.ResponsePage,
            expiresAtUtc.AddMinutes(-5));

        Assert.True(result.Success);
        Assert.False(result.Expired);
        Assert.Equal(501, result.Payload.LeadId);
        Assert.Equal(9001, result.Payload.JourneyId);
        Assert.Equal(providerId, result.Payload.ProviderId);
        Assert.Equal("lead:501:wave:1:provider:11111111111111111111111111111111", result.Payload.TargetKey);
    }

    [Fact(DisplayName = "Journey Provider Dispatch Link | Deve invalidar token expirado")]
    public void ValidateToken_DeveInvalidarTokenExpirado()
    {
        var expiresAtUtc = new DateTime(2026, 3, 20, 18, 0, 0, DateTimeKind.Utc);
        var sut = CreateSut();

        var token = sut.GenerateToken(
            JourneyProviderDispatchLinkPurposes.OpenTracking,
            leadId: 502,
            journeyId: 9002,
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            targetKey: "lead:502:wave:1:provider:22222222222222222222222222222222",
            expiresAtUtc);
        var result = sut.ValidateToken(
            token,
            JourneyProviderDispatchLinkPurposes.OpenTracking,
            expiresAtUtc.AddSeconds(1));

        Assert.False(result.Success);
        Assert.True(result.Expired);
        Assert.Equal("O link da oportunidade expirou.", result.Message);
    }

    private static JourneyProviderDispatchLinkService CreateSut()
    {
        return new JourneyProviderDispatchLinkService(Options.Create(new JourneyProviderNotificationOptions
        {
            Enabled = true,
            PublicBaseUrl = "https://www.consertapramim.com",
            LinkSigningSecret = "12345678901234567890123456789012",
            LinkExpirationMinutes = 45
        }));
    }
}
