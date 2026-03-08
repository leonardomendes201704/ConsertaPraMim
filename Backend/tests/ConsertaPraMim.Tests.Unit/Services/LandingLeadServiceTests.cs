using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Moq;
using Xunit;

namespace ConsertaPraMim.Tests.Unit.Services;

public class LandingLeadServiceTests
{
    /// <summary>
    /// Cenario: lead de cliente enviado pela landing com URL atual contendo UTM.
    /// Passos: service recebe payload + contexto HTTP, persiste a entidade e complementa as colunas de rastreio.
    /// Resultado esperado: lead salvo com origem correta, UTM extraida da query e metadados tecnicos basicos persistidos.
    /// </summary>
    [Fact(DisplayName = "Landing lead service | Captura | Deve persistir lead cliente com UTM e metadados tecnicos")]
    public async Task CaptureAsync_ShouldPersistClientLeadWithCampaignMetadata()
    {
        var repositoryMock = new Mock<ILandingLeadRepository>();
        var adminNotificationMock = new Mock<ILandingAdminNotificationService>();
        LandingLead? persistedLead = null;
        repositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<LandingLead>(), It.IsAny<CancellationToken>()))
            .Callback<LandingLead, CancellationToken>((lead, _) => persistedLead = lead)
            .Returns(Task.CompletedTask);

        var service = new ConsertaPraMim.Application.Services.LandingLeadService(
            repositoryMock.Object,
            adminNotificationMock.Object);

        var request = new CaptureLandingLeadRequestDto(
            Origin: LandingLeadOrigin.Client,
            VisitorId: "visitor-client-001",
            FullName: "Leonardo Silva",
            Phone: "(13) 99999-9999",
            Email: "leo@exemplo.com",
            City: "Praia Grande",
            State: "sp",
            Neighborhood: "Ocian",
            ServiceCategory: "Ar-condicionado",
            RequestedService: "Instalacao split 12000 BTUs",
            CompanyName: null,
            CompanyDocument: null,
            YearsOfExperience: null,
            Message: "Preciso instalar ainda esta semana.",
            CurrentPageUrl: "https://www.consertapramim.com/#captacao?utm_source=meta&utm_medium=cpc&utm_campaign=landing-marco",
            ReferrerUrl: "https://www.google.com/",
            QueryString: "?utm_source=meta&utm_medium=cpc&utm_campaign=landing-marco",
            UtmSource: null,
            UtmMedium: null,
            UtmCampaign: null,
            UtmTerm: null,
            UtmContent: null,
            BrowserLanguage: "pt-BR",
            ScreenResolution: "1920x1080",
            DevicePlatform: "Windows",
            TimeZone: "America/Sao_Paulo");

        var context = new LandingLeadCaptureContextDto(
            IpAddress: "187.77.48.150",
            ForwardedFor: "187.77.48.150",
            UserAgent: "Mozilla/5.0",
            AcceptLanguage: "pt-BR,pt;q=0.9",
            Host: "api.consertapramim.com",
            Scheme: "https",
            Path: "/api/landing-leads/public",
            RefererHeader: "https://www.consertapramim.com/");

        var response = await service.CaptureAsync(request, context);

        Assert.NotNull(persistedLead);
        Assert.Equal(LandingLeadOrigin.Client, persistedLead!.Origin);
        Assert.Equal("visitor-client-001", persistedLead.VisitorId);
        Assert.Equal("SP", persistedLead.State);
        Assert.Equal("meta", persistedLead.UtmSource);
        Assert.Equal("cpc", persistedLead.UtmMedium);
        Assert.Equal("landing-marco", persistedLead.UtmCampaign);
        Assert.Equal("187.77.48.150", persistedLead.IpAddress);
        Assert.Equal("Mozilla/5.0", persistedLead.UserAgent);
        Assert.Contains("landing-marco", persistedLead.MetadataJson);
        Assert.Equal(response.LeadId, persistedLead.Id);
        adminNotificationMock.Verify(
            service => service.NotifyLandingLeadCapturedAsync(
                It.Is<LandingLead>(lead => lead.Id == persistedLead.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Cenario: prestador informa documento com mascara e anos de experiencia fora do limite maximo.
    /// Passos: service normaliza CPF/CNPJ, clampa anos de experiencia e persiste o lead.
    /// Resultado esperado: documento apenas com digitos e experiencia limitada ao teto operacional definido.
    /// </summary>
    [Fact(DisplayName = "Landing lead service | Captura | Deve normalizar documento e limitar experiencia do prestador")]
    public async Task CaptureAsync_ShouldNormalizeProviderFields()
    {
        var repositoryMock = new Mock<ILandingLeadRepository>();
        var adminNotificationMock = new Mock<ILandingAdminNotificationService>();
        LandingLead? persistedLead = null;
        repositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<LandingLead>(), It.IsAny<CancellationToken>()))
            .Callback<LandingLead, CancellationToken>((lead, _) => persistedLead = lead)
            .Returns(Task.CompletedTask);

        var service = new ConsertaPraMim.Application.Services.LandingLeadService(
            repositoryMock.Object,
            adminNotificationMock.Object);

        var request = new CaptureLandingLeadRequestDto(
            Origin: LandingLeadOrigin.Provider,
            VisitorId: "visitor-provider-001",
            FullName: "Maria Souza",
            Phone: "13988887777",
            Email: "maria@empresa.com",
            City: "Santos",
            State: "sp",
            Neighborhood: "Ponta da Praia",
            ServiceCategory: "Eletrica predial",
            RequestedService: null,
            CompanyName: "MS Eletrica",
            CompanyDocument: "12.345.678/0001-99",
            YearsOfExperience: 99,
            Message: "Atendo litoral sul.",
            CurrentPageUrl: "https://www.consertapramim.com/#captacao",
            ReferrerUrl: null,
            QueryString: null,
            UtmSource: null,
            UtmMedium: null,
            UtmCampaign: null,
            UtmTerm: null,
            UtmContent: null,
            BrowserLanguage: "pt-BR",
            ScreenResolution: "1280x720",
            DevicePlatform: "Android",
            TimeZone: "America/Sao_Paulo");

        var context = new LandingLeadCaptureContextDto(
            IpAddress: "10.0.0.1",
            ForwardedFor: "10.0.0.1, 187.77.48.150",
            UserAgent: "Android",
            AcceptLanguage: "pt-BR",
            Host: "api.consertapramim.com",
            Scheme: "https",
            Path: "/api/landing-leads/public",
            RefererHeader: string.Empty);

        await service.CaptureAsync(request, context);

        Assert.NotNull(persistedLead);
        Assert.Equal("visitor-provider-001", persistedLead!.VisitorId);
        Assert.Equal("12345678000199", persistedLead!.CompanyDocument);
        Assert.Equal(60, persistedLead.YearsOfExperience);
        Assert.Equal(LandingLeadOrigin.Provider, persistedLead.Origin);
        adminNotificationMock.Verify(
            service => service.NotifyLandingLeadCapturedAsync(
                It.Is<LandingLead>(lead => lead.Id == persistedLead.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
