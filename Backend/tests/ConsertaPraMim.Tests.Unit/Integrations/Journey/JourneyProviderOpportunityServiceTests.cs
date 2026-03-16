using AppMobileCPM.Integrations.Journey;
using AppMobileCPM.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Integrations.Journey;

public sealed class JourneyProviderOpportunityServiceTests
{
    [Fact(DisplayName = "Journey Provider Opportunity | Deve confirmar aceite e reservar alvo")]
    public async Task ConfirmAction_DeveConfirmarAceiteEReservarAlvo()
    {
        var nowUtc = new DateTime(2026, 3, 21, 13, 0, 0, DateTimeKind.Utc);
        var providerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var targetKey = "lead:601:wave:1:provider:33333333333333333333333333333333";
        var payload = BuildPayload(
            leadId: 601,
            journeyId: 9601,
            providerId,
            targetKey,
            nowUtc.AddMinutes(20),
            JourneyProviderDispatchLinkPurposes.ResponsePage);
        var kanbanService = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        var linkService = new Mock<IJourneyProviderDispatchLinkService>(MockBehavior.Strict);
        var dispatchService = new Mock<IJourneyProviderDispatchService>(MockBehavior.Strict);
        var connectionService = new Mock<IJourneyProviderConnectionService>(MockBehavior.Strict);

        linkService
            .Setup(service => service.ValidateToken("token-valido", JourneyProviderDispatchLinkPurposes.ResponsePage, nowUtc))
            .Returns(new JourneyProviderDispatchTokenValidationResult
            {
                Success = true,
                Payload = payload
            });
        linkService
            .Setup(service => service.GenerateToken(
                JourneyProviderDispatchLinkPurposes.ResponsePage,
                601,
                9601,
                providerId,
                targetKey,
                It.IsAny<DateTime>()))
            .Returns("token-renovado");
        kanbanService
            .Setup(service => service.ApplyJourneyDispatchTargetInteraction(It.Is<AdminKanbanJourneyDispatchTargetInteractionRequest>(request =>
                request.LeadId == 601 &&
                request.ProviderId == providerId &&
                request.TargetKey == targetKey &&
                request.InteractionType == AdminKanbanJourneyDispatchInteractionTypes.Clicked)))
            .Returns(new AdminKanbanJourneyDispatchTargetInteractionResult
            {
                Succeeded = true,
                LeadId = 601,
                JourneyId = 9601,
                CurrentState = AdminKanbanJourneyStates.WaitingProviderAcceptance,
                TargetStatus = AdminKanbanJourneyDispatchTargetStatuses.Sent
            });
        kanbanService
            .Setup(service => service.TryReserveJourneyDispatchTarget(It.Is<AdminKanbanJourneyDispatchReservationRequest>(request =>
                request.LeadId == 601 &&
                request.ProviderId == providerId &&
                request.TargetKey == targetKey &&
                request.SourceChannel == "email_signed_link")))
            .Returns(new AdminKanbanJourneyDispatchReservationResult
            {
                Succeeded = true,
                LeadId = 601,
                JourneyId = 9601,
                CurrentState = AdminKanbanJourneyStates.ProviderConnected,
                ReservedProviderId = providerId,
                ReservedProviderName = "Prestador Teste"
            });
        kanbanService
            .SetupSequence(service => service.GetLeadDetails(601))
            .Returns(BuildLeadDetails(601, 9601, providerId, targetKey, AdminKanbanJourneyDispatchTargetStatuses.Sent))
            .Returns(BuildLeadDetails(601, 9601, providerId, targetKey, AdminKanbanJourneyDispatchTargetStatuses.Accepted, reservedProviderId: providerId));
        connectionService
            .Setup(service => service.ConnectAsync(It.Is<JourneyProviderConnectionRequest>(request =>
                request.Lead.Id == 601 &&
                request.Target.ProviderId == providerId &&
                request.Target.TargetKey == targetKey), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JourneyProviderConnectionResult
            {
                Success = true,
                CalendarUpdated = true,
                ClientNotified = true,
                ProviderNotified = true,
                Message = "Conexao direta liberada para cliente e prestador."
            });

        var sut = CreateSut(kanbanService.Object, linkService.Object, dispatchService.Object, connectionService.Object);

        var result = await sut.ConfirmActionAsync("token-valido", JourneyProviderOpportunityActions.Accept, nowUtc);

        Assert.True(result.Success);
        Assert.Equal(JourneyProviderOpportunityActions.Accept, result.Action);
        Assert.Contains("conexao direta", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Aceite confirmado", result.Context.ResponseHeadline);
        Assert.True(result.Context.ClientContactReleased);
        Assert.Equal("13999990000", result.Context.ClientPhone);
        kanbanService.VerifyAll();
        linkService.VerifyAll();
        dispatchService.VerifyNoOtherCalls();
        connectionService.VerifyAll();
    }

    [Fact(DisplayName = "Journey Provider Opportunity | Deve registrar recusa e acionar nova onda")]
    public async Task ConfirmAction_DeveRegistrarRecusaEAcionarNovaOnda()
    {
        var nowUtc = new DateTime(2026, 3, 21, 14, 0, 0, DateTimeKind.Utc);
        var providerId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var targetKey = "lead:602:wave:1:provider:44444444444444444444444444444444";
        var payload = BuildPayload(
            leadId: 602,
            journeyId: 9602,
            providerId,
            targetKey,
            nowUtc.AddMinutes(20),
            JourneyProviderDispatchLinkPurposes.ResponsePage);
        var kanbanService = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        var linkService = new Mock<IJourneyProviderDispatchLinkService>(MockBehavior.Strict);
        var dispatchService = new Mock<IJourneyProviderDispatchService>(MockBehavior.Strict);
        var connectionService = new Mock<IJourneyProviderConnectionService>(MockBehavior.Strict);

        linkService
            .Setup(service => service.ValidateToken("token-recusa", JourneyProviderDispatchLinkPurposes.ResponsePage, nowUtc))
            .Returns(new JourneyProviderDispatchTokenValidationResult
            {
                Success = true,
                Payload = payload
            });
        linkService
            .Setup(service => service.GenerateToken(
                JourneyProviderDispatchLinkPurposes.ResponsePage,
                602,
                9602,
                providerId,
                targetKey,
                It.IsAny<DateTime>()))
            .Returns("token-renovado");
        kanbanService
            .SetupSequence(service => service.ApplyJourneyDispatchTargetInteraction(It.IsAny<AdminKanbanJourneyDispatchTargetInteractionRequest>()))
            .Returns(new AdminKanbanJourneyDispatchTargetInteractionResult
            {
                Succeeded = true,
                LeadId = 602,
                JourneyId = 9602,
                CurrentState = AdminKanbanJourneyStates.WaitingProviderAcceptance,
                TargetStatus = AdminKanbanJourneyDispatchTargetStatuses.Sent
            })
            .Returns(new AdminKanbanJourneyDispatchTargetInteractionResult
            {
                Succeeded = true,
                LeadId = 602,
                JourneyId = 9602,
                CurrentState = AdminKanbanJourneyStates.DispatchInProgress,
                TargetStatus = AdminKanbanJourneyDispatchTargetStatuses.Declined,
                Message = "Recusa registrada com sucesso."
            });
        kanbanService
            .Setup(service => service.GetLeadDetails(602))
            .Returns(BuildLeadDetails(602, 9602, providerId, targetKey, AdminKanbanJourneyDispatchTargetStatuses.Sent));
        dispatchService
            .Setup(service => service.RunOnceAsync(nowUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JourneyProviderDispatchRunResult
            {
                WavesQueuedCount = 1
            });

        var sut = CreateSut(kanbanService.Object, linkService.Object, dispatchService.Object, connectionService.Object);

        var result = await sut.ConfirmActionAsync("token-recusa", JourneyProviderOpportunityActions.Decline, nowUtc);

        Assert.True(result.Success);
        Assert.Equal(JourneyProviderOpportunityActions.Decline, result.Action);
        Assert.Equal("Recusa registrada", result.Context.ResponseHeadline);
        kanbanService.VerifyAll();
        linkService.VerifyAll();
        dispatchService.VerifyAll();
        connectionService.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "Journey Provider Opportunity | Deve rastrear abertura do e-mail")]
    public void TrackOpen_DeveRegistrarAberturaQuandoTokenValido()
    {
        var nowUtc = new DateTime(2026, 3, 21, 15, 0, 0, DateTimeKind.Utc);
        var providerId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var targetKey = "lead:603:wave:1:provider:55555555555555555555555555555555";
        var payload = BuildPayload(
            leadId: 603,
            journeyId: 9603,
            providerId,
            targetKey,
            nowUtc.AddMinutes(10),
            JourneyProviderDispatchLinkPurposes.OpenTracking);
        var kanbanService = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        var linkService = new Mock<IJourneyProviderDispatchLinkService>(MockBehavior.Strict);
        var dispatchService = new Mock<IJourneyProviderDispatchService>(MockBehavior.Strict);

        linkService
            .Setup(service => service.ValidateToken("token-open", JourneyProviderDispatchLinkPurposes.OpenTracking, nowUtc))
            .Returns(new JourneyProviderDispatchTokenValidationResult
            {
                Success = true,
                Payload = payload
            });
        kanbanService
            .Setup(service => service.ApplyJourneyDispatchTargetInteraction(It.Is<AdminKanbanJourneyDispatchTargetInteractionRequest>(request =>
                request.LeadId == 603 &&
                request.ProviderId == providerId &&
                request.TargetKey == targetKey &&
                request.InteractionType == AdminKanbanJourneyDispatchInteractionTypes.Opened &&
                request.SourceChannel == "email_pixel")))
            .Returns(new AdminKanbanJourneyDispatchTargetInteractionResult
            {
                Succeeded = true,
                LeadId = 603,
                JourneyId = 9603,
                CurrentState = AdminKanbanJourneyStates.WaitingProviderAcceptance,
                TargetStatus = AdminKanbanJourneyDispatchTargetStatuses.Sent
            });

        var sut = CreateSut(
            kanbanService.Object,
            linkService.Object,
            dispatchService.Object,
            Mock.Of<IJourneyProviderConnectionService>());

        var tracked = sut.TrackOpen("token-open", nowUtc);

        Assert.True(tracked);
        kanbanService.VerifyAll();
        linkService.VerifyAll();
        dispatchService.VerifyNoOtherCalls();
    }

    private static JourneyProviderOpportunityService CreateSut(
        IAdminKanbanService kanbanService,
        IJourneyProviderDispatchLinkService linkService,
        IJourneyProviderDispatchService dispatchService,
        IJourneyProviderConnectionService connectionService)
    {
        return new JourneyProviderOpportunityService(
            kanbanService,
            linkService,
            dispatchService,
            connectionService,
            Options.Create(new JourneyProviderNotificationOptions
            {
                Enabled = true,
                PublicBaseUrl = "https://www.consertapramim.com",
                LinkSigningSecret = "12345678901234567890123456789012",
                LinkExpirationMinutes = 45,
                ProviderPortalBaseUrl = "https://prestador.consertapramim.com"
            }));
    }

    private static JourneyProviderDispatchSignedTokenPayload BuildPayload(
        int leadId,
        int journeyId,
        Guid providerId,
        string targetKey,
        DateTime expiresAtUtc,
        string purpose)
    {
        return new JourneyProviderDispatchSignedTokenPayload
        {
            Purpose = purpose,
            LeadId = leadId,
            JourneyId = journeyId,
            ProviderId = providerId,
            TargetKey = targetKey,
            ExpiresAtUtc = expiresAtUtc
        };
    }

    private static AdminKanbanLeadDetailsRecord BuildLeadDetails(
        int leadId,
        int journeyId,
        Guid providerId,
        string targetKey,
        string targetStatus,
        Guid? reservedProviderId = null)
    {
        return new AdminKanbanLeadDetailsRecord
        {
            Id = leadId,
            StageId = 17,
            StageName = AdminKanbanJourneyClientStageNames.WaitingAcceptance,
            BoardType = AdminKanbanBoardTypes.Clients,
            Name = "Cliente Teste",
            Phone = "13999990000",
            Email = "cliente@teste.com",
            ServiceCategory = "Eletricista",
            PostalCode = "11701-200",
            City = "Praia Grande",
            Source = "telegram",
            Priority = "normal",
            StatusNote = "Cliente aguardando aceite.",
            InternalNotes = string.Empty,
            CreatedAt = new DateTime(2026, 3, 21, 12, 0, 0, DateTimeKind.Utc),
            Journey = new AdminKanbanLeadJourneyRecord
            {
                JourneyId = journeyId,
                JourneyPublicId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                LeadId = leadId,
                BoardType = AdminKanbanBoardTypes.Clients,
                SourceChannel = AdminKanbanJourneySourceChannels.Telegram,
                SourceOrigin = "telegram-bot",
                CurrentState = AdminKanbanJourneyStates.WaitingProviderAcceptance,
                CreatedAt = new DateTime(2026, 3, 21, 12, 0, 0, DateTimeKind.Utc),
                LastIntakeAt = new DateTime(2026, 3, 21, 12, 5, 0, DateTimeKind.Utc),
                Qualification = new AdminKanbanJourneyQualificationRecord
                {
                    Status = AdminKanbanJourneyQualificationStatuses.Qualified,
                    ProblemContext = "Chuveiro nao esquenta.",
                    Street = "Rua Bahia",
                    Neighborhood = "Ocian",
                    City = "Praia Grande",
                    State = "SP",
                    PostalCode = "11701-200"
                },
                Scheduling = new AdminKanbanJourneySchedulingRecord
                {
                    Status = AdminKanbanJourneySchedulingStatuses.Confirmed,
                    ScheduledStartAtUtc = new DateTime(2026, 3, 22, 18, 0, 0, DateTimeKind.Utc),
                    ScheduledEndAtUtc = new DateTime(2026, 3, 22, 19, 0, 0, DateTimeKind.Utc)
                },
                Matching = new AdminKanbanJourneyMatchingRecord
                {
                    Status = AdminKanbanJourneyMatchingStatuses.EligibleProvidersFound,
                    RequestedCategory = "Eletricista",
                    RequestedSubcategory = "chuveiro"
                },
                Dispatch = new AdminKanbanJourneyDispatchRecord
                {
                    Status = reservedProviderId.HasValue ? AdminKanbanJourneyDispatchStatuses.Reserved : AdminKanbanJourneyDispatchStatuses.WaitingAcceptance,
                    CurrentWaveNumber = 1,
                    TargetsCreatedCount = 1,
                    PendingTargetsCount = targetStatus == AdminKanbanJourneyDispatchTargetStatuses.Sent ? 1 : 0,
                    ReservedProviderId = reservedProviderId,
                    ReservedProviderName = reservedProviderId.HasValue ? "Prestador Teste" : string.Empty,
                    ReservedProviderEmail = reservedProviderId.HasValue ? "prestador@teste.com" : string.Empty,
                    ReservedProviderPhone = reservedProviderId.HasValue ? "13999998888" : string.Empty,
                    Targets =
                    [
                        new AdminKanbanJourneyDispatchTargetRecord
                        {
                            TargetKey = targetKey,
                            ProviderId = providerId,
                            ProviderName = "Prestador Teste",
                            ProviderEmail = "prestador@teste.com",
                            ProviderPhone = "13999998888",
                            RankPosition = 1,
                            WaveNumber = 1,
                            Status = targetStatus,
                            CreatedAtUtc = new DateTime(2026, 3, 21, 12, 10, 0, DateTimeKind.Utc),
                            SentAtUtc = new DateTime(2026, 3, 21, 12, 11, 0, DateTimeKind.Utc),
                            ExpiresAtUtc = new DateTime(2026, 3, 21, 15, 0, 0, DateTimeKind.Utc)
                        }
                    ]
                }
            },
            History = []
        };
    }
}
