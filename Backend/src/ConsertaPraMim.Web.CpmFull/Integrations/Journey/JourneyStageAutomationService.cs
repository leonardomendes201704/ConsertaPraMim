using System.Text.Json;
using AppMobileCPM.Services;
using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyStageAutomationService : IJourneyStageAutomationService
{
    private readonly IAdminKanbanService _kanbanService;
    private readonly IJourneyGovernanceService _journeyGovernanceService;
    private readonly JourneyStageAutomationOptions _options;
    private readonly ILogger<JourneyStageAutomationService> _logger;

    public JourneyStageAutomationService(
        IAdminKanbanService kanbanService,
        IJourneyGovernanceService journeyGovernanceService,
        IOptions<JourneyStageAutomationOptions> options,
        ILogger<JourneyStageAutomationService> logger)
    {
        _kanbanService = kanbanService;
        _journeyGovernanceService = journeyGovernanceService;
        _options = options.Value;
        _logger = logger;
    }

    public Task<JourneyStageAutomationRunResult> RunOnceAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return Task.FromResult(new JourneyStageAutomationRunResult());
        }

        var governanceDecision = _journeyGovernanceService.EvaluateStep(
            JourneyGovernanceSteps.StageAutomation,
            AdminKanbanJourneySourceChannels.Landing);
        if (!governanceDecision.Allowed)
        {
            _logger.LogInformation("JourneyStageAutomationService ignorado pela governanca. Motivo={Reason}.", governanceDecision.Reason);
            return Task.FromResult(new JourneyStageAutomationRunResult());
        }

        var effectiveNowUtc = NormalizeUtc(nowUtc) ?? DateTime.UtcNow;
        var candidates = _kanbanService.ListJourneyStageAutomationCandidates(
            AdminKanbanBoardTypes.Clients,
            effectiveNowUtc,
            _options.WorkerBatchSize);

        var updatedCount = 0;
        var timerEscalationCount = 0;

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var updateRequest = BuildUpdateRequest(candidate, effectiveNowUtc);
            if (updateRequest is null)
            {
                continue;
            }

            var result = _kanbanService.ApplyJourneyStageAutomation(updateRequest);
            if (result is null)
            {
                continue;
            }

            updatedCount++;
            if (string.Equals(updateRequest.Origin, AdminKanbanJourneyAutomationOrigins.Timer, StringComparison.OrdinalIgnoreCase))
            {
                timerEscalationCount++;
            }
        }

        if (updatedCount > 0)
        {
            _logger.LogInformation(
                "JourneyStageAutomationService processou {ScannedCount} candidatos e aplicou {UpdatedCount} transicoes. TimerEscalations={TimerEscalationCount}.",
                candidates.Count,
                updatedCount,
                timerEscalationCount);
        }

        return Task.FromResult(new JourneyStageAutomationRunResult
        {
            ScannedCount = candidates.Count,
            UpdatedCount = updatedCount,
            TimerEscalationCount = timerEscalationCount
        });
    }

    private AdminKanbanJourneyStageAutomationUpdateRequest? BuildUpdateRequest(
        AdminKanbanJourneyStageAutomationCandidateRecord candidate,
        DateTime nowUtc)
    {
        if (!string.Equals(candidate.BoardType, AdminKanbanBoardTypes.Clients, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var timerEscalation = BuildTimerEscalation(candidate, nowUtc);
        if (timerEscalation is not null)
        {
            return timerEscalation;
        }

        var normalizedState = AdminKanbanJourneyStates.Normalize(candidate.CurrentState);
        var stateEnteredAtUtc = candidate.CurrentStateEnteredAtUtc ?? candidate.LastIntakeAtUtc ?? candidate.CreatedAtUtc;

        return normalizedState switch
        {
            AdminKanbanJourneyStates.IntakeOpened or AdminKanbanJourneyStates.ServiceRequestOpened
                => BuildStateMachineRequest(candidate, AdminKanbanJourneyClientStageNames.AutomatedTriage, normalizedState, "Intake automatizado recebido; card movido para triagem automatica."),
            AdminKanbanJourneyStates.AutomatedTriage
                => BuildStateMachineRequest(candidate, AdminKanbanJourneyClientStageNames.AutomatedTriage, normalizedState, "Jornada em triagem automatica."),
            AdminKanbanJourneyStates.QualificationPending or AdminKanbanJourneyStates.QualificationConfirmationRequired
                => BuildStateMachineRequest(
                    candidate,
                    AdminKanbanJourneyClientStageNames.PendingData,
                    normalizedState,
                    normalizedState == AdminKanbanJourneyStates.QualificationConfirmationRequired
                        ? "A jornada requer confirmacao de dados do cliente."
                        : "Ainda faltam dados obrigatorios para continuar a jornada.",
                    AdminKanbanJourneyTimerCodes.PendingData,
                    stateEnteredAtUtc.AddMinutes(_options.PendingDataTimeoutMinutes)),
            AdminKanbanJourneyStates.QualificationValidated
                => BuildStateMachineRequest(candidate, AdminKanbanJourneyClientStageNames.ValidatedAddressAndCategory, normalizedState, "Endereco e categoria validados para seguir com o agendamento."),
            AdminKanbanJourneyStates.SlotSuggested
                => BuildSlotSuggestedRequest(candidate, nowUtc),
            AdminKanbanJourneyStates.WaitingScheduleConfirmation
                => BuildStateMachineRequest(
                    candidate,
                    AdminKanbanJourneyClientStageNames.WaitingScheduleConfirmation,
                    normalizedState,
                    "Aguardando o cliente confirmar uma das janelas sugeridas.",
                    AdminKanbanJourneyTimerCodes.PendingScheduleConfirmation,
                    (candidate.SchedulingSuggestedAtUtc ?? stateEnteredAtUtc).AddMinutes(_options.ScheduleConfirmationTimeoutMinutes)),
            AdminKanbanJourneyStates.AppointmentConfirmed
                => BuildStateMachineRequest(candidate, AdminKanbanJourneyClientStageNames.AppointmentConfirmed, normalizedState, "Agendamento confirmado e pronto para as proximas etapas da jornada."),
            AdminKanbanJourneyStates.AppointmentCancelled or AdminKanbanJourneyStates.Cancelled
                => BuildStateMachineRequest(candidate, AdminKanbanJourneyClientStageNames.Cancelled, AdminKanbanJourneyStates.Cancelled, "Agendamento cancelado na jornada automatizada."),
            AdminKanbanJourneyStates.MatchingInProgress
                => BuildStateMachineRequest(candidate, AdminKanbanJourneyClientStageNames.MatchingInProgress, normalizedState, "Calculando lista de prestadores elegiveis."),
            AdminKanbanJourneyStates.DispatchInProgress
                => BuildStateMachineRequest(candidate, AdminKanbanJourneyClientStageNames.DispatchInProgress, normalizedState, "Disparo em ondas para prestadores iniciado."),
            AdminKanbanJourneyStates.WaitingProviderAcceptance
                => IsDispatchManagedAcceptance(candidate)
                    ? BuildStateMachineRequest(
                        candidate,
                        AdminKanbanJourneyClientStageNames.WaitingAcceptance,
                        normalizedState,
                        "Aguardando aceite de prestador elegivel.")
                    : BuildStateMachineRequest(
                        candidate,
                        AdminKanbanJourneyClientStageNames.WaitingAcceptance,
                        normalizedState,
                        "Aguardando aceite de prestador elegivel.",
                        AdminKanbanJourneyTimerCodes.PendingAcceptance,
                        stateEnteredAtUtc.AddMinutes(_options.ProviderAcceptanceTimeoutMinutes)),
            AdminKanbanJourneyStates.ProviderConnected
                => BuildStateMachineRequest(candidate, AdminKanbanJourneyClientStageNames.ProviderConnected, normalizedState, "Prestador conectado ao cliente."),
            AdminKanbanJourneyStates.ServiceInProgress
                => BuildStateMachineRequest(candidate, AdminKanbanJourneyClientStageNames.ServiceInProgress, normalizedState, "Servico em andamento com prestador conectado."),
            AdminKanbanJourneyStates.WaitingCompletionConfirmation
                => BuildStateMachineRequest(candidate, AdminKanbanJourneyClientStageNames.WaitingCompletionConfirmation, normalizedState, "Aguardando confirmacao de conclusao do atendimento."),
            AdminKanbanJourneyStates.WaitingClientReview
                => BuildStateMachineRequest(
                    candidate,
                    AdminKanbanJourneyClientStageNames.WaitingClientReview,
                    normalizedState,
                    "Aguardando avaliacao do cliente apos a conclusao.",
                    AdminKanbanJourneyTimerCodes.PendingClientReview,
                    stateEnteredAtUtc.AddHours(_options.ClientReviewTimeoutHours)),
            AdminKanbanJourneyStates.WaitingProviderReview
                => BuildStateMachineRequest(
                    candidate,
                    AdminKanbanJourneyClientStageNames.WaitingProviderReview,
                    normalizedState,
                    "Aguardando avaliacao do prestador apos a conclusao.",
                    AdminKanbanJourneyTimerCodes.PendingProviderReview,
                    stateEnteredAtUtc.AddHours(_options.ProviderReviewTimeoutHours)),
            AdminKanbanJourneyStates.Completed
                => BuildStateMachineRequest(candidate, AdminKanbanJourneyClientStageNames.Completed, normalizedState, "Jornada concluida automaticamente."),
            AdminKanbanJourneyStates.NoMatch
                => BuildStateMachineRequest(candidate, AdminKanbanJourneyClientStageNames.NoMatch, normalizedState, "Nenhum prestador aceitou a oportunidade dentro das ondas configuradas."),
            AdminKanbanJourneyStates.OperationalException
                => BuildStateMachineRequest(candidate, AdminKanbanJourneyClientStageNames.OperationalException, normalizedState, "Jornada encaminhada para excecao operacional."),
            _ => null
        };
    }

    private AdminKanbanJourneyStageAutomationUpdateRequest? BuildSlotSuggestedRequest(
        AdminKanbanJourneyStageAutomationCandidateRecord candidate,
        DateTime nowUtc)
    {
        if (!string.Equals(candidate.StageName, AdminKanbanJourneyClientStageNames.SlotSuggested, StringComparison.Ordinal))
        {
            return BuildStateMachineRequest(
                candidate,
                AdminKanbanJourneyClientStageNames.SlotSuggested,
                AdminKanbanJourneyStates.SlotSuggested,
                "Slots sugeridos ao cliente; card movido para Janela sugerida.");
        }

        var stateEnteredAtUtc = candidate.SchedulingSuggestedAtUtc ?? candidate.CurrentStateEnteredAtUtc ?? candidate.LastIntakeAtUtc ?? candidate.CreatedAtUtc;
        return BuildStateMachineRequest(
            candidate,
            AdminKanbanJourneyClientStageNames.WaitingScheduleConfirmation,
            AdminKanbanJourneyStates.WaitingScheduleConfirmation,
            "Cliente recebeu as janelas e a jornada agora aguarda confirmacao da agenda.",
            AdminKanbanJourneyTimerCodes.PendingScheduleConfirmation,
            stateEnteredAtUtc.AddMinutes(_options.ScheduleConfirmationTimeoutMinutes));
    }

    private AdminKanbanJourneyStageAutomationUpdateRequest? BuildTimerEscalation(
        AdminKanbanJourneyStageAutomationCandidateRecord candidate,
        DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(candidate.ActiveTimerCode) ||
            !candidate.ActiveTimerDueAtUtc.HasValue ||
            candidate.ActiveTimerDueAtUtc.Value > nowUtc)
        {
            return null;
        }

        return candidate.ActiveTimerCode switch
        {
            AdminKanbanJourneyTimerCodes.PendingData
                => BuildOperationalExceptionTimerRequest(
                    candidate,
                    _journeyGovernanceService.ResolveOperationalException(
                        JourneyGovernanceReasonCodes.PendingDataTimeout,
                        "Prazo de dados pendentes expirou sem resposta suficiente do cliente."),
                    clearTimer: true),
            AdminKanbanJourneyTimerCodes.PendingScheduleConfirmation
                => BuildOperationalExceptionTimerRequest(
                    candidate,
                    _journeyGovernanceService.ResolveOperationalException(
                        JourneyGovernanceReasonCodes.ScheduleConfirmationTimeout,
                        "Prazo de confirmacao da agenda expirou sem resposta do cliente."),
                    clearTimer: true),
            AdminKanbanJourneyTimerCodes.PendingAcceptance
                => IsDispatchManagedAcceptance(candidate)
                    ? null
                    : BuildTimerRequest(
                        candidate,
                        AdminKanbanJourneyClientStageNames.NoMatch,
                        AdminKanbanJourneyStates.NoMatch,
                        "Prazo de aceite do prestador expirou sem confirmacao valida.",
                        "jornada_timer_aceite_pendente_vencido",
                        clearTimer: true),
            AdminKanbanJourneyTimerCodes.PendingClientReview
                => BuildTimerRequest(
                    candidate,
                    AdminKanbanJourneyClientStageNames.WaitingProviderReview,
                    AdminKanbanJourneyStates.WaitingProviderReview,
                    "Prazo da avaliacao do cliente expirou; a jornada avancou para a avaliacao do prestador.",
                    "jornada_timer_avaliacao_cliente_vencido",
                    clearTimer: false,
                    nextTimerCode: AdminKanbanJourneyTimerCodes.PendingProviderReview,
                    nextTimerDueAtUtc: nowUtc.AddHours(_options.ProviderReviewTimeoutHours)),
            AdminKanbanJourneyTimerCodes.PendingProviderReview
                => BuildTimerRequest(
                    candidate,
                    AdminKanbanJourneyClientStageNames.Completed,
                    AdminKanbanJourneyStates.Completed,
                    "Prazo da avaliacao do prestador expirou; a jornada foi concluida automaticamente.",
                    "jornada_timer_avaliacao_prestador_vencido",
                    clearTimer: true),
            _ => null
        };
    }

    private static bool IsDispatchManagedAcceptance(AdminKanbanJourneyStageAutomationCandidateRecord candidate)
    {
        if (!string.Equals(candidate.CurrentState, AdminKanbanJourneyStates.WaitingProviderAcceptance, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(candidate.DispatchStatus, AdminKanbanJourneyDispatchStatuses.WaveQueued, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(candidate.DispatchStatus, AdminKanbanJourneyDispatchStatuses.WaitingAcceptance, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(candidate.DispatchStatus, AdminKanbanJourneyDispatchStatuses.Reserved, StringComparison.OrdinalIgnoreCase);
    }

    private static AdminKanbanJourneyStageAutomationUpdateRequest? BuildStateMachineRequest(
        AdminKanbanJourneyStageAutomationCandidateRecord candidate,
        string targetStageName,
        string targetState,
        string reason,
        string? timerCode = null,
        DateTime? timerDueAtUtc = null)
    {
        return BuildRequest(
            candidate,
            targetStageName,
            targetState,
            reason,
            AdminKanbanJourneyAutomationOrigins.StateMachine,
            "jornada_kanban_automatizada",
            timerCode,
            timerDueAtUtc);
    }

    private static AdminKanbanJourneyStageAutomationUpdateRequest? BuildTimerRequest(
        AdminKanbanJourneyStageAutomationCandidateRecord candidate,
        string targetStageName,
        string targetState,
        string reason,
        string historyEventType,
        bool clearTimer,
        string? nextTimerCode = null,
        DateTime? nextTimerDueAtUtc = null)
    {
        return BuildRequest(
            candidate,
            targetStageName,
            targetState,
            reason,
            AdminKanbanJourneyAutomationOrigins.Timer,
            historyEventType,
            clearTimer ? nextTimerCode : nextTimerCode ?? candidate.ActiveTimerCode,
            clearTimer ? nextTimerDueAtUtc : nextTimerDueAtUtc ?? candidate.ActiveTimerDueAtUtc);
    }

    private static AdminKanbanJourneyStageAutomationUpdateRequest? BuildOperationalExceptionTimerRequest(
        AdminKanbanJourneyStageAutomationCandidateRecord candidate,
        JourneyOperationalExceptionPolicy exceptionPolicy,
        bool clearTimer)
    {
        return BuildTimerRequest(
            candidate,
            exceptionPolicy.TargetStageName,
            exceptionPolicy.TargetState,
            exceptionPolicy.Summary,
            exceptionPolicy.HistoryEventType,
            clearTimer: clearTimer);
    }

    private static AdminKanbanJourneyStageAutomationUpdateRequest? BuildRequest(
        AdminKanbanJourneyStageAutomationCandidateRecord candidate,
        string targetStageName,
        string targetState,
        string reason,
        string origin,
        string historyEventType,
        string? timerCode,
        DateTime? timerDueAtUtc)
    {
        var normalizedTargetState = AdminKanbanJourneyStates.Normalize(targetState);
        var normalizedOrigin = AdminKanbanJourneyAutomationOrigins.Normalize(origin);
        var normalizedTimerCode = NormalizeTimerCode(timerCode);
        var normalizedTimerDueAtUtc = NormalizeUtc(timerDueAtUtc);

        if (string.Equals(candidate.StageName, targetStageName, StringComparison.Ordinal) &&
            string.Equals(candidate.CurrentState, normalizedTargetState, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidate.ActiveTimerCode, normalizedTimerCode, StringComparison.OrdinalIgnoreCase) &&
            candidate.ActiveTimerDueAtUtc == normalizedTimerDueAtUtc &&
            string.Equals(candidate.LastAutomationReason, reason, StringComparison.Ordinal) &&
            string.Equals(candidate.LastAutomationOrigin, normalizedOrigin, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var description = $"Kanban movido automaticamente para {targetStageName}. Motivo: {reason}";
        return new AdminKanbanJourneyStageAutomationUpdateRequest
        {
            LeadId = candidate.LeadId,
            BoardType = candidate.BoardType,
            TargetStageName = targetStageName,
            TargetCurrentState = normalizedTargetState,
            Reason = reason,
            Origin = normalizedOrigin,
            HistoryEventType = historyEventType,
            HistoryDescription = description,
            MetadataJson = JsonSerializer.Serialize(new
            {
                previousStageName = candidate.StageName,
                previousState = candidate.CurrentState,
                targetStageName,
                targetState = normalizedTargetState,
                reason,
                origin = normalizedOrigin,
                timerCode = normalizedTimerCode,
                timerDueAtUtc = normalizedTimerDueAtUtc
            }),
            ActiveTimerCode = normalizedTimerCode,
            ActiveTimerDueAtUtc = normalizedTimerDueAtUtc
        };
    }

    private static DateTime? NormalizeUtc(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }

    private static string NormalizeTimerCode(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        AdminKanbanJourneyTimerCodes.PendingData => AdminKanbanJourneyTimerCodes.PendingData,
        AdminKanbanJourneyTimerCodes.PendingScheduleConfirmation => AdminKanbanJourneyTimerCodes.PendingScheduleConfirmation,
        AdminKanbanJourneyTimerCodes.PendingAcceptance => AdminKanbanJourneyTimerCodes.PendingAcceptance,
        AdminKanbanJourneyTimerCodes.PendingClientReview => AdminKanbanJourneyTimerCodes.PendingClientReview,
        AdminKanbanJourneyTimerCodes.PendingProviderReview => AdminKanbanJourneyTimerCodes.PendingProviderReview,
        _ => string.Empty
    };
}
