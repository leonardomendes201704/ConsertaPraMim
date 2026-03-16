using AppMobileCPM.Integrations.Chatwoot;
using AppMobileCPM.Integrations.Journey;
using AppMobileCPM.Integrations.Telegram;
using AppMobileCPM.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Integrations.Telegram;

public sealed class TelegramLeadAutomationServiceTests
{
    [Fact(DisplayName = "Telegram Lead Automation | Deve criar lead e sincronizar com Chatwoot")]
    public async Task UpsertLeadAsync_DeveCriarLeadESincronizarComChatwoot()
    {
        var chatbotConversationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var kanbanService = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        var chatwootLeadSyncService = new Mock<IChatwootLeadSyncService>(MockBehavior.Strict);
        var journeyQualificationService = new Mock<IJourneyQualificationService>(MockBehavior.Strict);

        journeyQualificationService
            .Setup(service => service.QualifyAsync(
                It.Is<JourneyQualificationInput>(input =>
                    input.BoardType == AdminKanbanBoardTypes.Clients &&
                    input.SourceChannel == AdminKanbanJourneySourceChannels.Telegram &&
                    input.Phone == "+5513997114422" &&
                    input.Email == "cliente@telegram.com" &&
                    input.ServiceCategory == "Eletricista" &&
                    input.ProblemDescription == "Chuveiro queimou no apartamento"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JourneyQualificationResult
            {
                Status = AdminKanbanJourneyQualificationStatuses.Qualified,
                Source = AdminKanbanJourneyQualificationSources.Deterministic,
                ConfidenceScore = 0.92m,
                HasRequiredData = true,
                NeedsConfirmation = false,
                NormalizedServiceCategoryId = "eletricista",
                NormalizedServiceCategoryName = "Eletricista",
                ProblemContext = "Chuveiro queimou no apartamento",
                Street = "Rua Bahia",
                Neighborhood = "Ocian",
                City = "Praia Grande",
                State = "SP",
                PostalCode = "11701-200",
                Latitude = -24.010101,
                Longitude = -46.40202,
                Summary = "Triagem concluida.",
                RequiredFields = ["Telefone", "Categoria", "CEP", "Cidade", "Logradouro ou bairro", "Contexto do problema"],
                OptionalFields = ["E-mail", "UF", "Latitude", "Longitude"]
            });

        kanbanService
            .Setup(service => service.UpsertJourneyIntake(It.Is<AdminKanbanJourneyIntakeRequest>(request =>
                request.BoardType == AdminKanbanBoardTypes.Clients &&
                request.SourceChannel == AdminKanbanJourneySourceChannels.Telegram &&
                request.ChatbotConversationId == chatbotConversationId &&
                request.ChannelConversationId == "chat-telegram-5513" &&
                request.TelegramChatId == 5513997114422 &&
                request.ClientId == userId &&
                request.Phone == "+5513997114422" &&
                request.Email == "cliente@telegram.com" &&
                request.ServiceCategory == "Eletricista" &&
                request.ProblemDescription == "Chuveiro queimou no apartamento" &&
                request.Street == "Rua Bahia" &&
                request.Neighborhood == "Ocian" &&
                request.City == "Praia Grande" &&
                request.State == "SP" &&
                request.PostalCode == "11701-200" &&
                request.Latitude == -24.010101 &&
                request.Longitude == -46.40202 &&
                request.Qualification.Status == AdminKanbanJourneyQualificationStatuses.Qualified &&
                request.Qualification.NormalizedServiceCategoryId == "eletricista"))))
            .Returns(new AdminKanbanJourneyUpsertResult
            {
                LeadId = 81,
                JourneyId = 17,
                JourneyPublicId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                CreatedLead = true,
                CreatedJourney = true,
                StageId = 1,
                BoardType = AdminKanbanBoardTypes.Clients,
                CurrentState = AdminKanbanJourneyStates.AutomatedTriage
            });
        kanbanService
            .Setup(service => service.GetLeadDetails(81))
            .Returns(new AdminKanbanLeadDetailsRecord
            {
                Id = 81,
                StageId = 1,
                StageName = "Novo lead",
                BoardType = AdminKanbanBoardTypes.Clients,
                Name = "Ricardo Almeida",
                Phone = "+5513997114422",
                Email = "cliente@telegram.com",
                History = []
            });
        kanbanService
            .Setup(service => service.GetJourneyDetails(81))
            .Returns(new AdminKanbanLeadJourneyRecord
            {
                LeadId = 81,
                BoardType = AdminKanbanBoardTypes.Clients,
                PrimaryPhone = "+5513997114422",
                PrimaryEmail = "cliente@telegram.com",
                Qualification = new AdminKanbanJourneyQualificationRecord
                {
                    City = "Praia Grande",
                    NormalizedServiceCategoryName = "Eletricista"
                }
            });
        chatwootLeadSyncService
            .Setup(service => service.SyncLeadAsync(81, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(ChatwootLeadSyncResult.Synced(
                "Lead sincronizado com Chatwoot.",
                101,
                202,
                1));

        var sut = CreateSut(
            kanbanService.Object,
            chatwootLeadSyncService.Object,
            journeyQualificationService.Object,
            new TelegramAutomationOptions
            {
                Enabled = true,
                ClientsAutomationEnabled = true,
                ProvidersAutomationEnabled = true,
                SharedSecret = "segredo-compartilhado",
                TelegramBridgeBaseUrl = "https://bridge.exemplo.com",
                RequestTimeoutSeconds = 15
            });

        var result = await sut.UpsertLeadAsync(
            new TelegramLeadAutomationRequest
            {
                BoardType = AdminKanbanBoardTypes.Clients,
                ChatbotConversationId = chatbotConversationId,
                ChannelConversationId = "chat-telegram-5513",
                TelegramChatId = 5513997114422,
                UserId = userId,
                UserName = "Ricardo Almeida",
                UserPhone = "+5513997114422",
                UserEmail = "cliente@telegram.com",
                ServiceCategory = "Eletricista",
                ProblemDescription = "Chuveiro queimou no apartamento",
                PostalCode = "11701-200",
                City = "Praia Grande",
                LastContactAtUtc = new DateTime(2026, 3, 14, 20, 0, 0, DateTimeKind.Utc)
            },
            "segredo-compartilhado");

        Assert.True(result.Success);
        Assert.Equal(StatusCodes.Status200OK, result.HttpStatusCode);
        Assert.NotNull(result.Payload);
        Assert.Equal(81, result.Payload!.LeadId);
        Assert.True(result.Payload.Created);
        Assert.Equal(AdminKanbanBoardTypes.Clients, result.Payload.BoardType);
        Assert.True(result.Payload.HasPhone);
        Assert.True(result.Payload.HasEmail);
        Assert.True(result.Payload.HasCity);
        Assert.True(result.Payload.HasServiceCategory);
        Assert.Equal("synced", result.Payload.ChatwootStatus);
        Assert.Equal(101, result.Payload.ChatwootContactId);
        Assert.Equal(202, result.Payload.ChatwootConversationId);
        Assert.Equal(1, result.Payload.ChatwootInboxId);
        kanbanService.VerifyAll();
        chatwootLeadSyncService.VerifyAll();
        journeyQualificationService.VerifyAll();
    }

    [Fact(DisplayName = "Telegram Lead Automation | Deve rejeitar segredo invalido")]
    public async Task UpsertLeadAsync_DeveFalharQuandoSegredoForInvalido()
    {
        var sut = CreateSut(
            Mock.Of<IAdminKanbanService>(),
            Mock.Of<IChatwootLeadSyncService>(),
            Mock.Of<IJourneyQualificationService>(),
            new TelegramAutomationOptions
            {
                Enabled = true,
                ClientsAutomationEnabled = true,
                ProvidersAutomationEnabled = true,
                SharedSecret = "segredo-correto",
                TelegramBridgeBaseUrl = "https://bridge.exemplo.com",
                RequestTimeoutSeconds = 15
            });

        var result = await sut.UpsertLeadAsync(
            new TelegramLeadAutomationRequest
            {
                BoardType = AdminKanbanBoardTypes.Clients,
                ChatbotConversationId = Guid.NewGuid(),
                ChannelConversationId = "chat-telegram-erro",
                TelegramChatId = 5513997000000,
                UserId = Guid.NewGuid()
            },
            "segredo-incorreto");

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.HttpStatusCode);
        Assert.Contains("invalida", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Telegram Lead Automation | Deve bloquear prestadores quando feature flag estiver desligada")]
    public async Task UpsertLeadAsync_DeveFalharQuandoPrestadoresEstiveremDesabilitados()
    {
        var sut = CreateSut(
            Mock.Of<IAdminKanbanService>(),
            Mock.Of<IChatwootLeadSyncService>(),
            Mock.Of<IJourneyQualificationService>(),
            new TelegramAutomationOptions
            {
                Enabled = true,
                ClientsAutomationEnabled = true,
                ProvidersAutomationEnabled = false,
                SharedSecret = "segredo-correto",
                TelegramBridgeBaseUrl = "https://bridge.exemplo.com",
                RequestTimeoutSeconds = 15
            });

        var result = await sut.UpsertLeadAsync(
            new TelegramLeadAutomationRequest
            {
                BoardType = AdminKanbanBoardTypes.Providers,
                ChatbotConversationId = Guid.NewGuid(),
                ChannelConversationId = "chat-provider",
                TelegramChatId = 5513988887777,
                UserId = Guid.NewGuid()
            },
            "segredo-correto");

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status409Conflict, result.HttpStatusCode);
        Assert.Contains("prestadores", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Telegram Lead Automation | Deve refletir contato persistido no retorno do upsert")]
    public async Task UpsertLeadAsync_DeveRetornarContatoPersistidoDoLead()
    {
        var chatbotConversationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var kanbanService = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        var chatwootLeadSyncService = new Mock<IChatwootLeadSyncService>(MockBehavior.Strict);
        var journeyQualificationService = new Mock<IJourneyQualificationService>(MockBehavior.Strict);

        journeyQualificationService
            .Setup(service => service.QualifyAsync(It.IsAny<JourneyQualificationInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JourneyQualificationResult
            {
                Status = AdminKanbanJourneyQualificationStatuses.Pending,
                Source = AdminKanbanJourneyQualificationSources.Deterministic,
                ConfidenceScore = 0.71m,
                HasRequiredData = false,
                NeedsConfirmation = false
            });

        kanbanService
            .Setup(service => service.UpsertJourneyIntake(It.IsAny<AdminKanbanJourneyIntakeRequest>()))
            .Returns(new AdminKanbanJourneyUpsertResult
            {
                LeadId = 99,
                JourneyId = 7,
                JourneyPublicId = Guid.NewGuid(),
                CreatedLead = false,
                CreatedJourney = false,
                StageId = 2,
                BoardType = AdminKanbanBoardTypes.Clients,
                CurrentState = AdminKanbanJourneyStates.QualificationPending
            });
        kanbanService
            .Setup(service => service.GetLeadDetails(99))
            .Returns(new AdminKanbanLeadDetailsRecord
            {
                Id = 99,
                StageId = 2,
                StageName = "Dados pendentes",
                BoardType = AdminKanbanBoardTypes.Clients,
                Name = "Ricardo Almeida",
                Phone = "+5513996891738",
                History = []
            });
        kanbanService
            .Setup(service => service.GetJourneyDetails(99))
            .Returns(new AdminKanbanLeadJourneyRecord
            {
                LeadId = 99,
                BoardType = AdminKanbanBoardTypes.Clients,
                PrimaryPhone = "+5513996891738"
            });
        chatwootLeadSyncService
            .Setup(service => service.SyncLeadAsync(99, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(ChatwootLeadSyncResult.Pending("Sincronizacao pendente."));

        var sut = CreateSut(
            kanbanService.Object,
            chatwootLeadSyncService.Object,
            journeyQualificationService.Object,
            new TelegramAutomationOptions
            {
                Enabled = true,
                ClientsAutomationEnabled = true,
                ProvidersAutomationEnabled = true,
                SharedSecret = "segredo-compartilhado",
                TelegramBridgeBaseUrl = "https://bridge.exemplo.com",
                RequestTimeoutSeconds = 15
            });

        var result = await sut.UpsertLeadAsync(
            new TelegramLeadAutomationRequest
            {
                BoardType = AdminKanbanBoardTypes.Clients,
                ChatbotConversationId = chatbotConversationId,
                ChannelConversationId = "chat-telegram-5513",
                TelegramChatId = 5513996891738,
                UserId = userId,
                UserName = "Ricardo Almeida",
                City = "Praia Grande"
            },
            "segredo-compartilhado");

        Assert.True(result.Success);
        Assert.NotNull(result.Payload);
        Assert.True(result.Payload!.HasPhone);
        Assert.False(result.Payload.HasEmail);
        Assert.False(result.Payload.HasCity);
        Assert.False(result.Payload.HasServiceCategory);
        kanbanService.VerifyAll();
        chatwootLeadSyncService.VerifyAll();
        journeyQualificationService.VerifyAll();
    }

    private static TelegramLeadAutomationService CreateSut(
        IAdminKanbanService kanbanService,
        IChatwootLeadSyncService chatwootLeadSyncService,
        IJourneyQualificationService journeyQualificationService,
        TelegramAutomationOptions options)
    {
        return new TelegramLeadAutomationService(
            kanbanService,
            chatwootLeadSyncService,
            journeyQualificationService,
            Options.Create(options),
            NullLogger<TelegramLeadAutomationService>.Instance);
    }
}
