using AppMobileCPM.Integrations.Journey;
using AppMobileCPM.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Integrations.Journey;

public sealed class JourneyStageAutomationServiceTests
{
    [Fact(DisplayName = "Journey Stage Automation | Deve mover jornada com dados pendentes para etapa correta e armar timer")]
    public async Task RunOnceAsync_DeveMoverDadosPendentesEArmarTimer()
    {
        var kanbanService = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        var nowUtc = new DateTime(2026, 3, 18, 14, 0, 0, DateTimeKind.Utc);
        var stateEnteredAtUtc = new DateTime(2026, 3, 18, 13, 0, 0, DateTimeKind.Utc);

        kanbanService
            .Setup(service => service.ListJourneyStageAutomationCandidates(
                AdminKanbanBoardTypes.Clients,
                nowUtc,
                50))
            .Returns(
            [
                new AdminKanbanJourneyStageAutomationCandidateRecord
                {
                    LeadId = 101,
                    JourneyId = 701,
                    BoardType = AdminKanbanBoardTypes.Clients,
                    StageId = 10,
                    StageName = AdminKanbanJourneyClientStageNames.AutomatedTriage,
                    CurrentState = AdminKanbanJourneyStates.QualificationPending,
                    QualificationStatus = AdminKanbanJourneyQualificationStatuses.Pending,
                    SchedulingStatus = AdminKanbanJourneySchedulingStatuses.NotStarted,
                    CreatedAtUtc = stateEnteredAtUtc.AddMinutes(-15),
                    LastIntakeAtUtc = stateEnteredAtUtc,
                    CurrentStateEnteredAtUtc = stateEnteredAtUtc
                }
            ]);
        kanbanService
            .Setup(service => service.ApplyJourneyStageAutomation(
                It.Is<AdminKanbanJourneyStageAutomationUpdateRequest>(request =>
                    request.LeadId == 101 &&
                    request.TargetStageName == AdminKanbanJourneyClientStageNames.PendingData &&
                    request.TargetCurrentState == AdminKanbanJourneyStates.QualificationPending &&
                    request.Origin == AdminKanbanJourneyAutomationOrigins.StateMachine &&
                    request.ActiveTimerCode == AdminKanbanJourneyTimerCodes.PendingData &&
                    request.ActiveTimerDueAtUtc == stateEnteredAtUtc.AddMinutes(120) &&
                    request.HistoryEventType == "jornada_kanban_automatizada")))
            .Returns(new AdminKanbanJourneyStageAutomationUpdateResult
            {
                LeadId = 101,
                JourneyId = 701,
                FromStageId = 10,
                FromStageName = AdminKanbanJourneyClientStageNames.AutomatedTriage,
                ToStageId = 11,
                ToStageName = AdminKanbanJourneyClientStageNames.PendingData,
                CurrentState = AdminKanbanJourneyStates.QualificationPending,
                StageChanged = true
            });

        var sut = CreateSut(kanbanService.Object);

        var result = await sut.RunOnceAsync(nowUtc);

        Assert.Equal(1, result.ScannedCount);
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.TimerEscalationCount);
        kanbanService.VerifyAll();
    }

    [Fact(DisplayName = "Journey Stage Automation | Deve avancar janela sugerida para aguardando confirmacao da agenda")]
    public async Task RunOnceAsync_DeveAvancarJanelaSugeridaParaAguardarConfirmacao()
    {
        var kanbanService = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        var nowUtc = new DateTime(2026, 3, 18, 15, 0, 0, DateTimeKind.Utc);
        var suggestedAtUtc = new DateTime(2026, 3, 18, 14, 55, 0, DateTimeKind.Utc);

        kanbanService
            .Setup(service => service.ListJourneyStageAutomationCandidates(
                AdminKanbanBoardTypes.Clients,
                nowUtc,
                50))
            .Returns(
            [
                new AdminKanbanJourneyStageAutomationCandidateRecord
                {
                    LeadId = 102,
                    JourneyId = 702,
                    BoardType = AdminKanbanBoardTypes.Clients,
                    StageId = 12,
                    StageName = AdminKanbanJourneyClientStageNames.SlotSuggested,
                    CurrentState = AdminKanbanJourneyStates.SlotSuggested,
                    QualificationStatus = AdminKanbanJourneyQualificationStatuses.Qualified,
                    SchedulingStatus = AdminKanbanJourneySchedulingStatuses.SlotSuggested,
                    CreatedAtUtc = suggestedAtUtc.AddHours(-1),
                    LastIntakeAtUtc = suggestedAtUtc,
                    CurrentStateEnteredAtUtc = suggestedAtUtc,
                    SchedulingSuggestedAtUtc = suggestedAtUtc,
                    LastAutomationReason = "Slots sugeridos ao cliente; card movido para Janela sugerida.",
                    LastAutomationOrigin = AdminKanbanJourneyAutomationOrigins.StateMachine
                }
            ]);
        kanbanService
            .Setup(service => service.ApplyJourneyStageAutomation(
                It.Is<AdminKanbanJourneyStageAutomationUpdateRequest>(request =>
                    request.LeadId == 102 &&
                    request.TargetStageName == AdminKanbanJourneyClientStageNames.WaitingScheduleConfirmation &&
                    request.TargetCurrentState == AdminKanbanJourneyStates.WaitingScheduleConfirmation &&
                    request.ActiveTimerCode == AdminKanbanJourneyTimerCodes.PendingScheduleConfirmation &&
                    request.ActiveTimerDueAtUtc == suggestedAtUtc.AddMinutes(180))))
            .Returns(new AdminKanbanJourneyStageAutomationUpdateResult
            {
                LeadId = 102,
                JourneyId = 702,
                FromStageId = 12,
                FromStageName = AdminKanbanJourneyClientStageNames.SlotSuggested,
                ToStageId = 13,
                ToStageName = AdminKanbanJourneyClientStageNames.WaitingScheduleConfirmation,
                CurrentState = AdminKanbanJourneyStates.WaitingScheduleConfirmation,
                StageChanged = true
            });

        var sut = CreateSut(kanbanService.Object);

        var result = await sut.RunOnceAsync(nowUtc);

        Assert.Equal(1, result.ScannedCount);
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.TimerEscalationCount);
        kanbanService.VerifyAll();
    }

    [Fact(DisplayName = "Journey Stage Automation | Deve escalonar agenda pendente vencida para excecao operacional")]
    public async Task RunOnceAsync_DeveEscalonarAgendaPendenteVencida()
    {
        var kanbanService = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        var nowUtc = new DateTime(2026, 3, 18, 19, 0, 0, DateTimeKind.Utc);
        var dueAtUtc = new DateTime(2026, 3, 18, 18, 0, 0, DateTimeKind.Utc);

        kanbanService
            .Setup(service => service.ListJourneyStageAutomationCandidates(
                AdminKanbanBoardTypes.Clients,
                nowUtc,
                50))
            .Returns(
            [
                new AdminKanbanJourneyStageAutomationCandidateRecord
                {
                    LeadId = 103,
                    JourneyId = 703,
                    BoardType = AdminKanbanBoardTypes.Clients,
                    StageId = 13,
                    StageName = AdminKanbanJourneyClientStageNames.WaitingScheduleConfirmation,
                    CurrentState = AdminKanbanJourneyStates.WaitingScheduleConfirmation,
                    QualificationStatus = AdminKanbanJourneyQualificationStatuses.Qualified,
                    SchedulingStatus = AdminKanbanJourneySchedulingStatuses.SlotSuggested,
                    CreatedAtUtc = dueAtUtc.AddHours(-2),
                    LastIntakeAtUtc = dueAtUtc.AddHours(-1),
                    CurrentStateEnteredAtUtc = dueAtUtc.AddHours(-1),
                    ActiveTimerCode = AdminKanbanJourneyTimerCodes.PendingScheduleConfirmation,
                    ActiveTimerDueAtUtc = dueAtUtc,
                    LastAutomationReason = "Aguardando o cliente confirmar uma das janelas sugeridas.",
                    LastAutomationOrigin = AdminKanbanJourneyAutomationOrigins.StateMachine
                }
            ]);
        kanbanService
            .Setup(service => service.ApplyJourneyStageAutomation(
                It.Is<AdminKanbanJourneyStageAutomationUpdateRequest>(request =>
                    request.LeadId == 103 &&
                    request.TargetStageName == AdminKanbanJourneyClientStageNames.OperationalException &&
                    request.TargetCurrentState == AdminKanbanJourneyStates.OperationalException &&
                    request.Origin == AdminKanbanJourneyAutomationOrigins.Timer &&
                    request.ActiveTimerCode == string.Empty &&
                    request.ActiveTimerDueAtUtc == null &&
                    request.HistoryEventType == "jornada_timer_agenda_pendente_vencido")))
            .Returns(new AdminKanbanJourneyStageAutomationUpdateResult
            {
                LeadId = 103,
                JourneyId = 703,
                FromStageId = 13,
                FromStageName = AdminKanbanJourneyClientStageNames.WaitingScheduleConfirmation,
                ToStageId = 19,
                ToStageName = AdminKanbanJourneyClientStageNames.OperationalException,
                CurrentState = AdminKanbanJourneyStates.OperationalException,
                StageChanged = true
            });

        var sut = CreateSut(kanbanService.Object);

        var result = await sut.RunOnceAsync(nowUtc);

        Assert.Equal(1, result.ScannedCount);
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(1, result.TimerEscalationCount);
        kanbanService.VerifyAll();
    }

    private static JourneyStageAutomationService CreateSut(IAdminKanbanService kanbanService)
    {
        return new JourneyStageAutomationService(
            kanbanService,
            Options.Create(new JourneyStageAutomationOptions
            {
                Enabled = true,
                WorkerEnabled = true,
                WorkerIntervalSeconds = 20,
                WorkerBatchSize = 50,
                PendingDataTimeoutMinutes = 120,
                ScheduleConfirmationTimeoutMinutes = 180,
                ProviderAcceptanceTimeoutMinutes = 45,
                ClientReviewTimeoutHours = 72,
                ProviderReviewTimeoutHours = 72
            }),
            NullLogger<JourneyStageAutomationService>.Instance);
    }
}
