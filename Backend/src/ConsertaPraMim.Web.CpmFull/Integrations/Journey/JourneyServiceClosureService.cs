using System.Net;
using System.Net.Mail;
using System.Text.Encodings.Web;
using System.Text.Json;
using AppMobileCPM.Integrations.Telegram;
using AppMobileCPM.Services;
using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyServiceClosureService : IJourneyServiceClosureService
{
    private readonly IAdminKanbanService _kanbanService;
    private readonly IJourneyServiceClosureLinkService _linkService;
    private readonly ITelegramBridgeDeliveryClient _telegramBridgeDeliveryClient;
    private readonly JourneyProviderNotificationOptions _notificationOptions;
    private readonly JourneyServiceClosureOptions _options;
    private readonly ILogger<JourneyServiceClosureService> _logger;

    public JourneyServiceClosureService(
        IAdminKanbanService kanbanService,
        IJourneyServiceClosureLinkService linkService,
        ITelegramBridgeDeliveryClient telegramBridgeDeliveryClient,
        IOptions<JourneyProviderNotificationOptions> notificationOptions,
        IOptions<JourneyServiceClosureOptions> options,
        ILogger<JourneyServiceClosureService> logger)
    {
        _kanbanService = kanbanService;
        _linkService = linkService;
        _telegramBridgeDeliveryClient = telegramBridgeDeliveryClient;
        _notificationOptions = notificationOptions.Value;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<JourneyServiceClosureStartResult> StartServiceAsync(int leadId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return new JourneyServiceClosureStartResult { Success = true, Message = "Encerramento automatico desabilitado no ambiente atual." };
        }

        var lead = _kanbanService.GetLeadDetails(leadId);
        if (lead is null)
        {
            return new JourneyServiceClosureStartResult { Message = "Lead nao encontrado para iniciar o encerramento da jornada." };
        }

        _ = _kanbanService.UpdateJourneyClosure(leadId, new AdminKanbanJourneyClosureUpdateRequest
        {
            Status = AdminKanbanJourneyClosureStatuses.ServiceInProgress,
            Summary = "Prestador conectado e atendimento liberado para execucao.",
            Outcome = string.Empty,
            ServiceInProgressAtUtc = nowUtc,
            ClientReviewStatus = AdminKanbanJourneyReviewStatuses.Pending,
            ProviderReviewStatus = AdminKanbanJourneyReviewStatuses.Pending,
            CurrentState = AdminKanbanJourneyStates.ServiceInProgress,
            HistoryEventType = "jornada_servico_iniciado",
            HistoryDescription = "O caso foi reservado e a jornada entrou em servico em andamento.",
            SourceChannel = lead.Journey.SourceChannel,
            MetadataJson = BuildMetadataJson(lead, "service_started")
        });

        _ = _kanbanService.ApplyJourneyStageAutomation(new AdminKanbanJourneyStageAutomationUpdateRequest
        {
            LeadId = leadId,
            BoardType = lead.BoardType,
            TargetStageName = AdminKanbanJourneyClientStageNames.ServiceInProgress,
            TargetCurrentState = AdminKanbanJourneyStates.ServiceInProgress,
            Reason = "Prestador conectado e atendimento liberado para execucao.",
            Origin = AdminKanbanJourneyAutomationOrigins.StateMachine,
            HistoryEventType = "jornada_servico_iniciado",
            HistoryDescription = "O card avancou automaticamente para servico em andamento.",
            MetadataJson = BuildMetadataJson(lead, "service_started"),
            ActiveTimerCode = string.Empty,
            ActiveTimerDueAtUtc = null
        });

        var providerNotification = await NotifyProviderCompletionRequestAsync(lead, nowUtc, cancellationToken);
        return new JourneyServiceClosureStartResult
        {
            Success = providerNotification.Success,
            Message = providerNotification.Message
        };
    }

    public JourneyServiceClosureCompletionContext GetProviderCompletionContext(string token, DateTime nowUtc)
    {
        var validation = _linkService.ValidateToken(token, JourneyServiceClosureTokenPurposes.ProviderCompletion, JourneyServiceClosureAudiences.Provider, nowUtc);
        if (!validation.Success)
        {
            return BuildInvalidCompletionContext(validation);
        }

        var lead = _kanbanService.GetLeadDetails(validation.Payload!.LeadId);
        if (lead is null || !IsReservedProvider(lead, validation.Payload.ProviderId))
        {
            return new JourneyServiceClosureCompletionContext
            {
                NotFound = true,
                Message = "A jornada nao foi localizada para este prestador.",
                ResponseHeadline = "Jornada indisponivel",
                ResponseDescription = "Nao foi possivel localizar um caso reservado para este link."
            };
        }

        return BuildCompletionContext(lead, token, "Informe o desfecho do atendimento", "Registre abaixo se o servico foi concluido, se houve no-show do cliente ou cancelamento tardio.");
    }

    public JourneyServiceClosureCompletionContext GetClientCompletionContext(string token, string action, DateTime nowUtc)
    {
        var validation = _linkService.ValidateToken(token, JourneyServiceClosureTokenPurposes.ClientCompletionDecision, JourneyServiceClosureAudiences.Client, nowUtc);
        if (!validation.Success)
        {
            return BuildInvalidCompletionContext(validation);
        }

        var lead = _kanbanService.GetLeadDetails(validation.Payload!.LeadId);
        if (lead is null)
        {
            return new JourneyServiceClosureCompletionContext
            {
                NotFound = true,
                Message = "A jornada nao foi localizada para este cliente.",
                ResponseHeadline = "Jornada indisponivel",
                ResponseDescription = "Nao foi possivel localizar o atendimento deste link."
            };
        }

        var normalizedAction = JourneyServiceClosureReviewActions.Normalize(action);
        return BuildCompletionContext(
            lead,
            token,
            normalizedAction == JourneyServiceClosureReviewActions.Contest ? "Contestar conclusao" : "Confirmar conclusao",
            normalizedAction == JourneyServiceClosureReviewActions.Contest
                ? "Se houve problema na conclusao, registre a contestacao."
                : "Confirme abaixo se o atendimento foi concluido corretamente.");
    }

    public JourneyServiceClosureReviewContext GetReviewContext(string token, string audience, DateTime nowUtc)
    {
        var normalizedAudience = JourneyServiceClosureAudiences.Normalize(audience);
        var purpose = normalizedAudience == JourneyServiceClosureAudiences.Provider
            ? JourneyServiceClosureTokenPurposes.ProviderReview
            : JourneyServiceClosureTokenPurposes.ClientReview;
        var validation = _linkService.ValidateToken(token, purpose, normalizedAudience, nowUtc);
        if (!validation.Success)
        {
            return BuildInvalidReviewContext(validation, normalizedAudience);
        }

        var lead = _kanbanService.GetLeadDetails(validation.Payload!.LeadId);
        if (lead is null)
        {
            return new JourneyServiceClosureReviewContext
            {
                NotFound = true,
                Audience = normalizedAudience,
                ResponseHeadline = "Avaliacao indisponivel",
                ResponseDescription = "Nao foi possivel localizar o atendimento desta avaliacao."
            };
        }

        var alreadyResponded = normalizedAudience == JourneyServiceClosureAudiences.Client
            ? string.Equals(lead.Journey.Closure.ClientReviewStatus, AdminKanbanJourneyReviewStatuses.Submitted, StringComparison.OrdinalIgnoreCase)
            : string.Equals(lead.Journey.Closure.ProviderReviewStatus, AdminKanbanJourneyReviewStatuses.Submitted, StringComparison.OrdinalIgnoreCase);

        return new JourneyServiceClosureReviewContext
        {
            Success = true,
            Audience = normalizedAudience,
            LeadName = lead.Name,
            CounterpartyName = normalizedAudience == JourneyServiceClosureAudiences.Client
                ? lead.Journey.Dispatch.ReservedProviderName
                : lead.Name,
            RequestedCategory = ResolveCategory(lead),
            ScheduledWindowLabel = BuildSchedulingWindowLabel(lead),
            AddressSummary = BuildAddressSummary(lead),
            Token = token,
            CanRespond = !alreadyResponded,
            AlreadyResponded = alreadyResponded,
            ResponseHeadline = alreadyResponded ? "Avaliacao ja enviada" : "Registrar avaliacao",
            ResponseDescription = alreadyResponded
                ? "Esta avaliacao ja foi registrada anteriormente."
                : "Preencha sua avaliacao para encerrar a etapa final desta jornada."
        };
    }

    public Task<JourneyServiceClosureCompletionActionResult> SubmitProviderOutcomeAsync(string token, string outcome, string notes, DateTime nowUtc, CancellationToken cancellationToken = default)
        => SubmitProviderOutcomeCoreAsync(token, outcome, notes, nowUtc, cancellationToken);

    public Task<JourneyServiceClosureCompletionActionResult> SubmitClientDecisionAsync(string token, string action, string reason, DateTime nowUtc, CancellationToken cancellationToken = default)
        => SubmitClientDecisionCoreAsync(token, action, reason, nowUtc, cancellationToken);

    public Task<JourneyServiceClosureReviewActionResult> SubmitReviewAsync(string token, string audience, JourneyServiceClosureReviewSubmissionRequest request, DateTime nowUtc, CancellationToken cancellationToken = default)
        => SubmitReviewCoreAsync(token, audience, request, nowUtc, cancellationToken);

    private async Task<JourneyServiceClosureCompletionActionResult> SubmitProviderOutcomeCoreAsync(string token, string outcome, string notes, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var validation = _linkService.ValidateToken(token, JourneyServiceClosureTokenPurposes.ProviderCompletion, JourneyServiceClosureAudiences.Provider, nowUtc);
        if (!validation.Success)
        {
            return new JourneyServiceClosureCompletionActionResult
            {
                TokenExpired = validation.Expired,
                Message = validation.Message,
                Context = BuildInvalidCompletionContext(validation)
            };
        }

        var lead = _kanbanService.GetLeadDetails(validation.Payload!.LeadId);
        if (lead is null || !IsReservedProvider(lead, validation.Payload.ProviderId))
        {
            return new JourneyServiceClosureCompletionActionResult
            {
                Message = "A jornada nao foi localizada para este prestador.",
                Context = new JourneyServiceClosureCompletionContext
                {
                    NotFound = true,
                    Message = "A jornada nao foi localizada para este prestador.",
                    ResponseHeadline = "Jornada indisponivel",
                    ResponseDescription = "Nao foi possivel localizar um caso reservado para este link."
                }
            };
        }

        var normalizedOutcome = JourneyServiceClosureProviderOutcomes.Normalize(outcome);
        if (!string.Equals(normalizedOutcome, JourneyServiceClosureProviderOutcomes.Completed, StringComparison.Ordinal))
        {
            var status = string.Equals(normalizedOutcome, JourneyServiceClosureProviderOutcomes.ClientNoShow, StringComparison.Ordinal)
                ? AdminKanbanJourneyClosureStatuses.ClientNoShow
                : AdminKanbanJourneyClosureStatuses.LateCancellation;
            var label = JourneyServiceClosureProviderOutcomes.GetLabel(normalizedOutcome);
            var description = $"O prestador registrou o desfecho '{label}' para o atendimento.";

            _ = _kanbanService.UpdateJourneyClosure(lead.Id, BuildClosureUpdateFromLead(
                lead,
                status,
                description,
                AdminKanbanJourneyStates.OperationalException,
                "jornada_conclusao_excecao",
                description,
                nowUtc,
                outcome: normalizedOutcome,
                providerCompletionSubmittedAtUtc: nowUtc,
                metadataJson: JsonSerializer.Serialize(new { outcome = normalizedOutcome, notes = notes.Trim() })));

            _ = _kanbanService.ApplyJourneyStageAutomation(new AdminKanbanJourneyStageAutomationUpdateRequest
            {
                LeadId = lead.Id,
                BoardType = lead.BoardType,
                TargetStageName = AdminKanbanJourneyClientStageNames.OperationalException,
                TargetCurrentState = AdminKanbanJourneyStates.OperationalException,
                Reason = description,
                Origin = AdminKanbanJourneyAutomationOrigins.StateMachine,
                HistoryEventType = "jornada_conclusao_excecao",
                HistoryDescription = description,
                MetadataJson = JsonSerializer.Serialize(new { outcome = normalizedOutcome, notes = notes.Trim() }),
                ActiveTimerCode = string.Empty,
                ActiveTimerDueAtUtc = null
            });

            return new JourneyServiceClosureCompletionActionResult
            {
                Success = true,
                Message = $"{label} registrado com sucesso. A jornada foi encaminhada para excecao operacional.",
                Context = BuildCompletionContext(lead, token, "Desfecho registrado", "O caso foi encaminhado para tratamento operacional.")
            };
        }

        _ = _kanbanService.UpdateJourneyClosure(lead.Id, BuildClosureUpdateFromLead(
            lead,
            AdminKanbanJourneyClosureStatuses.WaitingClientConfirmation,
            "Prestador informou a conclusao e a jornada agora aguarda confirmacao do cliente.",
            AdminKanbanJourneyStates.WaitingCompletionConfirmation,
            "jornada_conclusao_solicitada",
            "O prestador informou a conclusao do servico e o cliente recebeu a solicitacao de confirmacao.",
            nowUtc,
            outcome: AdminKanbanJourneyCompletionOutcomes.Completed,
            providerCompletionSubmittedAtUtc: nowUtc,
            clientConfirmationRequestedAtUtc: nowUtc,
            metadataJson: JsonSerializer.Serialize(new { notes = notes.Trim() })));

        _ = _kanbanService.ApplyJourneyStageAutomation(new AdminKanbanJourneyStageAutomationUpdateRequest
        {
            LeadId = lead.Id,
            BoardType = lead.BoardType,
            TargetStageName = AdminKanbanJourneyClientStageNames.WaitingCompletionConfirmation,
            TargetCurrentState = AdminKanbanJourneyStates.WaitingCompletionConfirmation,
            Reason = "Prestador informou a conclusao do servico.",
            Origin = AdminKanbanJourneyAutomationOrigins.StateMachine,
            HistoryEventType = "jornada_conclusao_solicitada",
            HistoryDescription = "A jornada entrou em aguardando confirmacao de conclusao pelo cliente.",
            MetadataJson = JsonSerializer.Serialize(new { notes = notes.Trim() }),
            ActiveTimerCode = string.Empty,
            ActiveTimerDueAtUtc = null
        });

        var notificationResult = await NotifyClientCompletionRequestAsync(lead, nowUtc, cancellationToken);
        return new JourneyServiceClosureCompletionActionResult
        {
            Success = notificationResult.Success,
            Message = notificationResult.Message,
            Context = BuildCompletionContext(lead, token, "Conclusao registrada", "O cliente ja recebeu a solicitacao para confirmar ou contestar a conclusao do atendimento.")
        };
    }

    private async Task<JourneyServiceClosureCompletionActionResult> SubmitClientDecisionCoreAsync(string token, string action, string reason, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var validation = _linkService.ValidateToken(token, JourneyServiceClosureTokenPurposes.ClientCompletionDecision, JourneyServiceClosureAudiences.Client, nowUtc);
        if (!validation.Success)
        {
            return new JourneyServiceClosureCompletionActionResult
            {
                TokenExpired = validation.Expired,
                Message = validation.Message,
                Context = BuildInvalidCompletionContext(validation)
            };
        }

        var lead = _kanbanService.GetLeadDetails(validation.Payload!.LeadId);
        if (lead is null)
        {
            return new JourneyServiceClosureCompletionActionResult
            {
                Message = "A jornada nao foi localizada para este cliente.",
                Context = new JourneyServiceClosureCompletionContext
                {
                    NotFound = true,
                    Message = "A jornada nao foi localizada para este cliente.",
                    ResponseHeadline = "Jornada indisponivel",
                    ResponseDescription = "Nao foi possivel localizar o atendimento deste link."
                }
            };
        }

        var normalizedAction = JourneyServiceClosureReviewActions.Normalize(action);
        if (normalizedAction == JourneyServiceClosureReviewActions.Contest)
        {
            _ = _kanbanService.UpdateJourneyClosure(lead.Id, BuildClosureUpdateFromLead(
                lead,
                AdminKanbanJourneyClosureStatuses.Contested,
                "O cliente contestou a conclusao do atendimento.",
                AdminKanbanJourneyStates.OperationalException,
                "jornada_conclusao_contestada",
                "O cliente contestou a conclusao do atendimento.",
                nowUtc,
                outcome: AdminKanbanJourneyCompletionOutcomes.Contested,
                contestedAtUtc: nowUtc,
                contestedReason: reason.Trim(),
                metadataJson: JsonSerializer.Serialize(new { reason = reason.Trim() })));

            _ = _kanbanService.ApplyJourneyStageAutomation(new AdminKanbanJourneyStageAutomationUpdateRequest
            {
                LeadId = lead.Id,
                BoardType = lead.BoardType,
                TargetStageName = AdminKanbanJourneyClientStageNames.OperationalException,
                TargetCurrentState = AdminKanbanJourneyStates.OperationalException,
                Reason = "Cliente contestou a conclusao do atendimento.",
                Origin = AdminKanbanJourneyAutomationOrigins.StateMachine,
                HistoryEventType = "jornada_conclusao_contestada",
                HistoryDescription = "A jornada foi encaminhada para excecao operacional apos contestacao do cliente.",
                MetadataJson = JsonSerializer.Serialize(new { reason = reason.Trim() }),
                ActiveTimerCode = string.Empty,
                ActiveTimerDueAtUtc = null
            });

            return new JourneyServiceClosureCompletionActionResult
            {
                Success = true,
                Message = "Contestacao registrada com sucesso. A equipe operacional pode revisar o caso.",
                Context = BuildCompletionContext(lead, token, "Contestacao registrada", "A contestacao foi registrada e a jornada saiu do fluxo automatico.")
            };
        }

        _ = _kanbanService.UpdateJourneyClosure(lead.Id, BuildClosureUpdateFromLead(
            lead,
            AdminKanbanJourneyClosureStatuses.WaitingClientReview,
            "Cliente confirmou a conclusao. A jornada agora aguarda a avaliacao do cliente.",
            AdminKanbanJourneyStates.WaitingClientReview,
            "jornada_conclusao_confirmada",
            "O cliente confirmou a conclusao do atendimento.",
            nowUtc,
            outcome: AdminKanbanJourneyCompletionOutcomes.Completed,
            clientConfirmedAtUtc: nowUtc,
            completedAtUtc: nowUtc,
            clientReviewStatus: AdminKanbanJourneyReviewStatuses.Pending,
            metadataJson: BuildMetadataJson(lead, "completion_confirmed")));

        _ = _kanbanService.ApplyJourneyStageAutomation(new AdminKanbanJourneyStageAutomationUpdateRequest
        {
            LeadId = lead.Id,
            BoardType = lead.BoardType,
            TargetStageName = AdminKanbanJourneyClientStageNames.WaitingClientReview,
            TargetCurrentState = AdminKanbanJourneyStates.WaitingClientReview,
            Reason = "Cliente confirmou a conclusao do atendimento.",
            Origin = AdminKanbanJourneyAutomationOrigins.StateMachine,
            HistoryEventType = "jornada_conclusao_confirmada",
            HistoryDescription = "A jornada passou a aguardar a avaliacao do cliente.",
            MetadataJson = BuildMetadataJson(lead, "completion_confirmed"),
            ActiveTimerCode = AdminKanbanJourneyTimerCodes.PendingClientReview,
            ActiveTimerDueAtUtc = nowUtc.AddHours(72)
        });

        var reviewToken = GenerateReviewToken(lead, JourneyServiceClosureAudiences.Client, nowUtc);
        return new JourneyServiceClosureCompletionActionResult
        {
            Success = true,
            Message = "Conclusao confirmada com sucesso. Agora falta registrar a avaliacao do atendimento.",
            NextClientReviewToken = reviewToken,
            Context = BuildCompletionContext(lead, token, "Conclusao confirmada", "A conclusao foi confirmada. Agora avalie o atendimento para fechar a jornada.")
        };
    }

    private async Task<JourneyServiceClosureReviewActionResult> SubmitReviewCoreAsync(string token, string audience, JourneyServiceClosureReviewSubmissionRequest request, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var normalizedAudience = JourneyServiceClosureAudiences.Normalize(audience);
        var purpose = normalizedAudience == JourneyServiceClosureAudiences.Provider
            ? JourneyServiceClosureTokenPurposes.ProviderReview
            : JourneyServiceClosureTokenPurposes.ClientReview;
        var validation = _linkService.ValidateToken(token, purpose, normalizedAudience, nowUtc);
        if (!validation.Success)
        {
            return new JourneyServiceClosureReviewActionResult
            {
                TokenExpired = validation.Expired,
                Message = validation.Message,
                Context = BuildInvalidReviewContext(validation, normalizedAudience)
            };
        }

        var lead = _kanbanService.GetLeadDetails(validation.Payload!.LeadId);
        if (lead is null)
        {
            return new JourneyServiceClosureReviewActionResult
            {
                Message = "A jornada nao foi localizada para registrar a avaliacao.",
                Context = new JourneyServiceClosureReviewContext
                {
                    NotFound = true,
                    Audience = normalizedAudience,
                    ResponseHeadline = "Avaliacao indisponivel",
                    ResponseDescription = "Nao foi possivel localizar o atendimento desta avaliacao."
                }
            };
        }

        var reviewRecord = new AdminKanbanJourneyReviewRecord
        {
            Rating = Math.Clamp(request.Rating, 1, 5),
            Comment = request.Comment.Trim(),
            LowScoreReason = ReviewRequiresReason(request.Rating) ? request.LowScoreReason.Trim() : string.Empty,
            WouldHireAgain = request.WouldHireAgain,
            SubmittedAtUtc = nowUtc
        };

        if (normalizedAudience == JourneyServiceClosureAudiences.Client)
        {
            _ = _kanbanService.UpdateJourneyClosure(lead.Id, BuildClosureUpdateFromLead(
                lead,
                AdminKanbanJourneyClosureStatuses.WaitingProviderReview,
                "Cliente avaliou o atendimento. A jornada agora aguarda a avaliacao do prestador.",
                AdminKanbanJourneyStates.WaitingProviderReview,
                "jornada_avaliacao_cliente_enviada",
                "O cliente concluiu a avaliacao do atendimento.",
                nowUtc,
                completedAtUtc: lead.Journey.Closure.CompletedAtUtc ?? nowUtc,
                clientReviewStatus: AdminKanbanJourneyReviewStatuses.Submitted,
                clientReview: reviewRecord,
                metadataJson: JsonSerializer.Serialize(new { rating = reviewRecord.Rating, lowScoreReason = reviewRecord.LowScoreReason })));

            _ = _kanbanService.ApplyJourneyStageAutomation(new AdminKanbanJourneyStageAutomationUpdateRequest
            {
                LeadId = lead.Id,
                BoardType = lead.BoardType,
                TargetStageName = AdminKanbanJourneyClientStageNames.WaitingProviderReview,
                TargetCurrentState = AdminKanbanJourneyStates.WaitingProviderReview,
                Reason = "Cliente enviou a avaliacao do atendimento.",
                Origin = AdminKanbanJourneyAutomationOrigins.StateMachine,
                HistoryEventType = "jornada_avaliacao_cliente_enviada",
                HistoryDescription = "A jornada passou a aguardar a avaliacao do prestador.",
                MetadataJson = JsonSerializer.Serialize(new { rating = reviewRecord.Rating }),
                ActiveTimerCode = AdminKanbanJourneyTimerCodes.PendingProviderReview,
                ActiveTimerDueAtUtc = nowUtc.AddHours(72)
            });

            var providerNotification = await NotifyProviderReviewRequestAsync(lead, nowUtc, cancellationToken);
            var reviewContext = GetReviewContext(token, normalizedAudience, nowUtc);
            return new JourneyServiceClosureReviewActionResult
            {
                Success = true,
                Message = providerNotification.Message,
                Context = new JourneyServiceClosureReviewContext
                {
                    Success = reviewContext.Success,
                    TokenExpired = reviewContext.TokenExpired,
                    NotFound = reviewContext.NotFound,
                    Message = reviewContext.Message,
                    Audience = reviewContext.Audience,
                    LeadName = reviewContext.LeadName,
                    CounterpartyName = reviewContext.CounterpartyName,
                    RequestedCategory = reviewContext.RequestedCategory,
                    ScheduledWindowLabel = reviewContext.ScheduledWindowLabel,
                    AddressSummary = reviewContext.AddressSummary,
                    ResponseHeadline = "Avaliacao registrada",
                    ResponseDescription = "Sua avaliacao foi registrada com sucesso.",
                    Token = reviewContext.Token,
                    CanRespond = false,
                    AlreadyResponded = true
                }
            };
        }

        _ = _kanbanService.UpdateJourneyClosure(lead.Id, BuildClosureUpdateFromLead(
            lead,
            AdminKanbanJourneyClosureStatuses.Completed,
            "Cliente e prestador avaliaram o atendimento. A jornada foi concluida.",
            AdminKanbanJourneyStates.Completed,
            "jornada_avaliacao_prestador_enviada",
            "O prestador concluiu a avaliacao do cliente e a jornada foi encerrada.",
            nowUtc,
            completedAtUtc: lead.Journey.Closure.CompletedAtUtc ?? nowUtc,
            providerReviewStatus: AdminKanbanJourneyReviewStatuses.Submitted,
            providerReview: reviewRecord,
            metadataJson: JsonSerializer.Serialize(new { rating = reviewRecord.Rating, lowScoreReason = reviewRecord.LowScoreReason })));

        _ = _kanbanService.ApplyJourneyStageAutomation(new AdminKanbanJourneyStageAutomationUpdateRequest
        {
            LeadId = lead.Id,
            BoardType = lead.BoardType,
            TargetStageName = AdminKanbanJourneyClientStageNames.Completed,
            TargetCurrentState = AdminKanbanJourneyStates.Completed,
            Reason = "Prestador enviou a avaliacao final do atendimento.",
            Origin = AdminKanbanJourneyAutomationOrigins.StateMachine,
            HistoryEventType = "jornada_avaliacao_prestador_enviada",
            HistoryDescription = "A jornada foi concluida apos a avaliacao bilateral.",
            MetadataJson = JsonSerializer.Serialize(new { rating = reviewRecord.Rating }),
            ActiveTimerCode = string.Empty,
            ActiveTimerDueAtUtc = null
        });

        var completedReviewContext = GetReviewContext(token, normalizedAudience, nowUtc);
        return new JourneyServiceClosureReviewActionResult
        {
            Success = true,
            Message = "Avaliacao registrada com sucesso. A jornada foi concluida.",
            Context = new JourneyServiceClosureReviewContext
            {
                Success = completedReviewContext.Success,
                TokenExpired = completedReviewContext.TokenExpired,
                NotFound = completedReviewContext.NotFound,
                Message = completedReviewContext.Message,
                Audience = completedReviewContext.Audience,
                LeadName = completedReviewContext.LeadName,
                CounterpartyName = completedReviewContext.CounterpartyName,
                RequestedCategory = completedReviewContext.RequestedCategory,
                ScheduledWindowLabel = completedReviewContext.ScheduledWindowLabel,
                AddressSummary = completedReviewContext.AddressSummary,
                ResponseHeadline = "Avaliacao registrada",
                ResponseDescription = "Sua avaliacao foi registrada com sucesso e o caso foi concluido.",
                Token = completedReviewContext.Token,
                CanRespond = false,
                AlreadyResponded = true
            }
        };
    }

    private async Task<JourneyServiceClosureNotificationResult> NotifyProviderCompletionRequestAsync(AdminKanbanLeadDetailsRecord lead, DateTime nowUtc, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(lead.Journey.Dispatch.ReservedProviderEmail))
        {
            return new JourneyServiceClosureNotificationResult
            {
                Message = "Servico em andamento registrado, mas o prestador esta sem e-mail para receber o link de conclusao."
            };
        }

        var token = _linkService.GenerateToken(
            JourneyServiceClosureTokenPurposes.ProviderCompletion,
            JourneyServiceClosureAudiences.Provider,
            lead.Id,
            lead.Journey.JourneyId,
            lead.Journey.Dispatch.ReservedProviderId ?? Guid.Empty,
            nowUtc.AddHours(_options.CompletionLinkExpirationHours));
        var url = _linkService.BuildProviderCompletionUrl(token);
        var subject = $"Registrar conclusao do atendimento - {ResolveCategory(lead)}";
        var body = BuildProviderCompletionEmailBody(lead, url);
        var sendResult = await TrySendEmailAsync(
            lead.Journey.Dispatch.ReservedProviderEmail,
            subject,
            body,
            lead.Id,
            lead.Journey.Dispatch.ReservedProviderId ?? Guid.Empty,
            "prestador",
            cancellationToken);

        if (sendResult.Success)
        {
            _ = _kanbanService.UpdateJourneyClosure(lead.Id, new AdminKanbanJourneyClosureUpdateRequest
            {
                Status = AdminKanbanJourneyClosureStatuses.ServiceInProgress,
                Summary = "Servico em andamento. Prestador ja recebeu o link para registrar a conclusao.",
                Outcome = lead.Journey.Closure.Outcome,
                ServiceInProgressAtUtc = lead.Journey.Closure.ServiceInProgressAtUtc ?? nowUtc,
                ProviderCompletionRequestedAtUtc = nowUtc,
                ProviderCompletionSubmittedAtUtc = lead.Journey.Closure.ProviderCompletionSubmittedAtUtc,
                ClientConfirmationRequestedAtUtc = lead.Journey.Closure.ClientConfirmationRequestedAtUtc,
                ClientConfirmedAtUtc = lead.Journey.Closure.ClientConfirmedAtUtc,
                CompletedAtUtc = lead.Journey.Closure.CompletedAtUtc,
                ContestedAtUtc = lead.Journey.Closure.ContestedAtUtc,
                ContestedReason = lead.Journey.Closure.ContestedReason,
                ClientReviewStatus = lead.Journey.Closure.ClientReviewStatus,
                ProviderReviewStatus = lead.Journey.Closure.ProviderReviewStatus,
                ClientReview = lead.Journey.Closure.ClientReview,
                ProviderReview = lead.Journey.Closure.ProviderReview,
                CurrentState = AdminKanbanJourneyStates.ServiceInProgress,
                HistoryEventType = "jornada_conclusao_link_prestador_enviado",
                HistoryDescription = "O prestador recebeu o link assinado para registrar a conclusao do atendimento.",
                SourceChannel = lead.Journey.SourceChannel,
                MetadataJson = JsonSerializer.Serialize(new { audience = "prestador" })
            });
        }

        return new JourneyServiceClosureNotificationResult
        {
            Success = sendResult.Success,
            Message = sendResult.Success
                ? "Servico em andamento registrado e link de conclusao enviado ao prestador."
                : $"Servico em andamento registrado, mas houve falha ao notificar o prestador: {sendResult.ErrorMessage}"
        };
    }

    private async Task<JourneyServiceClosureNotificationResult> NotifyClientCompletionRequestAsync(AdminKanbanLeadDetailsRecord lead, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var decisionToken = _linkService.GenerateToken(
            JourneyServiceClosureTokenPurposes.ClientCompletionDecision,
            JourneyServiceClosureAudiences.Client,
            lead.Id,
            lead.Journey.JourneyId,
            lead.Journey.Dispatch.ReservedProviderId ?? Guid.Empty,
            nowUtc.AddHours(_options.CompletionLinkExpirationHours));
        var confirmUrl = _linkService.BuildClientCompletionUrl(decisionToken, JourneyServiceClosureReviewActions.Confirm);
        var contestUrl = _linkService.BuildClientCompletionUrl(decisionToken, JourneyServiceClosureReviewActions.Contest);

        var telegramMessage = $"""
O prestador informou que o atendimento foi concluido.

Janela: {BuildSchedulingWindowLabel(lead)}
Servico: {ResolveCategory(lead)}

Confirmar conclusao:
{confirmUrl}

Contestar conclusao:
{contestUrl}
""";

        if (lead.Telegram.TelegramChatId.HasValue && lead.Telegram.TelegramChatId.Value > 0)
        {
            var telegramResult = await _telegramBridgeDeliveryClient.SendHumanReplyAsync(
                new TelegramBridgeHumanReplyRequest
                {
                    LeadId = lead.Id,
                    TelegramChatId = lead.Telegram.TelegramChatId.Value,
                    SenderName = "ConsertaPraMim",
                    MessageText = telegramMessage,
                    ActivateHumanHandoff = false
                },
                cancellationToken);

            return new JourneyServiceClosureNotificationResult
            {
                Success = telegramResult.Success,
                Message = telegramResult.Success
                    ? "O cliente recebeu a solicitacao para confirmar a conclusao do atendimento."
                    : $"Falha ao enviar a solicitacao de conclusao pelo Telegram: {telegramResult.Message}"
            };
        }

        var clientEmail = ResolveClientEmail(lead);
        if (string.IsNullOrWhiteSpace(clientEmail))
        {
            return new JourneyServiceClosureNotificationResult
            {
                Message = "Cliente sem Telegram ativo e sem e-mail valido para confirmar a conclusao."
            };
        }

        var body = BuildClientCompletionEmailBody(lead, confirmUrl, contestUrl);
        var sendResult = await TrySendEmailAsync(
            clientEmail,
            $"Confirmar conclusao do atendimento - {ResolveCategory(lead)}",
            body,
            lead.Id,
            lead.Journey.Dispatch.ReservedProviderId ?? Guid.Empty,
            "cliente",
            cancellationToken);

        return new JourneyServiceClosureNotificationResult
        {
            Success = sendResult.Success,
            Message = sendResult.Success
                ? "O cliente recebeu a solicitacao para confirmar a conclusao do atendimento."
                : $"Falha ao notificar o cliente por e-mail: {sendResult.ErrorMessage}"
        };
    }

    private async Task<JourneyServiceClosureNotificationResult> NotifyProviderReviewRequestAsync(AdminKanbanLeadDetailsRecord lead, DateTime nowUtc, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(lead.Journey.Dispatch.ReservedProviderEmail))
        {
            return new JourneyServiceClosureNotificationResult
            {
                Message = "Avaliacao do cliente registrada, mas o prestador esta sem e-mail para receber a propria avaliacao."
            };
        }

        var token = GenerateReviewToken(lead, JourneyServiceClosureAudiences.Provider, nowUtc);
        var url = _linkService.BuildReviewUrl(token);
        var body = BuildProviderReviewEmailBody(lead, url);
        var sendResult = await TrySendEmailAsync(
            lead.Journey.Dispatch.ReservedProviderEmail,
            $"Avalie o cliente deste atendimento - {ResolveCategory(lead)}",
            body,
            lead.Id,
            lead.Journey.Dispatch.ReservedProviderId ?? Guid.Empty,
            "prestador",
            cancellationToken);

        return new JourneyServiceClosureNotificationResult
        {
            Success = sendResult.Success,
            Message = sendResult.Success
                ? "Avaliacao do cliente registrada. O prestador ja recebeu a solicitacao de avaliacao."
                : $"Avaliacao do cliente registrada, mas houve falha ao notificar o prestador: {sendResult.ErrorMessage}"
        };
    }

    private static JourneyServiceClosureCompletionContext BuildInvalidCompletionContext(JourneyServiceClosureTokenValidationResult validation)
    {
        return new JourneyServiceClosureCompletionContext
        {
            TokenExpired = validation.Expired,
            Message = validation.Message,
            ResponseHeadline = validation.Expired ? "Link expirado" : "Link invalido",
            ResponseDescription = validation.Message
        };
    }

    private static JourneyServiceClosureReviewContext BuildInvalidReviewContext(JourneyServiceClosureTokenValidationResult validation, string audience)
    {
        return new JourneyServiceClosureReviewContext
        {
            Audience = audience,
            TokenExpired = validation.Expired,
            Message = validation.Message,
            ResponseHeadline = validation.Expired ? "Link expirado" : "Link invalido",
            ResponseDescription = validation.Message
        };
    }

    private static JourneyServiceClosureCompletionContext BuildCompletionContext(AdminKanbanLeadDetailsRecord lead, string token, string headline, string description)
    {
        return new JourneyServiceClosureCompletionContext
        {
            Success = true,
            LeadName = lead.Name,
            ProviderName = lead.Journey.Dispatch.ReservedProviderName,
            RequestedCategory = ResolveCategory(lead),
            AddressSummary = BuildAddressSummary(lead),
            ScheduledWindowLabel = BuildSchedulingWindowLabel(lead),
            CompletionStatusLabel = string.IsNullOrWhiteSpace(lead.Journey.Closure.Status)
                ? "-"
                : AdminKanbanJourneyClosureStatuses.GetLabel(lead.Journey.Closure.Status),
            ResponseHeadline = headline,
            ResponseDescription = description,
            Token = token
        };
    }

    private static bool IsReservedProvider(AdminKanbanLeadDetailsRecord lead, Guid providerId) =>
        lead.Journey.Dispatch.ReservedProviderId.HasValue &&
        lead.Journey.Dispatch.ReservedProviderId.Value == providerId;

    private bool ReviewRequiresReason(int rating) => rating > 0 && rating <= _options.LowScoreThreshold;

    private static AdminKanbanJourneyClosureUpdateRequest BuildClosureUpdateFromLead(
        AdminKanbanLeadDetailsRecord lead,
        string status,
        string summary,
        string currentState,
        string historyEventType,
        string historyDescription,
        DateTime nowUtc,
        string? outcome = null,
        DateTime? providerCompletionSubmittedAtUtc = null,
        DateTime? clientConfirmationRequestedAtUtc = null,
        DateTime? clientConfirmedAtUtc = null,
        DateTime? completedAtUtc = null,
        DateTime? contestedAtUtc = null,
        string? contestedReason = null,
        string? clientReviewStatus = null,
        string? providerReviewStatus = null,
        AdminKanbanJourneyReviewRecord? clientReview = null,
        AdminKanbanJourneyReviewRecord? providerReview = null,
        string? metadataJson = null)
    {
        return new AdminKanbanJourneyClosureUpdateRequest
        {
            Status = status,
            Summary = summary,
            Outcome = outcome ?? lead.Journey.Closure.Outcome,
            ServiceInProgressAtUtc = lead.Journey.Closure.ServiceInProgressAtUtc ?? nowUtc,
            ProviderCompletionRequestedAtUtc = lead.Journey.Closure.ProviderCompletionRequestedAtUtc,
            ProviderCompletionSubmittedAtUtc = providerCompletionSubmittedAtUtc ?? lead.Journey.Closure.ProviderCompletionSubmittedAtUtc,
            ClientConfirmationRequestedAtUtc = clientConfirmationRequestedAtUtc ?? lead.Journey.Closure.ClientConfirmationRequestedAtUtc,
            ClientConfirmedAtUtc = clientConfirmedAtUtc ?? lead.Journey.Closure.ClientConfirmedAtUtc,
            CompletedAtUtc = completedAtUtc ?? lead.Journey.Closure.CompletedAtUtc,
            ContestedAtUtc = contestedAtUtc ?? lead.Journey.Closure.ContestedAtUtc,
            ContestedReason = contestedReason ?? lead.Journey.Closure.ContestedReason,
            ClientReviewStatus = clientReviewStatus ?? lead.Journey.Closure.ClientReviewStatus,
            ProviderReviewStatus = providerReviewStatus ?? lead.Journey.Closure.ProviderReviewStatus,
            ClientReview = clientReview ?? lead.Journey.Closure.ClientReview,
            ProviderReview = providerReview ?? lead.Journey.Closure.ProviderReview,
            CurrentState = currentState,
            HistoryEventType = historyEventType,
            HistoryDescription = historyDescription,
            SourceChannel = lead.Journey.SourceChannel,
            MetadataJson = metadataJson ?? BuildMetadataJson(lead, historyEventType)
        };
    }

    private string GenerateReviewToken(AdminKanbanLeadDetailsRecord lead, string audience, DateTime nowUtc)
    {
        var purpose = JourneyServiceClosureAudiences.Normalize(audience) == JourneyServiceClosureAudiences.Provider
            ? JourneyServiceClosureTokenPurposes.ProviderReview
            : JourneyServiceClosureTokenPurposes.ClientReview;

        return _linkService.GenerateToken(
            purpose,
            audience,
            lead.Id,
            lead.Journey.JourneyId,
            lead.Journey.Dispatch.ReservedProviderId ?? Guid.Empty,
            nowUtc.AddHours(_options.ReviewLinkExpirationHours));
    }

    private async Task<(bool Success, string ErrorMessage)> TrySendEmailAsync(
        string recipientEmail,
        string subject,
        string htmlBody,
        int leadId,
        Guid providerId,
        string audience,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_notificationOptions.Enabled || !_notificationOptions.EmailEnabled)
            {
                return (false, "notificacao por e-mail desabilitada no ambiente atual.");
            }

            if (NormalizeTransport(_notificationOptions.EmailTransport) == "smtp")
            {
                using var message = new MailMessage
                {
                    From = new MailAddress(_notificationOptions.SenderEmail.Trim(), _notificationOptions.SenderDisplayName.Trim()),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };
                message.To.Add(recipientEmail.Trim());

                using var client = new SmtpClient(_notificationOptions.SmtpHost.Trim(), _notificationOptions.SmtpPort)
                {
                    EnableSsl = _notificationOptions.SmtpUseSsl,
                    Credentials = new NetworkCredential(_notificationOptions.SmtpUsername.Trim(), _notificationOptions.SmtpPassword)
                };

                cancellationToken.ThrowIfCancellationRequested();
                await client.SendMailAsync(message);
            }
            else
            {
                _logger.LogInformation(
                    "JOURNEY CLOSURE EMAIL [LOG] Audience={Audience} To={To} LeadId={LeadId} ProviderId={ProviderId} Subject={Subject} Body={Body}",
                    audience,
                    recipientEmail,
                    leadId,
                    providerId,
                    subject,
                    htmlBody);
            }

            return (true, string.Empty);
        }
        catch (SmtpException ex)
        {
            _logger.LogError(ex, "Falha SMTP ao notificar {Audience} na jornada. LeadId={LeadId} ProviderId={ProviderId}.", audience, leadId, providerId);
            return (false, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao notificar {Audience} na jornada. LeadId={LeadId} ProviderId={ProviderId}.", audience, leadId, providerId);
            return (false, ex.Message);
        }
    }

    private static string BuildMetadataJson(AdminKanbanLeadDetailsRecord lead, string eventCode)
    {
        return JsonSerializer.Serialize(new
        {
            leadId = lead.Id,
            journeyId = lead.Journey.JourneyId,
            eventCode,
            sourceChannel = lead.Journey.SourceChannel,
            reservedProviderId = lead.Journey.Dispatch.ReservedProviderId
        });
    }

    private static string BuildProviderCompletionEmailBody(AdminKanbanLeadDetailsRecord lead, Uri completionUrl)
    {
        var encoder = HtmlEncoder.Default;
        return $"""
<!DOCTYPE html>
<html lang="pt-BR">
<head><meta charset="utf-8"><title>Registrar conclusao</title></head>
<body style="margin:0;padding:24px;background:#f4f7fb;font-family:Segoe UI,Arial,sans-serif;color:#17202a;">
  <article style="max-width:680px;margin:0 auto;background:#ffffff;border-radius:18px;padding:32px;border:1px solid #d9e2ec;">
    <p style="margin:0 0 12px;font-size:14px;letter-spacing:.08em;text-transform:uppercase;color:#5b7083;">ConsertaPraMim</p>
    <h1 style="margin:0 0 16px;font-size:28px;line-height:1.2;">Registrar conclusao do atendimento</h1>
    <p style="margin:0 0 20px;font-size:16px;line-height:1.6;">Quando o atendimento terminar, registre aqui o desfecho oficial do caso.</p>
    <p style="margin:0 0 12px;"><strong>Cliente:</strong> {encoder.Encode(lead.Name)}</p>
    <p style="margin:0 0 12px;"><strong>Categoria:</strong> {encoder.Encode(ResolveCategory(lead))}</p>
    <p style="margin:0 0 24px;"><strong>Janela:</strong> {encoder.Encode(BuildSchedulingWindowLabel(lead))}</p>
    <p><a href="{encoder.Encode(completionUrl.ToString())}" style="display:inline-block;padding:14px 20px;border-radius:12px;background:#0f766e;color:#ffffff;text-decoration:none;font-weight:700;">Registrar desfecho do atendimento</a></p>
  </article>
</body>
</html>
""";
    }

    private static string BuildClientCompletionEmailBody(AdminKanbanLeadDetailsRecord lead, Uri confirmUrl, Uri contestUrl)
    {
        var encoder = HtmlEncoder.Default;
        return $"""
<!DOCTYPE html>
<html lang="pt-BR">
<head><meta charset="utf-8"><title>Confirmar conclusao</title></head>
<body style="margin:0;padding:24px;background:#f4f7fb;font-family:Segoe UI,Arial,sans-serif;color:#17202a;">
  <article style="max-width:680px;margin:0 auto;background:#ffffff;border-radius:18px;padding:32px;border:1px solid #d9e2ec;">
    <p style="margin:0 0 12px;font-size:14px;letter-spacing:.08em;text-transform:uppercase;color:#5b7083;">ConsertaPraMim</p>
    <h1 style="margin:0 0 16px;font-size:28px;line-height:1.2;">Confirme a conclusao do atendimento</h1>
    <p style="margin:0 0 20px;font-size:16px;line-height:1.6;">O prestador informou que o servico foi concluido. Escolha uma das opcoes abaixo.</p>
    <p style="margin:0 0 12px;"><strong>Categoria:</strong> {encoder.Encode(ResolveCategory(lead))}</p>
    <p style="margin:0 0 24px;"><strong>Janela:</strong> {encoder.Encode(BuildSchedulingWindowLabel(lead))}</p>
    <p><a href="{encoder.Encode(confirmUrl.ToString())}" style="display:inline-block;margin-right:12px;padding:14px 20px;border-radius:12px;background:#0f766e;color:#ffffff;text-decoration:none;font-weight:700;">Confirmar conclusao</a></p>
    <p><a href="{encoder.Encode(contestUrl.ToString())}" style="display:inline-block;padding:14px 20px;border-radius:12px;background:#ffffff;border:1px solid #d9e2ec;color:#102a43;text-decoration:none;font-weight:700;">Contestar conclusao</a></p>
  </article>
</body>
</html>
""";
    }

    private static string BuildProviderReviewEmailBody(AdminKanbanLeadDetailsRecord lead, Uri reviewUrl)
    {
        var encoder = HtmlEncoder.Default;
        return $"""
<!DOCTYPE html>
<html lang="pt-BR">
<head><meta charset="utf-8"><title>Avaliar cliente</title></head>
<body style="margin:0;padding:24px;background:#f4f7fb;font-family:Segoe UI,Arial,sans-serif;color:#17202a;">
  <article style="max-width:680px;margin:0 auto;background:#ffffff;border-radius:18px;padding:32px;border:1px solid #d9e2ec;">
    <p style="margin:0 0 12px;font-size:14px;letter-spacing:.08em;text-transform:uppercase;color:#5b7083;">ConsertaPraMim</p>
    <h1 style="margin:0 0 16px;font-size:28px;line-height:1.2;">Avalie o cliente deste atendimento</h1>
    <p style="margin:0 0 20px;font-size:16px;line-height:1.6;">O cliente ja avaliou este atendimento. Falta apenas a sua avaliacao para concluir a jornada.</p>
    <p style="margin:0 0 24px;"><strong>Categoria:</strong> {encoder.Encode(ResolveCategory(lead))}</p>
    <p><a href="{encoder.Encode(reviewUrl.ToString())}" style="display:inline-block;padding:14px 20px;border-radius:12px;background:#0f766e;color:#ffffff;text-decoration:none;font-weight:700;">Registrar avaliacao</a></p>
  </article>
</body>
</html>
""";
    }

    private static string ResolveClientEmail(AdminKanbanLeadDetailsRecord lead)
    {
        if (!string.IsNullOrWhiteSpace(lead.Journey.PrimaryEmail))
        {
            return lead.Journey.PrimaryEmail;
        }

        if (!string.IsNullOrWhiteSpace(lead.Email))
        {
            return lead.Email;
        }

        return lead.Telegram.ClientEmail ?? string.Empty;
    }

    private static string ResolveCategory(AdminKanbanLeadDetailsRecord lead)
    {
        if (!string.IsNullOrWhiteSpace(lead.Journey.Matching.RequestedCategory))
        {
            return lead.Journey.Matching.RequestedCategory;
        }

        if (!string.IsNullOrWhiteSpace(lead.Journey.Qualification.NormalizedServiceCategoryName))
        {
            return lead.Journey.Qualification.NormalizedServiceCategoryName;
        }

        return string.IsNullOrWhiteSpace(lead.ServiceCategory) ? "Servico solicitado" : lead.ServiceCategory;
    }

    private static string BuildAddressSummary(AdminKanbanLeadDetailsRecord lead)
    {
        var parts = new[]
        {
            lead.Journey.Qualification.Street,
            lead.Journey.Qualification.Neighborhood,
            lead.Journey.Qualification.City,
            lead.Journey.Qualification.State,
            lead.Journey.Qualification.PostalCode
        }.Where(part => !string.IsNullOrWhiteSpace(part)).ToList();

        return parts.Count == 0 ? "Endereco validado na jornada." : string.Join(", ", parts);
    }

    private static string BuildSchedulingWindowLabel(AdminKanbanLeadDetailsRecord lead)
    {
        var start = NormalizeUtc(lead.Journey.Scheduling.ScheduledStartAtUtc);
        var end = NormalizeUtc(lead.Journey.Scheduling.ScheduledEndAtUtc);
        if (!start.HasValue || !end.HasValue)
        {
            return "Janela em confirmacao operacional";
        }

        var timezone = ResolveBusinessTimeZone();
        var localStart = TimeZoneInfo.ConvertTimeFromUtc(start.Value, timezone);
        var localEnd = TimeZoneInfo.ConvertTimeFromUtc(end.Value, timezone);
        return $"{localStart:dd/MM/yyyy HH:mm} - {localEnd:HH:mm} (America/Sao_Paulo)";
    }

    private static string NormalizeTransport(string? transport) => (transport ?? string.Empty).Trim().ToLowerInvariant();

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

    private static TimeZoneInfo ResolveBusinessTimeZone()
    {
        foreach (var timeZoneId in new[] { "America/Sao_Paulo", "E. South America Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }
}
