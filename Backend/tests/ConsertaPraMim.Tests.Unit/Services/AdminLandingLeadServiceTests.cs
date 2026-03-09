using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Moq;
using Xunit;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminLandingLeadServiceTests
{
    /// <summary>
    /// Cenario: operacao admin filtra leads da landing por origem, cidade e termo livre.
    /// Passos: service recebe lista mista de leads cliente/prestador e aplica query com recorte textual/localidade.
    /// Resultado esperado: lista paginada retorna apenas o lead aderente e a coluna de localidade consolida bairro, cidade e UF.
    /// </summary>
    [Fact(DisplayName = "Admin landing lead service | Lista | Deve filtrar leads e montar localidade real")]
    public async Task GetLandingLeadsAsync_ShouldFilterLeadsAndBuildLocality()
    {
        var now = DateTime.UtcNow;
        var repositoryMock = new Mock<ILandingLeadRepository>();
        repositoryMock
            .Setup(repository => repository.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LandingLead>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Origin = LandingLeadOrigin.Client,
                    FullName = "Leonardo Silva",
                    Phone = "13999999999",
                    Email = "leo@exemplo.com",
                    City = "Praia Grande",
                    State = "SP",
                    Neighborhood = "Ocian",
                    RequestedService = "Conserto de ar-condicionado",
                    UtmCampaign = "landing-marco",
                    CreatedAt = now.AddHours(-2)
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Origin = LandingLeadOrigin.Provider,
                    FullName = "Maria Souza",
                    Phone = "13988887777",
                    Email = "maria@empresa.com",
                    City = "Santos",
                    State = "SP",
                    Neighborhood = "Ponta da Praia",
                    CompanyName = "MS Eletrica",
                    CreatedAt = now.AddHours(-3)
                }
            });

        var service = new AdminLandingLeadService(repositoryMock.Object);

        var response = await service.GetLandingLeadsAsync(new AdminLandingLeadsQueryDto(
            SearchTerm: "Ocian",
            Origin: "Client",
            City: "Praia",
            State: "SP",
            FromUtc: now.AddDays(-1),
            ToUtc: now,
            Page: 1,
            PageSize: 20));

        Assert.Equal(1, response.TotalCount);
        Assert.Equal(1, response.TotalClientLeads);
        Assert.Equal(0, response.TotalProviderLeads);
        var item = Assert.Single(response.Items);
        Assert.Equal("Ocian - Praia Grande/SP", item.Locality);
        Assert.Equal(LandingLeadOrigin.Client, item.Origin);
        Assert.Equal("Conserto de ar-condicionado", item.PrimaryInterest);
    }

    /// <summary>
    /// Cenario: admin abre o detalhe completo de um lead captado pela landing.
    /// Passos: repository retorna um lead com contexto comercial, UTM e metadados tecnicos ja persistidos.
    /// Resultado esperado: DTO final preserva os campos de negocio e a localidade consolidada para follow-up.
    /// </summary>
    [Fact(DisplayName = "Admin landing lead service | Detalhe | Deve mapear lead completo para o admin")]
    public async Task GetLandingLeadByIdAsync_ShouldMapLeadDetails()
    {
        var leadId = Guid.NewGuid();
        var repositoryMock = new Mock<ILandingLeadRepository>();
        repositoryMock
            .Setup(repository => repository.GetByIdAsync(leadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LandingLead
            {
                Id = leadId,
                Origin = LandingLeadOrigin.Provider,
                FullName = "Maria Souza",
                Phone = "13988887777",
                Email = "maria@empresa.com",
                City = "Santos",
                State = "SP",
                Neighborhood = "Ponta da Praia",
                ServiceCategory = "Eletrica predial",
                CompanyName = "MS Eletrica",
                CompanyDocument = "12345678000199",
                YearsOfExperience = 12,
                Message = "Atendo toda a Baixada.",
                CurrentPageUrl = "https://www.consertapramim.com/",
                ReferrerUrl = "https://www.google.com/",
                Host = "api.consertapramim.com",
                Scheme = "https",
                Path = "/api/landing-leads/public",
                QueryString = "?utm_source=google",
                UtmSource = "google",
                UtmCampaign = "parceiros-marco",
                IpAddress = "187.77.48.150",
                ForwardedFor = "187.77.48.150",
                UserAgent = "Mozilla/5.0",
                AcceptLanguage = "pt-BR",
                BrowserLanguage = "pt-BR",
                ScreenResolution = "1920x1080",
                DevicePlatform = "Windows",
                TimeZone = "America/Sao_Paulo",
                MetadataJson = "{\"browser\":\"Chrome\"}",
                CreatedAt = DateTime.UtcNow.AddHours(-4),
                UpdatedAt = DateTime.UtcNow.AddHours(-1)
            });

        var service = new AdminLandingLeadService(repositoryMock.Object);

        var response = await service.GetLandingLeadByIdAsync(leadId);

        Assert.NotNull(response);
        Assert.Equal(leadId, response!.Id);
        Assert.Equal(LandingLeadOrigin.Provider, response.Origin);
        Assert.Equal("Ponta da Praia - Santos/SP", response.Locality);
        Assert.Equal("MS Eletrica", response.CompanyName);
        Assert.Equal("google", response.UtmSource);
        Assert.Equal("Mozilla/5.0", response.UserAgent);
    }
}
