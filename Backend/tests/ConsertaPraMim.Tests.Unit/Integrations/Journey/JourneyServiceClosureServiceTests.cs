using AppMobileCPM.Integrations.Journey;
using AppMobileCPM.Integrations.Telegram;
using AppMobileCPM.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Integrations.Journey;

public sealed class JourneyServiceClosureServiceTests
{
    [Fact(DisplayName = "Journey Service Closure | Deve contestar conclusao e levar a jornada para excecao operacional")]
    public async Task SubmitClientDecisionAsync_DeveContestarConclusao()
    {
        var nowUtc = new DateTime(2026, 3, 26, 14, 0, 0, DateTimeKind.Utc);
        var providerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var kanbanService = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        var linkService = new Mock<IJourneyServiceClosureLinkService>(MockBehavior.Strict);
        var telegramClient = new Mock<ITelegramBridgeDeliveryClient>(MockBehavior.Strict);

        linkService
            .Setup(service => service.ValidateToken("token-cliente", JourneyServiceClosureTokenPurposes.ClientCompletionDecision, JourneyServiceClosureAudiences.Client, nowUtc))
            .Returns(new JourneyServiceClosureTokenValidationResult
            {
                Success = true,
                Payload = new JourneyServiceClosureSignedTokenPayload
                {
                    Purpose = JourneyServiceClosureTokenPurposes.ClientCompletionDecision,
                    Audience = JourneyServiceClosureAudiences.Client,
                    LeadId = 1001,
                    JourneyId = 91001,
                    ProviderId = providerId,
                    ExpiresAtUtc = nowUtc.AddHours(24)
                }
            });
        kanbanService
            .Setup(service => service.GetLeadDetails(1001))
            .Returns(BuildLead(1001, 91001, providerId));
        kanbanService
            .Setup(service => service.UpdateJourneyClosure(It.Is<int>(id => id == 1001), It.Is<AdminKanbanJourneyClosureUpdateRequest>(request =>
                request.Status == AdminKanbanJourneyClosureStatuses.Contested &&
                request.CurrentState == AdminKanbanJourneyStates.OperationalException &&
                request.HistoryEventType == "jornada_conclusao_contestada" &&
                request.ContestedReason == "O servico nao foi finalizado"))))
            .Returns(new AdminKanbanJourneyClosureUpdateResult
            {
                LeadId = 1001,
                JourneyId = 91001,
                CurrentState = AdminKanbanJourneyStates.OperationalException,
                Closure = new AdminKanbanJourneyClosureRecord
                {
                    Status = AdminKanbanJourneyClosureStatuses.Contested
                }
            });
        kanbanService
            .Setup(service => service.ApplyJourneyStageAutomation(It.Is<AdminKanbanJourneyStageAutomationUpdateRequest>(request =>
                request.LeadId == 1001 &&
                request.TargetCurrentState == AdminKanbanJourneyStates.OperationalException &&
                request.HistoryEventType == "jornada_conclusao_contestada")))
            .Returns(new AdminKanbanJourneyStageAutomationUpdateResult
            {
                LeadId = 1001,
                JourneyId = 91001,
                CurrentState = AdminKanbanJourneyStates.OperationalException
            });

        var sut = CreateSut(kanbanService.Object, linkService.Object, telegramClient.Object);

        var result = await sut.SubmitClientDecisionAsync(
            "token-cliente",
            JourneyServiceClosureReviewActions.Contest,
            "O servico nao foi finalizado",
            nowUtc);

