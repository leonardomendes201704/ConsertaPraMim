using AppMobileCPM.Integrations.Journey;
using AppMobileCPM.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Integrations.Journey;

public sealed class JourneyProviderMatchingServiceTests
{
    [Fact(DisplayName = "Journey Provider Matching | Deve ranquear prestadores elegiveis e avancar jornada para em matching")]
    public async Task RunOnceAsync_DeveRanquearPrestadoresElegiveisEAvancarJornada()
    {
        var kanbanService = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        var nowUtc = new DateTime(2026, 3, 19, 12, 0, 0, DateTimeKind.Utc);
        var scheduledStartAtUtc = new DateTime(2026, 3, 19, 13, 0, 0, DateTimeKind.Utc);
        var scheduledEndAtUtc = new DateTime(2026, 3, 19, 14, 0, 0, DateTimeKind.Utc);
        var journey = BuildJourney(
            leadId: 201,
            journeyId: 801,
            currentState: AdminKanbanJourneyStates.AppointmentConfirmed,
            scheduledStartAtUtc,
            scheduledEndAtUtc,
            latitude: -24.025331,
            longitude: -46.469028,
            categoryName: "Eletricista",
            problemContext: "Preciso trocar o chuveiro hoje.");

        kanbanService
            .Setup(service => service.ListJourneyStageAutomationCandidates(
                AdminKanbanBoardTypes.Clients,
                nowUtc,
                25))
            .Returns(
            [
                new AdminKanbanJourneyStageAutomationCandidateRecord
                {
                    LeadId = 201,
                    JourneyId = 801,
                    BoardType = AdminKanbanBoardTypes.Clients,
                    StageId = 14,
                    StageName = AdminKanbanJourneyClientStageNames.AppointmentConfirmed,
                    CurrentState = AdminKanbanJourneyStates.AppointmentConfirmed,
                    QualificationStatus = AdminKanbanJourneyQualificationStatuses.Qualified,
                    SchedulingStatus = AdminKanbanJourneySchedulingStatuses.Confirmed,
                    CreatedAtUtc = nowUtc.AddHours(-3),
                    LastIntakeAtUtc = nowUtc.AddHours(-2),
                    CurrentStateEnteredAtUtc = nowUtc.AddHours(-1)
                }
            ]);
        kanbanService
            .Setup(service => service.GetJourneyDetails(201))
            .Returns(journey);
        kanbanService
            .Setup(service => service.ListJourneyProviderProfiles(scheduledStartAtUtc, scheduledEndAtUtc))
            .Returns(
            [
                BuildProvider(
                    providerId: Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
                    providerName: "Prestador Elegivel",
                    baseLatitude: -24.030000,
                    baseLongitude: -46.470000,
                    radiusKm: 12d,
                    categoryCodes: [1],
                    specialtyHints: "chuveiro | eletrica",
                    conflictingAppointmentsCount: 0,
                    availabilityRules:
                    [
                        new AdminKanbanJourneyProviderAvailabilityRuleRecord
                        {
                            ProviderId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
                            DayOfWeekCode = 4,
                            StartTime = TimeSpan.FromHours(8),
                            EndTime = TimeSpan.FromHours(18),
                            SlotDurationMinutes = 60
                        }
                    ]),
                BuildProvider(
                    providerId: Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"),
                    providerName: "Prestador Fora do Raio",
                    baseLatitude: -23.850000,
                    baseLongitude: -46.650000,
                    radiusKm: 5d,
                    categoryCodes: [1],
                    specialtyHints: "chuveiro",
                    conflictingAppointmentsCount: 0,
                    availabilityRules:
                    [
                        new AdminKanbanJourneyProviderAvailabilityRuleRecord
                        {
                            ProviderId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"),
                            DayOfWeekCode = 4,
                            StartTime = TimeSpan.FromHours(8),
                            EndTime = TimeSpan.FromHours(18),
                            SlotDurationMinutes = 60
                        }
                    ])
            ]);
        kanbanService
            .Setup(service => service.UpdateJourneyMatching(
                201,
                It.Is<AdminKanbanJourneyMatchingUpdateRequest>(request =>
                    request.Status == AdminKanbanJourneyMatchingStatuses.EligibleProvidersFound &&
                    request.RequestedCategory == "Eletricista" &&
                    request.RequestedSubcategory == "chuveiro" &&
                    request.EvaluatedProvidersCount == 2 &&
                    request.EligibleProvidersCount == 1 &&
                    request.Candidates.Count == 2 &&
                    request.Candidates.Any(item => item.ProviderName == "Prestador Elegivel" && item.IsEligible && item.RankPosition == 1) &&
                    request.Candidates.Any(item => item.ProviderName == "Prestador Fora do Raio" && !item.IsEligible && item.BlockReasonCode == "outside_radius"))))
            .Returns(new AdminKanbanJourneyMatchingUpdateResult
            {
                LeadId = 201,
                JourneyId = 801,
                CurrentState = AdminKanbanJourneyStates.AppointmentConfirmed,
                Matching = new AdminKanbanJourneyMatchingRecord
                {
                    Status = AdminKanbanJourneyMatchingStatuses.EligibleProvidersFound
                }
            });
        kanbanService
            .Setup(service => service.ApplyJourneyStageAutomation(
                It.Is<AdminKanbanJourneyStageAutomationUpdateRequest>(request =>
                    request.LeadId == 201 &&
                    request.TargetStageName == AdminKanbanJourneyClientStageNames.MatchingInProgress &&
                    request.TargetCurrentState == AdminKanbanJourneyStates.MatchingInProgress &&
                    request.Origin == AdminKanbanJourneyAutomationOrigins.MatchingEngine &&
                    request.HistoryEventType == "jornada_matching_concluido")))
            .Returns(new AdminKanbanJourneyStageAutomationUpdateResult
            {
                LeadId = 201,
                JourneyId = 801,
                FromStageId = 14,
                FromStageName = AdminKanbanJourneyClientStageNames.AppointmentConfirmed,
                ToStageId = 15,
                ToStageName = AdminKanbanJourneyClientStageNames.MatchingInProgress,
                CurrentState = AdminKanbanJourneyStates.MatchingInProgress,
                StageChanged = true
            });

        var sut = CreateSut(kanbanService.Object);

        var result = await sut.RunOnceAsync(nowUtc);

        Assert.Equal(1, result.ScannedCount);
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(1, result.EligibleJourneysCount);
        Assert.Equal(0, result.NoCoverageJourneysCount);
        kanbanService.VerifyAll();
    }

