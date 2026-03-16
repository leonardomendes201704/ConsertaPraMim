using AppMobileCPM.Integrations.Journey;
using AppMobileCPM.Models;
using AppMobileCPM.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Integrations.Journey;

public sealed class JourneyQualificationServiceTests
{
    [Fact(DisplayName = "Journey Qualification | Deve qualificar cliente com dados completos")]
    public async Task QualifyAsync_DeveQualificarClienteQuandoDadosObrigatoriosEstaoPresentes()
    {
        var marketplaceRepository = new Mock<IMarketplaceRepository>(MockBehavior.Strict);
        var geocodingService = new Mock<IJourneyGeocodingService>(MockBehavior.Strict);
        var aiGateway = new Mock<IJourneyQualificationAiGateway>(MockBehavior.Strict);

        marketplaceRepository
            .Setup(repository => repository.GetCategories())
            .Returns(
            [
                new ServiceCategory { Id = "eletricista", Name = "Eletricista", IconClass = "bolt" },
                new ServiceCategory { Id = "encanador", Name = "Encanador", IconClass = "water_drop" }
            ]);
        geocodingService
            .Setup(service => service.ResolveAsync("11701-200", "", "Praia Grande", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JourneyGeocodingResult
            {
                PostalCode = "11701-200",
                Street = "Rua Bahia",
                Neighborhood = "Ocian",
                City = "Praia Grande",
                State = "SP",
                Latitude = -24.005001,
                Longitude = -46.401001
            });

        var sut = new JourneyQualificationService(
            marketplaceRepository.Object,
            geocodingService.Object,
            aiGateway.Object,
            Options.Create(new JourneyQualificationOptions
            {
                Enabled = true,
                AiEnabled = false,
                MinimumConfidenceForAutoApply = 0.75m
            }),
            NullLogger<JourneyQualificationService>.Instance);

        var result = await sut.QualifyAsync(
            new JourneyQualificationInput
            {
                BoardType = AdminKanbanBoardTypes.Clients,
                SourceChannel = AdminKanbanJourneySourceChannels.Telegram,
                Name = "Cliente Jornada",
                Phone = "+5513999990001",
                Email = "cliente@teste.com",
                ServiceCategory = "chuveiro queimado",
                ProblemDescription = "Meu chuveiro queimou e preciso de atendimento ainda hoje no apartamento.",
                City = "Praia Grande",
                State = "SP",
                PostalCode = "11701-200"
            });

        Assert.Equal(AdminKanbanJourneyQualificationStatuses.Qualified, result.Status);
        Assert.Equal(AdminKanbanJourneyQualificationSources.Deterministic, result.Source);
        Assert.True(result.HasRequiredData);
        Assert.False(result.NeedsConfirmation);
        Assert.Equal("eletricista", result.NormalizedServiceCategoryId);
        Assert.Equal("Eletricista", result.NormalizedServiceCategoryName);
        Assert.Equal("Rua Bahia", result.Street);
        Assert.Equal("Ocian", result.Neighborhood);
        Assert.Equal("Praia Grande", result.City);
        Assert.Equal("11701-200", result.PostalCode);
        Assert.Equal(-24.005001, result.Latitude);
        Assert.Equal(-46.401001, result.Longitude);
        Assert.Empty(result.MissingRequiredFields);
        Assert.NotNull(result.QualifiedAtUtc);
        Assert.Contains("Triagem estruturada concluida", result.Summary, StringComparison.OrdinalIgnoreCase);

        marketplaceRepository.VerifyAll();
        geocodingService.VerifyAll();
        aiGateway.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "Journey Qualification | Deve solicitar confirmacao para prestador com confianca baixa")]
    public async Task QualifyAsync_DeveSolicitarConfirmacaoQuandoPrestadorTemDadosMinimosMasConfiancaBaixa()
    {
        var marketplaceRepository = new Mock<IMarketplaceRepository>(MockBehavior.Strict);
        var geocodingService = new Mock<IJourneyGeocodingService>(MockBehavior.Strict);
        var aiGateway = new Mock<IJourneyQualificationAiGateway>(MockBehavior.Strict);

        marketplaceRepository
            .Setup(repository => repository.GetCategories())
            .Returns(
            [
                new ServiceCategory { Id = "eletricista", Name = "Eletricista", IconClass = "bolt" }
            ]);

        var sut = new JourneyQualificationService(
            marketplaceRepository.Object,
            geocodingService.Object,
            aiGateway.Object,
            Options.Create(new JourneyQualificationOptions
            {
                Enabled = true,
                AiEnabled = false,
                MinimumConfidenceForAutoApply = 0.75m
            }),
            NullLogger<JourneyQualificationService>.Instance);

        var result = await sut.QualifyAsync(
            new JourneyQualificationInput
            {
                BoardType = AdminKanbanBoardTypes.Providers,
                SourceChannel = AdminKanbanJourneySourceChannels.Telegram,
                Name = "Prestador Jornada",
                Phone = "13999990002",
                ServiceCategory = "Eletricista",
                ProblemDescription = "Atuo como eletricista autonomo e quero receber oportunidades na regiao.",
                City = "Praia Grande"
            });

        Assert.Equal(AdminKanbanJourneyQualificationStatuses.ConfirmationRequired, result.Status);
        Assert.Equal(AdminKanbanJourneyQualificationSources.Deterministic, result.Source);
        Assert.True(result.HasRequiredData);
        Assert.True(result.NeedsConfirmation);
        Assert.Equal("Eletricista", result.NormalizedServiceCategoryName);
        Assert.Empty(result.MissingRequiredFields);
        Assert.Contains("Antes de seguir com seu cadastro", result.ConfirmationPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0.70m, result.ConfidenceScore);

        marketplaceRepository.VerifyAll();
        geocodingService.VerifyNoOtherCalls();
        aiGateway.VerifyNoOtherCalls();
    }
}
