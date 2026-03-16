using AppMobileCPM.Integrations.Journey;
using AppMobileCPM.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Integrations.Journey;

public sealed class JourneyProviderDispatchServiceTests
{
    [Fact(DisplayName = "Journey Provider Dispatch | Deve enfileirar a primeira onda para jornada elegivel")]
    public async Task RunOnceAsync_DeveEnfileirarPrimeiraOndaParaJornadaElegivel()
    {
        var kanbanService = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        var nowUtc = new DateTime(2026, 3, 20, 15, 0, 0, DateTimeKind.Utc);
        var firstProviderId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var secondProviderId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var journey = BuildJourney(
            leadId: 301,
            journeyId: 901,
            currentState: AdminKanbanJourneyStates.MatchingInProgress,
            matchingStatus: AdminKanbanJourneyMatchingStatuses.EligibleProvidersFound,
            dispatch: new AdminKanbanJourneyDispatchRecord(),
            matchingCandidates:
            [
                BuildCandidate(firstProviderId, "Prestador Rank 1", rankPosition: 1, isEligible: true),
                BuildCandidate(secondProviderId, "Prestador Rank 2", rankPosition: 2, isEligible: true),
                BuildCandidate(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Prestador Bloqueado", rankPosition: 0, isEligible: false)
            ]);

        kanbanService
            .SetupSequence(service => service.ListJourneyStageAutomationCandidates(
                AdminKanbanBoardTypes.Clients,
                nowUtc,
                25))
            .Returns(
            [
                new AdminKanbanJourneyStageAutomationCandidateRecord
                {
                    LeadId = 301,
                    JourneyId = 901,
                    BoardType = AdminKanbanBoardTypes.Clients,
                    StageId = 15,
                    StageName = AdminKanbanJourneyClientStageNames.MatchingInProgress,
                    CurrentState = AdminKanbanJourneyStates.MatchingInProgress,
                    QualificationStatus = AdminKanbanJourneyQualificationStatuses.Qualified,
                    SchedulingStatus = AdminKanbanJourneySchedulingStatuses.Confirmed,
                    CreatedAtUtc = nowUtc.AddHours(-2),
                    LastIntakeAtUtc = nowUtc.AddHours(-1),
                    CurrentStateEnteredAtUtc = nowUtc.AddMinutes(-30)
                }
            ])
            .Returns([]);
        kanbanService
            .Setup(service => service.GetJourneyDetails(301))
            .Returns(journey);
        kanbanService
            .Setup(service => service.UpdateJourneyDispatch(
                301,
                It.Is<AdminKanbanJourneyDispatchUpdateRequest>(request =>
                    request.Status == AdminKanbanJourneyDispatchStatuses.WaveQueued &&
                    request.CurrentState == AdminKanbanJourneyStates.DispatchInProgress &&
                    request.CurrentWaveNumber == 1 &&
                    request.TargetsCreatedCount == 2 &&
                    request.Waves.Count == 1 &&
                    request.Targets.Count == 2 &&
                    request.Targets.All(item => item.WaveNumber == 1) &&
                    string.IsNullOrWhiteSpace(request.HistoryEventType)))) 
            .Returns(new AdminKanbanJourneyDispatchUpdateResult
            {
                LeadId = 301,
                JourneyId = 901,
                CurrentState = AdminKanbanJourneyStates.DispatchInProgress,
                Dispatch = new AdminKanbanJourneyDispatchRecord
                {
                    Status = AdminKanbanJourneyDispatchStatuses.WaveQueued,
                    CurrentWaveNumber = 1,
                    TargetsCreatedCount = 2
                }
            });
        kanbanService
            .Setup(service => service.EnqueueJourneyDispatchQueueItem(
                It.Is<AdminKanbanJourneyDispatchQueueEnqueueRequest>(request =>
                    request.LeadId == 301 &&
                    request.JourneyId == 901 &&
                    request.WaveNumber == 1 &&
                    request.ProviderId == firstProviderId)))
            .Returns(new AdminKanbanJourneyDispatchQueueItemRecord
            {
                Id = 1,
                LeadId = 301,
                JourneyId = 901,
                WaveNumber = 1,
                ProviderId = firstProviderId,
                TargetKey = BuildTargetKey(301, 1, firstProviderId),
                Status = AdminKanbanJourneyDispatchQueueStatuses.Pending,
                NextAttemptAt = nowUtc
            });
        kanbanService
            .Setup(service => service.EnqueueJourneyDispatchQueueItem(
                It.Is<AdminKanbanJourneyDispatchQueueEnqueueRequest>(request =>
                    request.LeadId == 301 &&
                    request.JourneyId == 901 &&
                    request.WaveNumber == 1 &&
                    request.ProviderId == secondProviderId)))
            .Returns(new AdminKanbanJourneyDispatchQueueItemRecord
            {
                Id = 2,
                LeadId = 301,
                JourneyId = 901,
                WaveNumber = 1,
                ProviderId = secondProviderId,
                TargetKey = BuildTargetKey(301, 1, secondProviderId),
                Status = AdminKanbanJourneyDispatchQueueStatuses.Pending,
                NextAttemptAt = nowUtc
            });
        kanbanService
            .Setup(service => service.ApplyJourneyStageAutomation(
                It.Is<AdminKanbanJourneyStageAutomationUpdateRequest>(request =>
                    request.LeadId == 301 &&
                    request.TargetStageName == AdminKanbanJourneyClientStageNames.DispatchInProgress &&
                    request.TargetCurrentState == AdminKanbanJourneyStates.DispatchInProgress &&
                    request.Origin == AdminKanbanJourneyAutomationOrigins.DispatchEngine &&
                    request.HistoryEventType == "jornada_disparo_onda_criada")))
            .Returns(new AdminKanbanJourneyStageAutomationUpdateResult
            {
                LeadId = 301,
                JourneyId = 901,
                FromStageId = 15,
                FromStageName = AdminKanbanJourneyClientStageNames.MatchingInProgress,
                ToStageId = 16,
                ToStageName = AdminKanbanJourneyClientStageNames.DispatchInProgress,
                CurrentState = AdminKanbanJourneyStates.DispatchInProgress,
                StageChanged = true
            });
        kanbanService
            .Setup(service => service.AcquireDueJourneyDispatchQueueItems(25, nowUtc, It.IsAny<string>()))
            .Returns([]);

        var sut = CreateSut(kanbanService.Object);

        var result = await sut.RunOnceAsync(nowUtc);

        Assert.Equal(1, result.ReadyCount);
        Assert.Equal(1, result.WavesQueuedCount);
        Assert.Equal(0, result.QueueProcessedCount);
        Assert.Equal(0, result.ExpiredWavesCount);
        Assert.Equal(0, result.ExhaustedJourneysCount);
        kanbanService.VerifyAll();
    }

    [Fact(DisplayName = "Journey Provider Dispatch | Deve disparar item da fila e aguardar aceite")]
    public async Task RunOnceAsync_DeveDispararItemDaFilaEAguardarAceite()
    {
        var kanbanService = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        var nowUtc = new DateTime(2026, 3, 21, 14, 0, 0, DateTimeKind.Utc);
        var providerId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var targetKey = BuildTargetKey(302, 1, providerId);
        var expectedDueAt = nowUtc.AddMinutes(45);
        var journey = BuildJourney(
            leadId: 302,
            journeyId: 902,
            currentState: AdminKanbanJourneyStates.DispatchInProgress,
            matchingStatus: AdminKanbanJourneyMatchingStatuses.EligibleProvidersFound,
            dispatch: new AdminKanbanJourneyDispatchRecord
            {
                Status = AdminKanbanJourneyDispatchStatuses.WaveQueued,
                CurrentWaveNumber = 1,
                MaxWaveNumber = 3,
                EligibleProvidersCount = 1,
                TargetsCreatedCount = 1,
                Waves =
                [
                    new AdminKanbanJourneyDispatchWaveRecord
                    {
                        WaveNumber = 1,
                        Status = AdminKanbanJourneyDispatchWaveStatuses.Queued,
                        EligibleSnapshotCount = 1,
                        TargetCount = 1,
                        CreatedAtUtc = nowUtc.AddMinutes(-10),
                        ExpiresAtUtc = expectedDueAt,
                        Summary = "Onda 1 preparada."
                    }
                ],
                Targets =
                [
                    new AdminKanbanJourneyDispatchTargetRecord
                    {
                        TargetKey = targetKey,
                        ProviderId = providerId,
                        ProviderName = "Prestador Aceite",
                        ProviderEmail = "aceite@teste.com",
                        ProviderPhone = "13999991111",
                        RankPosition = 1,
                        WaveNumber = 1,
                        Status = AdminKanbanJourneyDispatchTargetStatuses.Queued,
                        CreatedAtUtc = nowUtc.AddMinutes(-10),
                        ExpiresAtUtc = expectedDueAt,
                        Note = "Aguardando disparo."
                    }
                ]
            },
            matchingCandidates:
            [
                BuildCandidate(providerId, "Prestador Aceite", rankPosition: 1, isEligible: true)
            ]);

        kanbanService
            .SetupSequence(service => service.ListJourneyStageAutomationCandidates(
                AdminKanbanBoardTypes.Clients,
                nowUtc,
                25))
            .Returns([])
            .Returns([]);
        kanbanService
            .Setup(service => service.AcquireDueJourneyDispatchQueueItems(25, nowUtc, It.IsAny<string>()))
            .Returns(
            [
                new AdminKanbanJourneyDispatchQueueItemRecord
                {
                    Id = 10,
                    LeadId = 302,
                    JourneyId = 902,
                    WaveNumber = 1,
                    ProviderId = providerId,
                    TargetKey = targetKey,
                    PayloadJson = "{}",
                    Status = AdminKanbanJourneyDispatchQueueStatuses.Processing,
                    AttemptCount = 1,
                    MaxAttempts = 3,
                    NextAttemptAt = nowUtc,
                    LastAttemptAt = nowUtc
                }
            ]);
        kanbanService
            .Setup(service => service.GetJourneyDetails(302))
            .Returns(journey);
        kanbanService
            .Setup(service => service.UpdateJourneyDispatch(
                302,
                It.Is<AdminKanbanJourneyDispatchUpdateRequest>(request =>
                    request.Status == AdminKanbanJourneyDispatchStatuses.WaitingAcceptance &&
                    request.CurrentState == AdminKanbanJourneyStates.WaitingProviderAcceptance &&
                    request.WaitingAcceptanceUntilUtc == expectedDueAt &&
                    request.SentTargetsCount == 1 &&
                    request.PendingTargetsCount == 1 &&
                    request.HistoryEventType == string.Empty))))
            .Returns(new AdminKanbanJourneyDispatchUpdateResult
            {
                LeadId = 302,
                JourneyId = 902,
                CurrentState = AdminKanbanJourneyStates.WaitingProviderAcceptance,
                Dispatch = new AdminKanbanJourneyDispatchRecord
                {
                    Status = AdminKanbanJourneyDispatchStatuses.WaitingAcceptance,
                    CurrentWaveNumber = 1,
                    SentTargetsCount = 1,
                    PendingTargetsCount = 1,
                    WaitingAcceptanceUntilUtc = expectedDueAt
                }
            });
        kanbanService
            .Setup(service => service.ApplyJourneyStageAutomation(
                It.Is<AdminKanbanJourneyStageAutomationUpdateRequest>(request =>
                    request.LeadId == 302 &&
                    request.TargetStageName == AdminKanbanJourneyClientStageNames.WaitingAcceptance &&
                    request.TargetCurrentState == AdminKanbanJourneyStates.WaitingProviderAcceptance &&
                    request.ActiveTimerCode == AdminKanbanJourneyTimerCodes.PendingAcceptance &&
                    request.ActiveTimerDueAtUtc == expectedDueAt &&
                    request.HistoryEventType == "jornada_disparo_onda_enviada")))
            .Returns(new AdminKanbanJourneyStageAutomationUpdateResult
            {
                LeadId = 302,
                JourneyId = 902,
                FromStageId = 16,
                FromStageName = AdminKanbanJourneyClientStageNames.DispatchInProgress,
                ToStageId = 17,
                ToStageName = AdminKanbanJourneyClientStageNames.WaitingAcceptance,
                CurrentState = AdminKanbanJourneyStates.WaitingProviderAcceptance,
                StageChanged = true
            });
        kanbanService
            .Setup(service => service.FinalizeJourneyDispatchQueueItem(
                It.Is<AdminKanbanJourneyDispatchQueueFinalizeRequest>(request =>
                    request.QueueItemId == 10 &&
                    request.FinalStatus == AdminKanbanJourneyDispatchQueueStatuses.Processed &&
                    request.FinalizedAt == nowUtc &&
                    request.ClearLastError))))
            .Returns(new AdminKanbanJourneyDispatchQueueItemRecord
            {
                Id = 10,
                LeadId = 302,
                JourneyId = 902,
                WaveNumber = 1,
                ProviderId = providerId,
                TargetKey = targetKey,
                Status = AdminKanbanJourneyDispatchQueueStatuses.Processed,
                AttemptCount = 1,
                MaxAttempts = 3,
                NextAttemptAt = nowUtc,
                ProcessedAt = nowUtc
            });