    [Fact(DisplayName = "Journey Provider Matching | Deve marcar jornada como sem cobertura quando nao houver prestador elegivel")]
    public async Task RunOnceAsync_DeveMarcarJornadaComoSemCobertura()
    {
        var kanbanService = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        var nowUtc = new DateTime(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);
        var scheduledStartAtUtc = new DateTime(2026, 3, 20, 13, 0, 0, DateTimeKind.Utc);
        var scheduledEndAtUtc = new DateTime(2026, 3, 20, 14, 0, 0, DateTimeKind.Utc);
        var journey = BuildJourney(
            leadId: 202,
            journeyId: 802,
            currentState: AdminKanbanJourneyStates.AppointmentConfirmed,
            scheduledStartAtUtc,
            scheduledEndAtUtc,
            latitude: -24.025331,
            longitude: -46.469028,
            categoryName: "Encanador",
            problemContext: "Vazamento no cano da cozinha.");

        kanbanService
            .Setup(service => service.ListJourneyStageAutomationCandidates(
                AdminKanbanBoardTypes.Clients,
                nowUtc,
                25))
            .Returns(
            [
                new AdminKanbanJourneyStageAutomationCandidateRecord
                {
                    LeadId = 202,
                    JourneyId = 802,
                    BoardType = AdminKanbanBoardTypes.Clients,
                    StageId = 14,
                    StageName = AdminKanbanJourneyClientStageNames.AppointmentConfirmed,
                    CurrentState = AdminKanbanJourneyStates.AppointmentConfirmed,
                    QualificationStatus = AdminKanbanJourneyQualificationStatuses.Qualified,
                    SchedulingStatus = AdminKanbanJourneySchedulingStatuses.Confirmed,
                    CreatedAtUtc = nowUtc.AddHours(-3),
                    LastIntakeAtUtc = nowUtc.AddHours(-2),
                    CurrentStateEnteredAtUtc = nowUtc.AddHours(-1)
                }
            ]);
        kanbanService
            .Setup(service => service.GetJourneyDetails(202))
            .Returns(journey);
        kanbanService
            .Setup(service => service.ListJourneyProviderProfiles(scheduledStartAtUtc, scheduledEndAtUtc))
            .Returns(
            [
                BuildProvider(
                    providerId: Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"),
                    providerName: "Prestador Sem Cobertura",
                    baseLatitude: -24.200000,
                    baseLongitude: -46.800000,
                    radiusKm: 3d,
                    categoryCodes: [2],
                    specialtyHints: "vazamento | cano",
                    conflictingAppointmentsCount: 1,
                    availabilityRules:
                    [
                        new AdminKanbanJourneyProviderAvailabilityRuleRecord
                        {
                            ProviderId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"),
                            DayOfWeekCode = 5,
                            StartTime = TimeSpan.FromHours(8),
                            EndTime = TimeSpan.FromHours(18),
                            SlotDurationMinutes = 60
                        }
                    ])
            ]);
        kanbanService
            .Setup(service => service.UpdateJourneyMatching(
                202,
                It.Is<AdminKanbanJourneyMatchingUpdateRequest>(request =>
                    request.Status == AdminKanbanJourneyMatchingStatuses.NoCoverage &&
                    request.RequestedCategory == "Encanador" &&
                    request.RequestedSubcategory == "vazamento" &&
                    request.EvaluatedProvidersCount == 1 &&
                    request.EligibleProvidersCount == 0 &&
                    request.Candidates.Count == 1 &&
                    request.Candidates[0].ProviderName == "Prestador Sem Cobertura" &&
                    request.Candidates[0].BlockReasonCode == "outside_radius"))))
            .Returns(new AdminKanbanJourneyMatchingUpdateResult
            {
                LeadId = 202,
                JourneyId = 802,
                CurrentState = AdminKanbanJourneyStates.AppointmentConfirmed,
                Matching = new AdminKanbanJourneyMatchingRecord
                {
                    Status = AdminKanbanJourneyMatchingStatuses.NoCoverage
                }
            });
        kanbanService
            .Setup(service => service.ApplyJourneyStageAutomation(
                It.Is<AdminKanbanJourneyStageAutomationUpdateRequest>(request =>
                    request.LeadId == 202 &&
                    request.TargetStageName == AdminKanbanJourneyClientStageNames.NoMatch &&
                    request.TargetCurrentState == AdminKanbanJourneyStates.NoMatch &&
                    request.Origin == AdminKanbanJourneyAutomationOrigins.MatchingEngine &&
                    request.HistoryEventType == "jornada_matching_sem_cobertura")))
            .Returns(new AdminKanbanJourneyStageAutomationUpdateResult
            {
                LeadId = 202,
                JourneyId = 802,
                FromStageId = 14,
                FromStageName = AdminKanbanJourneyClientStageNames.AppointmentConfirmed,
                ToStageId = 19,
                ToStageName = AdminKanbanJourneyClientStageNames.NoMatch,
                CurrentState = AdminKanbanJourneyStates.NoMatch,
                StageChanged = true
            });