        Assert.True(result.Success);
        Assert.Contains("Contestacao registrada", result.Message, StringComparison.OrdinalIgnoreCase);
        kanbanService.VerifyAll();
        linkService.VerifyAll();
        telegramClient.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "Journey Service Closure | Deve registrar avaliacao do cliente e solicitar avaliacao do prestador")]
    public async Task SubmitReviewAsync_DeveRegistrarAvaliacaoDoCliente()
    {
        var nowUtc = new DateTime(2026, 3, 26, 15, 0, 0, DateTimeKind.Utc);
        var providerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var kanbanService = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        var linkService = new Mock<IJourneyServiceClosureLinkService>(MockBehavior.Strict);
        var telegramClient = new Mock<ITelegramBridgeDeliveryClient>(MockBehavior.Strict);

        linkService
            .Setup(service => service.ValidateToken("token-avaliacao", JourneyServiceClosureTokenPurposes.ClientReview, JourneyServiceClosureAudiences.Client, nowUtc))
            .Returns(new JourneyServiceClosureTokenValidationResult
            {
                Success = true,
                Payload = new JourneyServiceClosureSignedTokenPayload
                {
                    Purpose = JourneyServiceClosureTokenPurposes.ClientReview,
                    Audience = JourneyServiceClosureAudiences.Client,
                    LeadId = 1002,
                    JourneyId = 91002,
                    ProviderId = providerId,
                    ExpiresAtUtc = nowUtc.AddHours(24)
                }
            });
        linkService
            .Setup(service => service.GenerateToken(
                JourneyServiceClosureTokenPurposes.ProviderReview,
                JourneyServiceClosureAudiences.Provider,
                1002,
                91002,
                providerId,
                It.IsAny<DateTime>()))
            .Returns("token-avaliacao-prestador");
        linkService
            .Setup(service => service.BuildReviewUrl("token-avaliacao-prestador"))
            .Returns(new Uri("https://www.consertapramim.com/jornada/avaliacoes/responder?token=token-avaliacao-prestador"));
        kanbanService
            .Setup(service => service.GetLeadDetails(1002))
            .Returns(BuildLead(1002, 91002, providerId));
        kanbanService
            .Setup(service => service.UpdateJourneyClosure(It.Is<int>(id => id == 1002), It.Is<AdminKanbanJourneyClosureUpdateRequest>(request =>
                request.Status == AdminKanbanJourneyClosureStatuses.WaitingProviderReview &&
                request.ClientReviewStatus == AdminKanbanJourneyReviewStatuses.Submitted &&
                request.ProviderReviewStatus == AdminKanbanJourneyReviewStatuses.Pending &&
                request.HistoryEventType == "jornada_avaliacao_cliente_enviada" &&
                request.ClientReview.Rating == 5))))
            .Returns(new AdminKanbanJourneyClosureUpdateResult
            {
                LeadId = 1002,
                JourneyId = 91002,
                CurrentState = AdminKanbanJourneyStates.WaitingProviderReview,
                Closure = new AdminKanbanJourneyClosureRecord
                {
                    Status = AdminKanbanJourneyClosureStatuses.WaitingProviderReview
                }
            });
        kanbanService
            .Setup(service => service.ApplyJourneyStageAutomation(It.Is<AdminKanbanJourneyStageAutomationUpdateRequest>(request =>
                request.LeadId == 1002 &&
                request.TargetCurrentState == AdminKanbanJourneyStates.WaitingProviderReview &&
                request.HistoryEventType == "jornada_avaliacao_cliente_enviada")))
            .Returns(new AdminKanbanJourneyStageAutomationUpdateResult
            {
                LeadId = 1002,
                JourneyId = 91002,
                CurrentState = AdminKanbanJourneyStates.WaitingProviderReview
            });

        var sut = CreateSut(kanbanService.Object, linkService.Object, telegramClient.Object);

        var result = await sut.SubmitReviewAsync(
            "token-avaliacao",
            JourneyServiceClosureAudiences.Client,
            new JourneyServiceClosureReviewSubmissionRequest
            {
                Rating = 5,
                Comment = "Prestador chegou no horario e resolveu o problema.",
                WouldHireAgain = true
            },
            nowUtc);

        Assert.True(result.Success);
        Assert.Contains("prestador", result.Message, StringComparison.OrdinalIgnoreCase);
        kanbanService.VerifyAll();
        linkService.VerifyAll();
        telegramClient.VerifyNoOtherCalls();
    }

    private static JourneyServiceClosureService CreateSut(
        IAdminKanbanService kanbanService,
        IJourneyServiceClosureLinkService linkService,
        ITelegramBridgeDeliveryClient telegramClient)
    {
        var governanceService = new Mock<IJourneyGovernanceService>(MockBehavior.Strict);
        governanceService
            .Setup(service => service.EvaluateStep(JourneyGovernanceSteps.Closure, AdminKanbanJourneySourceChannels.Landing))
            .Returns(new JourneyGovernanceDecision
            {
                Allowed = true,
                Step = JourneyGovernanceSteps.Closure,
                Reason = "Etapa liberada pela governanca."
            });
        governanceService
            .Setup(service => service.ResolveOperationalException(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((reasonCode, fallbackSummary) => new JourneyOperationalExceptionPolicy
            {
                ReasonCode = reasonCode,
                Summary = fallbackSummary,
                HistoryEventType = reasonCode == JourneyGovernanceReasonCodes.ClientContestation
                    ? "jornada_conclusao_contestada"
                    : "jornada_conclusao_excecao"
            });

        return new JourneyServiceClosureService(
            kanbanService,
            governanceService.Object,
            linkService,
            telegramClient,
            Options.Create(new JourneyProviderNotificationOptions
            {
                Enabled = true,
                EmailEnabled = true,
                EmailTransport = "log",
                PublicBaseUrl = "https://www.consertapramim.com",
                LinkSigningSecret = "12345678901234567890123456789012",
                SenderEmail = "robot@consertapramim.com",
                SenderDisplayName = "ConsertaPraMim"
            }),
            Options.Create(new JourneyServiceClosureOptions
            {
                Enabled = true,
                CompletionLinkExpirationHours = 72,
                ReviewLinkExpirationHours = 168,
                LowScoreThreshold = 2
            }),
            NullLogger<JourneyServiceClosureService>.Instance);
    }

    private static AdminKanbanLeadDetailsRecord BuildLead(int leadId, int journeyId, Guid providerId)
    {
        return new AdminKanbanLeadDetailsRecord
        {
            Id = leadId,
            StageId = 19,
            StageName = AdminKanbanJourneyClientStageNames.WaitingCompletionConfirmation,
            BoardType = AdminKanbanBoardTypes.Clients,
            Name = "Cliente Jornada",
            Phone = "11999990000",
            Email = "cliente@teste.com",
            ServiceCategory = "Eletricista",
            PostalCode = "11701-200",
            City = "Praia Grande",
            Source = "telegram",
            Priority = "normal",
            StatusNote = "Aguardando confirmacao de conclusao.",
            CreatedAt = new DateTime(2026, 3, 26, 12, 0, 0, DateTimeKind.Utc),
            Journey = new AdminKanbanLeadJourneyRecord
            {
                JourneyId = journeyId,
                JourneyPublicId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                LeadId = leadId,
                BoardType = AdminKanbanBoardTypes.Clients,
                SourceChannel = AdminKanbanJourneySourceChannels.Telegram,
                SourceOrigin = "telegram-bot",
                CurrentState = AdminKanbanJourneyStates.WaitingCompletionConfirmation,
                PrimaryPhone = "11999990000",
                PrimaryEmail = "cliente@teste.com",
                Qualification = new AdminKanbanJourneyQualificationRecord
                {
                    NormalizedServiceCategoryName = "Eletricista",
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
                    ScheduledStartAtUtc = new DateTime(2026, 3, 26, 16, 0, 0, DateTimeKind.Utc),
                    ScheduledEndAtUtc = new DateTime(2026, 3, 26, 17, 0, 0, DateTimeKind.Utc)
                },
                Dispatch = new AdminKanbanJourneyDispatchRecord
                {
                    Status = AdminKanbanJourneyDispatchStatuses.Reserved,
                    ReservedProviderId = providerId,
                    ReservedProviderName = "Prestador Jornada",
                    ReservedProviderEmail = "prestador@teste.com",
                    ReservedProviderPhone = "13999998888"
                },
                Closure = new AdminKanbanJourneyClosureRecord
                {
                    Status = AdminKanbanJourneyClosureStatuses.WaitingClientConfirmation,
                    Outcome = AdminKanbanJourneyCompletionOutcomes.Completed,
                    ServiceInProgressAtUtc = new DateTime(2026, 3, 26, 13, 0, 0, DateTimeKind.Utc),
                    ProviderCompletionRequestedAtUtc = new DateTime(2026, 3, 26, 13, 5, 0, DateTimeKind.Utc),
                    ProviderCompletionSubmittedAtUtc = new DateTime(2026, 3, 26, 14, 0, 0, DateTimeKind.Utc),
                    ClientConfirmationRequestedAtUtc = new DateTime(2026, 3, 26, 14, 5, 0, DateTimeKind.Utc),
                    ClientReviewStatus = AdminKanbanJourneyReviewStatuses.Pending,
                    ProviderReviewStatus = AdminKanbanJourneyReviewStatuses.Pending
                }
            },
            History = []
        };
    }
}