        var sut = CreateSut(kanbanService.Object);

        var result = await sut.RunOnceAsync(nowUtc);

        Assert.Equal(0, result.ReadyCount);
        Assert.Equal(0, result.WavesQueuedCount);
        Assert.Equal(1, result.QueueProcessedCount);
        Assert.Equal(0, result.ExpiredWavesCount);
        Assert.Equal(0, result.ExhaustedJourneysCount);
        kanbanService.VerifyAll();
    }

    [Fact(DisplayName = "Journey Provider Dispatch | Deve expirar a onda atual e enfileirar a proxima")]
    public async Task RunOnceAsync_DeveExpirarAOndaAtualEEnfileirarAProxima()
    {
        var kanbanService = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        var nowUtc = new DateTime(2026, 3, 22, 16, 0, 0, DateTimeKind.Utc);
        var firstProviderId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var secondProviderId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var journey = BuildJourney(
            leadId: 303,
            journeyId: 903,
            currentState: AdminKanbanJourneyStates.WaitingProviderAcceptance,
            matchingStatus: AdminKanbanJourneyMatchingStatuses.EligibleProvidersFound,
            dispatch: new AdminKanbanJourneyDispatchRecord
            {
                Status = AdminKanbanJourneyDispatchStatuses.WaitingAcceptance,
                CurrentWaveNumber = 1,
                MaxWaveNumber = 3,
                EligibleProvidersCount = 2,
                TargetsCreatedCount = 1,
                SentTargetsCount = 1,
                PendingTargetsCount = 1,
                WaitingAcceptanceUntilUtc = nowUtc.AddMinutes(-1),
                Waves =
                [
                    new AdminKanbanJourneyDispatchWaveRecord
                    {
                        WaveNumber = 1,
                        Status = AdminKanbanJourneyDispatchWaveStatuses.Active,
                        EligibleSnapshotCount = 2,
                        TargetCount = 1,
                        CreatedAtUtc = nowUtc.AddHours(-1),
                        ActivatedAtUtc = nowUtc.AddMinutes(-50),
                        ExpiresAtUtc = nowUtc.AddMinutes(-1),
                        Summary = "Onda 1 aguardando aceite."
                    }
                ],
                Targets =
                [
                    new AdminKanbanJourneyDispatchTargetRecord
                    {
                        TargetKey = BuildTargetKey(303, 1, firstProviderId),
                        ProviderId = firstProviderId,
                        ProviderName = "Prestador Onda 1",
                        ProviderEmail = "onda1@teste.com",
                        ProviderPhone = "13999992222",
                        RankPosition = 1,
                        WaveNumber = 1,
                        Status = AdminKanbanJourneyDispatchTargetStatuses.Sent,
                        CreatedAtUtc = nowUtc.AddHours(-1),
                        SentAtUtc = nowUtc.AddMinutes(-50),
                        ExpiresAtUtc = nowUtc.AddMinutes(-1),
                        Note = "Oportunidade enviada."
                    }
                ]
            },
            matchingCandidates:
            [
                BuildCandidate(firstProviderId, "Prestador Onda 1", rankPosition: 1, isEligible: true),
                BuildCandidate(secondProviderId, "Prestador Onda 2", rankPosition: 2, isEligible: true)
            ]);

        kanbanService
            .SetupSequence(service => service.ListJourneyStageAutomationCandidates(
                AdminKanbanBoardTypes.Clients,
                nowUtc,
                25))
            .Returns([])
            .Returns(
            [
                new AdminKanbanJourneyStageAutomationCandidateRecord
                {
                    LeadId = 303,
                    JourneyId = 903,
                    BoardType = AdminKanbanBoardTypes.Clients,
                    StageId = 17,
                    StageName = AdminKanbanJourneyClientStageNames.WaitingAcceptance,
                    CurrentState = AdminKanbanJourneyStates.WaitingProviderAcceptance,
                    QualificationStatus = AdminKanbanJourneyQualificationStatuses.Qualified,
                    SchedulingStatus = AdminKanbanJourneySchedulingStatuses.Confirmed,
                    CreatedAtUtc = nowUtc.AddHours(-3),
                    LastIntakeAtUtc = nowUtc.AddHours(-2),
                    CurrentStateEnteredAtUtc = nowUtc.AddHours(-1),
                    DispatchStatus = AdminKanbanJourneyDispatchStatuses.WaitingAcceptance,
                    DispatchCurrentWaveNumber = 1,
                    DispatchWaitingAcceptanceUntilUtc = nowUtc.AddMinutes(-1)
                }
            ]);
        kanbanService
            .Setup(service => service.AcquireDueJourneyDispatchQueueItems(25, nowUtc, It.IsAny<string>()))
            .Returns([]);
        kanbanService
            .Setup(service => service.GetJourneyDetails(303))
            .Returns(journey);
        kanbanService
            .Setup(service => service.UpdateJourneyDispatch(
                303,
                It.Is<AdminKanbanJourneyDispatchUpdateRequest>(request =>
                    request.Status == AdminKanbanJourneyDispatchStatuses.WaveQueued &&
                    request.CurrentState == AdminKanbanJourneyStates.DispatchInProgress &&
                    request.CurrentWaveNumber == 2 &&
                    request.Waves.Count == 2 &&
                    request.Targets.Count == 2 &&
                    request.Waves.Any(item => item.WaveNumber == 1 && item.Status == AdminKanbanJourneyDispatchWaveStatuses.Expired) &&
                    request.Waves.Any(item => item.WaveNumber == 2 && item.Status == AdminKanbanJourneyDispatchWaveStatuses.Queued) &&
                    string.IsNullOrWhiteSpace(request.HistoryEventType)))) 
            .Returns(new AdminKanbanJourneyDispatchUpdateResult
            {
                LeadId = 303,
                JourneyId = 903,
                CurrentState = AdminKanbanJourneyStates.DispatchInProgress,
                Dispatch = new AdminKanbanJourneyDispatchRecord
                {
                    Status = AdminKanbanJourneyDispatchStatuses.WaveQueued,
                    CurrentWaveNumber = 2,
                    TargetsCreatedCount = 2,
                    ExpiredTargetsCount = 1
                }
            });
        kanbanService
            .Setup(service => service.EnqueueJourneyDispatchQueueItem(
                It.Is<AdminKanbanJourneyDispatchQueueEnqueueRequest>(request =>
                    request.LeadId == 303 &&
                    request.JourneyId == 903 &&
                    request.WaveNumber == 2 &&
                    request.ProviderId == secondProviderId)))
            .Returns(new AdminKanbanJourneyDispatchQueueItemRecord
            {
                Id = 20,
                LeadId = 303,
                JourneyId = 903,
                WaveNumber = 2,
                ProviderId = secondProviderId,
                TargetKey = BuildTargetKey(303, 2, secondProviderId),
                Status = AdminKanbanJourneyDispatchQueueStatuses.Pending,
                NextAttemptAt = nowUtc
            });
        kanbanService
            .Setup(service => service.ApplyJourneyStageAutomation(
                It.Is<AdminKanbanJourneyStageAutomationUpdateRequest>(request =>
                    request.LeadId == 303 &&
                    request.TargetStageName == AdminKanbanJourneyClientStageNames.DispatchInProgress &&
                    request.TargetCurrentState == AdminKanbanJourneyStates.DispatchInProgress &&
                    request.HistoryEventType == "jornada_disparo_onda_expirada")))
            .Returns(new AdminKanbanJourneyStageAutomationUpdateResult
            {
                LeadId = 303,
                JourneyId = 903,
                FromStageId = 17,
                FromStageName = AdminKanbanJourneyClientStageNames.WaitingAcceptance,
                ToStageId = 16,
                ToStageName = AdminKanbanJourneyClientStageNames.DispatchInProgress,
                CurrentState = AdminKanbanJourneyStates.DispatchInProgress,
                StageChanged = true
            });

        var sut = CreateSut(kanbanService.Object);

        var result = await sut.RunOnceAsync(nowUtc);

        Assert.Equal(0, result.ReadyCount);
        Assert.Equal(1, result.WavesQueuedCount);
        Assert.Equal(0, result.QueueProcessedCount);
        Assert.Equal(1, result.ExpiredWavesCount);
        Assert.Equal(0, result.ExhaustedJourneysCount);
        kanbanService.VerifyAll();
    }

    private static JourneyProviderDispatchService CreateSut(
        IAdminKanbanService kanbanService,
        IJourneyProviderDispatchNotificationService? notificationService = null)
    {
        if (notificationService is null)
        {
            var notificationServiceMock = new Mock<IJourneyProviderDispatchNotificationService>(MockBehavior.Strict);
            notificationServiceMock
                .Setup(service => service.SendOpportunityAsync(
                    It.IsAny<JourneyProviderDispatchNotificationRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JourneyProviderDispatchNotificationResult
                {
                    Success = true,
                    DeliveryChannel = "email",
                    DeliveryStatus = AdminKanbanJourneyDispatchDeliveryStatuses.Sent,
                    Message = "Oportunidade enviada por e-mail com links assinados."
                });
            notificationService = notificationServiceMock.Object;
        }

        return new JourneyProviderDispatchService(
            kanbanService,
            notificationService,
            Options.Create(new JourneyProviderDispatchOptions
            {
                Enabled = true,
                WorkerEnabled = true,
                WorkerIntervalSeconds = 30,
                WorkerBatchSize = 25,
                QueueBatchSize = 25,
                WaveSize = 2,
                MaxWaves = 3,
                AcceptanceTimeoutMinutes = 45,
                QueueMaxAttempts = 3,
                DispatchStrategy = "top_ranked_waves"
            }),
            NullLogger<JourneyProviderDispatchService>.Instance);
    }

    private static AdminKanbanLeadJourneyRecord BuildJourney(
        int leadId,
        int journeyId,
        string currentState,
        string matchingStatus,
        AdminKanbanJourneyDispatchRecord dispatch,
        IReadOnlyList<AdminKanbanJourneyProviderMatchRecord> matchingCandidates)
    {
        return new AdminKanbanLeadJourneyRecord
        {
            JourneyId = journeyId,
            JourneyPublicId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            LeadId = leadId,
            BoardType = AdminKanbanBoardTypes.Clients,
            SourceChannel = AdminKanbanJourneySourceChannels.Telegram,
            SourceOrigin = "telegram-bot",
            CurrentState = currentState,
            ChatbotConversationId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            ChannelConversationId = "5513997000000",
            TelegramChatId = 5513997000000,
            PrimaryPhone = "+5513997000000",
            PrimaryEmail = "cliente.dispatch@teste.com",
            CreatedAt = new DateTime(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc),
            LastIntakeAt = new DateTime(2026, 3, 20, 12, 5, 0, DateTimeKind.Utc),
            Qualification = new AdminKanbanJourneyQualificationRecord
            {
                Status = AdminKanbanJourneyQualificationStatuses.Qualified,
                Source = AdminKanbanJourneyQualificationSources.Deterministic,
                ConfidenceScore = 0.96m,
                HasRequiredData = true,
                NeedsConfirmation = false,
                NormalizedServiceCategoryId = "eletricista",
                NormalizedServiceCategoryName = "Eletricista",
                ProblemContext = "Chuveiro queimado.",
                Street = "Rua Bahia",
                Neighborhood = "Ocian",
                City = "Praia Grande",
                State = "SP",
                PostalCode = "11701-200",
                Latitude = -24.025331,
                Longitude = -46.469028,
                Summary = "Jornada pronta para matching e disparo."
            },
            Scheduling = new AdminKanbanJourneySchedulingRecord
            {
                Status = AdminKanbanJourneySchedulingStatuses.Confirmed,
                Summary = "Janela confirmada.",
                ScheduledStartAtUtc = new DateTime(2026, 3, 20, 18, 0, 0, DateTimeKind.Utc),
                ScheduledEndAtUtc = new DateTime(2026, 3, 20, 19, 0, 0, DateTimeKind.Utc)
            },
            Matching = new AdminKanbanJourneyMatchingRecord
            {
                Status = matchingStatus,
                Summary = "Matching pronto para disparo.",
                RequestedCategory = "Eletricista",
                RequestedSubcategory = "chuveiro",
                EvaluatedProvidersCount = matchingCandidates.Count,
                EligibleProvidersCount = matchingCandidates.Count(item => item.IsEligible),
                LastRunAtUtc = new DateTime(2026, 3, 20, 13, 0, 0, DateTimeKind.Utc),
                Candidates = matchingCandidates
            },
            Dispatch = dispatch
        };
    }

    private static AdminKanbanJourneyProviderMatchRecord BuildCandidate(
        Guid providerId,
        string providerName,
        int rankPosition,
        bool isEligible)
    {
        return new AdminKanbanJourneyProviderMatchRecord
        {
            ProviderId = providerId,
            ProviderName = providerName,
            ProviderEmail = $"{providerId:N}@teste.com",
            ProviderPhone = "13999990000",
            IsEligible = isEligible,
            RankPosition = rankPosition,
            Score = isEligible ? 90m - rankPosition : 0m,
            DistanceKm = rankPosition,
            CoverageRadiusKm = 15d,
            Rating = 4.8d,
            ReviewCount = 30,
            OperationalStatus = "Online",
            ClientPreference = "PF e PJ",
            RequestedCategory = "Eletricista",
            RequestedSubcategory = "chuveiro",
            CategoryMatched = isEligible,
            SubcategoryMatched = isEligible,
            RadiusMatched = isEligible,
            AvailabilityMatched = isEligible,
            CapacityMatched = isEligible,
            BlockReasonCode = isEligible ? string.Empty : "blocked",
            BlockReasonLabel = isEligible ? string.Empty : "Bloqueado",
            Summary = isEligible ? "Elegivel para a onda." : "Nao elegivel para a onda."
        };
    }

    private static string BuildTargetKey(int leadId, int waveNumber, Guid providerId)
    {
        return $"lead:{leadId}:wave:{waveNumber}:provider:{providerId:N}";
    }
}