        var sut = CreateSut(kanbanService.Object);

        var result = await sut.RunOnceAsync(nowUtc);

        Assert.Equal(1, result.ScannedCount);
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.EligibleJourneysCount);
        Assert.Equal(1, result.NoCoverageJourneysCount);
        kanbanService.VerifyAll();
    }

    private static JourneyProviderMatchingService CreateSut(IAdminKanbanService kanbanService)
    {
        return new JourneyProviderMatchingService(
            kanbanService,
            Options.Create(new JourneyProviderMatchingOptions
            {
                Enabled = true,
                WorkerEnabled = true,
                WorkerIntervalSeconds = 30,
                WorkerBatchSize = 25,
                MaxCandidatesToPersist = 12,
                Timezone = "America/Sao_Paulo"
            }),
            NullLogger<JourneyProviderMatchingService>.Instance);
    }

    private static AdminKanbanLeadJourneyRecord BuildJourney(
        int leadId,
        int journeyId,
        string currentState,
        DateTime scheduledStartAtUtc,
        DateTime scheduledEndAtUtc,
        double latitude,
        double longitude,
        string categoryName,
        string problemContext)
    {
        return new AdminKanbanLeadJourneyRecord
        {
            JourneyId = journeyId,
            JourneyPublicId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            LeadId = leadId,
            BoardType = AdminKanbanBoardTypes.Clients,
            SourceChannel = AdminKanbanJourneySourceChannels.Telegram,
            SourceOrigin = "telegram-bot",
            CurrentState = currentState,
            ChatbotConversationId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            ChannelConversationId = "5513997114400",
            TelegramChatId = 5513997114400,
            PrimaryPhone = "+5513997114400",
            PrimaryEmail = "cliente.matching@teste.com",
            CreatedAt = scheduledStartAtUtc.AddHours(-4),
            LastIntakeAt = scheduledStartAtUtc.AddHours(-3),
            Qualification = new AdminKanbanJourneyQualificationRecord
            {
                Status = AdminKanbanJourneyQualificationStatuses.Qualified,
                Source = AdminKanbanJourneyQualificationSources.Deterministic,
                ConfidenceScore = 0.95m,
                HasRequiredData = true,
                NeedsConfirmation = false,
                NormalizedServiceCategoryId = categoryName.ToLowerInvariant(),
                NormalizedServiceCategoryName = categoryName,
                ProblemContext = problemContext,
                Street = "Rua Bahia",
                Neighborhood = "Ocian",
                City = "Praia Grande",
                State = "SP",
                PostalCode = "11701-200",
                Latitude = latitude,
                Longitude = longitude,
                Summary = "Cliente pronto para matching."
            },
            Scheduling = new AdminKanbanJourneySchedulingRecord
            {
                Status = AdminKanbanJourneySchedulingStatuses.Confirmed,
                Summary = "Agenda confirmada.",
                ConfirmedAtUtc = scheduledStartAtUtc.AddHours(-2),
                ScheduledStartAtUtc = scheduledStartAtUtc,
                ScheduledEndAtUtc = scheduledEndAtUtc
            }
        };
    }

    private static AdminKanbanJourneyProviderProfileRecord BuildProvider(
        Guid providerId,
        string providerName,
        double baseLatitude,
        double baseLongitude,
        double radiusKm,
        IReadOnlyList<int> categoryCodes,
        string specialtyHints,
        int conflictingAppointmentsCount,
        IReadOnlyList<AdminKanbanJourneyProviderAvailabilityRuleRecord> availabilityRules)
    {
        return new AdminKanbanJourneyProviderProfileRecord
        {
            ProviderId = providerId,
            ProviderName = providerName,
            ProviderEmail = $"{providerName.ToLowerInvariant().Replace(' ', '.')}@teste.com",
            ProviderPhone = "13999990123",
            IsActive = true,
            IsOnboardingCompleted = true,
            OnboardingStatusCode = 2,
            RadiusKm = radiusKm,
            BaseZipCode = "11701-200",
            BaseLatitude = baseLatitude,
            BaseLongitude = baseLongitude,
            HasOperationalCompliancePending = false,
            OperationalStatusCode = 1,
            ClientPreferenceCode = 0,
            IsVerified = true,
            TrustStatusCode = 2,
            RiskLevelCode = 1,
            Rating = 4.7d,
            ReviewCount = 27,
            CategoryCodes = categoryCodes,
            SpecialtyHints = specialtyHints,
            ConflictingAppointmentsCount = conflictingAppointmentsCount,
            AvailabilityRules = availabilityRules
        };
    }
}
