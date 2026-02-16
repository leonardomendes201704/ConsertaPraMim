using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ConsertaPraMim.Application.Services;

public class AdminDisputeQueueService : IAdminDisputeQueueService
{
    private const string LgpdTextPlaceholder = "[ANONIMIZADO_POR_POLITICA_LGPD]";
    private const string LgpdFileUrlPlaceholder = "#anonymized";

    private readonly IServiceDisputeCaseRepository _serviceDisputeCaseRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAdminAuditLogRepository _adminAuditLogRepository;
    private readonly IServicePaymentTransactionRepository _servicePaymentTransactionRepository;
    private readonly IPaymentService _paymentService;
    private readonly IProviderCreditService _providerCreditService;
    private readonly INotificationService _notificationService;

    public AdminDisputeQueueService(
        IServiceDisputeCaseRepository serviceDisputeCaseRepository,
        IUserRepository userRepository,
        IAdminAuditLogRepository adminAuditLogRepository,
        IServicePaymentTransactionRepository servicePaymentTransactionRepository,
        IPaymentService paymentService,
        IProviderCreditService providerCreditService,
        INotificationService notificationService)
    {
        _serviceDisputeCaseRepository = serviceDisputeCaseRepository;
        _userRepository = userRepository;
        _adminAuditLogRepository = adminAuditLogRepository;
        _servicePaymentTransactionRepository = servicePaymentTransactionRepository;
        _paymentService = paymentService;
        _providerCreditService = providerCreditService;
        _notificationService = notificationService;
    }

    public async Task<AdminDisputesQueueResponseDto> GetQueueAsync(
        Guid? highlightedDisputeCaseId,
        int take = 100,
        string? status = null,
        string? type = null,
        Guid? operatorAdminId = null,
        string? operatorScope = null,
        string? sla = null)
    {
        var normalizedTake = Math.Clamp(take, 1, 200);
        var nowUtc = DateTime.UtcNow;
        var cases = (await _serviceDisputeCaseRepository.GetOpenCasesAsync(200))
            .Where(c => IsOpenStatus(c.Status))
            .ToList();

        if (TryParseStatus(status, out var parsedStatus))
        {
            cases = cases
                .Where(c => c.Status == parsedStatus)
                .ToList();
        }

        if (TryParseType(type, out var parsedType))
        {
            cases = cases
                .Where(c => c.Type == parsedType)
                .ToList();
        }

        if (operatorAdminId.HasValue && operatorAdminId.Value != Guid.Empty)
        {
            cases = cases
                .Where(c => c.OwnedByAdminUserId == operatorAdminId.Value)
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(operatorScope))
        {
            var normalizedScope = operatorScope.Trim().ToLowerInvariant();
            if (normalizedScope == "assigned")
            {
                cases = cases.Where(c => c.OwnedByAdminUserId.HasValue).ToList();
            }
            else if (normalizedScope == "unassigned")
            {
                cases = cases.Where(c => !c.OwnedByAdminUserId.HasValue).ToList();
            }
        }

        if (!string.IsNullOrWhiteSpace(sla))
        {
            var normalizedSla = sla.Trim().ToLowerInvariant();
            if (normalizedSla == "breached")
            {
                cases = cases
                    .Where(c => c.SlaDueAtUtc < nowUtc)
                    .ToList();
            }
            else if (normalizedSla is "ontrack" or "within")
            {
                cases = cases
                    .Where(c => c.SlaDueAtUtc >= nowUtc)
                    .ToList();
            }
        }

        cases = cases
            .OrderByDescending(c => c.Priority)
            .ThenBy(c => c.SlaDueAtUtc)
            .ThenByDescending(c => c.CreatedAt)
            .Take(normalizedTake)
            .ToList();

        var items = cases
            .Select(MapToQueueItem)
            .ToList();

        return new AdminDisputesQueueResponseDto(
            highlightedDisputeCaseId,
            items.Count,
            items);
    }

    public async Task<string> ExportQueueCsvAsync(
        Guid? highlightedDisputeCaseId,
        int take = 200,
        string? status = null,
        string? type = null,
        Guid? operatorAdminId = null,
        string? operatorScope = null,
        string? sla = null)
    {
        var queue = await GetQueueAsync(
            highlightedDisputeCaseId,
            take,
            status,
            type,
            operatorAdminId,
            operatorScope,
            sla);

        var sb = new StringBuilder();
        sb.AppendLine("DisputeCaseId,ServiceRequestId,ServiceAppointmentId,Type,Priority,Status,ReasonCode,OpenedByName,OpenedByRole,CounterpartyName,CounterpartyRole,OwnerAdminId,OwnerAdminName,City,Category,SlaDueAtUtc,IsSlaBreached,OpenedAtUtc,LastInteractionAtUtc,AttachmentCount,MessageCount,ActionUrl");

        foreach (var item in queue.Items)
        {
            sb.AppendLine(string.Join(",",
                EscapeCsv(item.DisputeCaseId.ToString("D")),
                EscapeCsv(item.ServiceRequestId.ToString("D")),
                EscapeCsv(item.ServiceAppointmentId.ToString("D")),
                EscapeCsv(item.Type),
                EscapeCsv(item.Priority),
                EscapeCsv(item.Status),
                EscapeCsv(item.ReasonCode),
                EscapeCsv(item.OpenedByName),
                EscapeCsv(item.OpenedByRole),
                EscapeCsv(item.CounterpartyName),
                EscapeCsv(item.CounterpartyRole),
                EscapeCsv(item.OwnedByAdminUserId?.ToString("D")),
                EscapeCsv(item.OwnedByAdminName),
                EscapeCsv(item.City),
                EscapeCsv(item.Category),
                EscapeCsv(item.SlaDueAtUtc.ToString("o")),
                EscapeCsv(item.IsSlaBreached ? "true" : "false"),
                EscapeCsv(item.OpenedAtUtc.ToString("o")),
                EscapeCsv(item.LastInteractionAtUtc.ToString("o")),
                EscapeCsv(item.AttachmentCount.ToString()),
                EscapeCsv(item.MessageCount.ToString()),
                EscapeCsv(item.ActionUrl)));
        }

        return sb.ToString();
    }

    public async Task<AdminDisputeObservabilityDashboardDto> GetObservabilityAsync(AdminDisputeObservabilityQueryDto query)
    {
        var normalizedQuery = query ?? new AdminDisputeObservabilityQueryDto();
        var (fromUtc, toUtc) = NormalizeObservabilityRange(normalizedQuery.FromUtc, normalizedQuery.ToUtc);
        var topTake = Math.Clamp(normalizedQuery.TopTake, 3, 50);
        var nowUtc = DateTime.UtcNow;

        var disputeCases = (await _serviceDisputeCaseRepository.GetCasesByOpenedPeriodAsync(fromUtc, toUtc, 20000))
            .ToList();

        var openCases = disputeCases.Count(c => IsOpenStatus(c.Status));
        var closedCases = disputeCases.Count(c => IsClosedStatus(c.Status));
        var resolvedCases = disputeCases.Count(c => c.Status == DisputeCaseStatus.Resolved);
        var rejectedCases = disputeCases.Count(c => c.Status == DisputeCaseStatus.Rejected);
        var slaBreachedOpenCases = disputeCases.Count(c => IsOpenStatus(c.Status) && c.SlaDueAtUtc < nowUtc);

        var closedWithDecision = disputeCases
            .Where(c => c.Status is DisputeCaseStatus.Resolved or DisputeCaseStatus.Rejected)
            .ToList();
        var proceedingCount = closedWithDecision.Count(c => IsProceedingOutcome(c.Status, c.MetadataJson));
        var decisionProceedingRatePercent = closedWithDecision.Count == 0
            ? 0m
            : decimal.Round((decimal)proceedingCount * 100m / closedWithDecision.Count, 2, MidpointRounding.AwayFromZero);

        var resolutionHours = disputeCases
            .Where(c => c.ClosedAtUtc.HasValue && c.ClosedAtUtc.Value > c.OpenedAtUtc)
            .Select(c => (decimal)(c.ClosedAtUtc!.Value - c.OpenedAtUtc).TotalHours)
            .OrderBy(v => v)
            .ToList();
        var averageResolutionHours = resolutionHours.Count == 0
            ? 0m
            : decimal.Round(resolutionHours.Average(), 2, MidpointRounding.AwayFromZero);
        var medianResolutionHours = resolutionHours.Count == 0
            ? 0m
            : CalculateMedian(resolutionHours);

        var casesByType = disputeCases
            .GroupBy(c => c.Type.ToString())
            .Select(g => new AdminStatusCountDto(g.Key, g.Count()))
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.Status)
            .ToList();

        var casesByPriority = disputeCases
            .GroupBy(c => c.Priority.ToString())
            .Select(g => new AdminStatusCountDto(g.Key, g.Count()))
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.Status)
            .ToList();

        var casesByStatus = disputeCases
            .GroupBy(c => c.Status.ToString())
            .Select(g => new AdminStatusCountDto(g.Key, g.Count()))
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.Status)
            .ToList();

        var topReasons = disputeCases
            .GroupBy(c => string.IsNullOrWhiteSpace(c.ReasonCode) ? "UNSPECIFIED" : c.ReasonCode.Trim().ToUpperInvariant())
            .Select(g =>
            {
                var total = g.Count();
                var proceedingByReason = g.Count(c => IsProceedingOutcome(c.Status, c.MetadataJson));
                var proceedingRate = total == 0
                    ? 0m
                    : decimal.Round((decimal)proceedingByReason * 100m / total, 2, MidpointRounding.AwayFromZero);
                return new AdminDisputeReasonKpiDto(
                    g.Key,
                    total,
                    proceedingByReason,
                    proceedingRate);
            })
            .OrderByDescending(x => x.Total)
            .ThenByDescending(x => x.ProceedingRatePercent)
            .ThenBy(x => x.ReasonCode)
            .Take(topTake)
            .ToList();

        var anomalyAlerts = BuildAnomalyAlerts(disputeCases, fromUtc, toUtc);

        return new AdminDisputeObservabilityDashboardDto(
            fromUtc,
            toUtc,
            disputeCases.Count,
            openCases,
            closedCases,
            resolvedCases,
            rejectedCases,
            slaBreachedOpenCases,
            decisionProceedingRatePercent,
            averageResolutionHours,
            medianResolutionHours,
            casesByType,
            casesByPriority,
            casesByStatus,
            anomalyAlerts,
            topReasons);
    }

    public async Task<AdminDisputeAuditTrailResponseDto> GetAuditTrailAsync(AdminDisputeAuditQueryDto query)
    {
        var normalizedQuery = query ?? new AdminDisputeAuditQueryDto();
        var (fromUtc, toUtc) = NormalizeObservabilityRange(normalizedQuery.FromUtc, normalizedQuery.ToUtc);
        var take = Math.Clamp(normalizedQuery.Take, 1, 2000);
        var actorUserId = normalizedQuery.ActorUserId.HasValue && normalizedQuery.ActorUserId.Value != Guid.Empty
            ? normalizedQuery.ActorUserId
            : null;
        var disputeCaseId = normalizedQuery.DisputeCaseId.HasValue && normalizedQuery.DisputeCaseId.Value != Guid.Empty
            ? normalizedQuery.DisputeCaseId
            : null;
        var normalizedEventType = NormalizeAuditEvent(normalizedQuery.EventType);

        var internalTake = Math.Clamp(take * 3, 10, 10000);
        var disputeAudits = await _serviceDisputeCaseRepository.GetAuditEntriesByPeriodAsync(
            fromUtc,
            toUtc,
            actorUserId,
            disputeCaseId,
            eventType: null,
            take: internalTake);
        var adminAudits = await _adminAuditLogRepository.GetByTargetAndPeriodAsync(
            "ServiceDisputeCase",
            fromUtc,
            toUtc,
            actorUserId,
            disputeCaseId,
            action: null,
            take: internalTake);

        var actorNameLookup = new Dictionary<Guid, string?>();
        var adminAuditActorIds = adminAudits
            .Select(x => x.ActorUserId)
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        foreach (var adminAuditActorId in adminAuditActorIds)
        {
            var actor = await _userRepository.GetByIdAsync(adminAuditActorId);
            actorNameLookup[adminAuditActorId] = actor?.Name;
        }

        var items = disputeAudits
            .Select(audit => new AdminDisputeAuditTrailItemDto(
                audit.CreatedAt,
                Source: "CaseAudit",
                EventType: audit.EventType,
                Message: audit.Message,
                DisputeCaseId: audit.ServiceDisputeCaseId,
                ActorUserId: audit.ActorUserId,
                ActorEmail: audit.ActorUser?.Email,
                ActorName: ResolveUserDisplayName(audit.ActorUserId, audit.ActorUser?.Name),
                ActorRole: audit.ActorRole.ToString(),
                MetadataJson: audit.MetadataJson))
            .Concat(adminAudits.Select(audit => new AdminDisputeAuditTrailItemDto(
                audit.CreatedAt,
                Source: "AdminAudit",
                EventType: audit.Action,
                Message: null,
                DisputeCaseId: audit.TargetId.HasValue && audit.TargetId.Value != Guid.Empty ? audit.TargetId.Value : null,
                ActorUserId: audit.ActorUserId,
                ActorEmail: audit.ActorEmail,
                ActorName: ResolveUserDisplayName(
                    audit.ActorUserId,
                    actorNameLookup.TryGetValue(audit.ActorUserId, out var actorName) ? actorName : null),
                ActorRole: "Admin",
                MetadataJson: audit.Metadata)))
            .Where(item => string.IsNullOrWhiteSpace(normalizedEventType) || NormalizeAuditEvent(item.EventType) == normalizedEventType)
            .OrderByDescending(item => item.OccurredAtUtc)
            .ThenByDescending(item => item.DisputeCaseId)
            .Take(take)
            .ToList();

        return new AdminDisputeAuditTrailResponseDto(
            fromUtc,
            toUtc,
            items.Count,
            items);
    }

    public async Task<AdminDisputeCaseDetailsDto?> GetCaseDetailsAsync(Guid disputeCaseId)
    {
        if (disputeCaseId == Guid.Empty)
        {
            return null;
        }

        var disputeCase = await _serviceDisputeCaseRepository.GetByIdWithDetailsAsync(disputeCaseId);
        if (disputeCase == null)
        {
            return null;
        }

        var nowUtc = DateTime.UtcNow;
        var messages = (disputeCase.Messages ?? Array.Empty<ServiceDisputeCaseMessage>())
            .OrderBy(m => m.CreatedAt)
            .Select(m => new AdminDisputeCaseMessageDto(
                m.Id,
                m.MessageType,
                m.MessageText,
                m.IsInternal,
                m.AuthorUserId,
                m.AuthorRole.ToString(),
                ResolveUserDisplayName(m.AuthorUserId, m.AuthorUser?.Name),
                m.CreatedAt))
            .ToList();

        var attachments = (disputeCase.Attachments ?? Array.Empty<ServiceDisputeCaseAttachment>())
            .OrderBy(a => a.CreatedAt)
            .Select(a => new AdminDisputeCaseAttachmentDto(
                a.Id,
                a.ServiceDisputeCaseMessageId,
                a.FileUrl,
                a.FileName,
                a.ContentType,
                a.MediaKind,
                a.SizeBytes,
                a.UploadedByUserId,
                ResolveUserDisplayName(a.UploadedByUserId, a.UploadedByUser?.Name),
                a.CreatedAt))
            .ToList();

        var audits = (disputeCase.AuditEntries ?? Array.Empty<ServiceDisputeCaseAuditEntry>())
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AdminDisputeCaseAuditEntryDto(
                a.Id,
                a.EventType,
                a.Message,
                a.ActorUserId,
                a.ActorRole.ToString(),
                ResolveUserDisplayName(a.ActorUserId, a.ActorUser?.Name),
                a.MetadataJson,
                a.CreatedAt))
            .ToList();

        return new AdminDisputeCaseDetailsDto(
            disputeCase.Id,
            disputeCase.ServiceRequestId,
            disputeCase.ServiceAppointmentId,
            disputeCase.Type.ToString(),
            disputeCase.Priority.ToString(),
            disputeCase.Status.ToString(),
            disputeCase.ReasonCode,
            disputeCase.Description,
            ResolveUserDisplayName(disputeCase.OpenedByUserId, disputeCase.OpenedByUser?.Name),
            disputeCase.OpenedByRole.ToString(),
            ResolveUserDisplayName(disputeCase.CounterpartyUserId, disputeCase.CounterpartyUser?.Name),
            disputeCase.CounterpartyRole.ToString(),
            disputeCase.OwnedByAdminUserId,
            ResolveUserDisplayName(disputeCase.OwnedByAdminUserId, disputeCase.OwnedByAdminUser?.Name),
            disputeCase.OwnedAtUtc,
            disputeCase.WaitingForRole?.ToString(),
            disputeCase.OpenedAtUtc,
            disputeCase.SlaDueAtUtc,
            disputeCase.LastInteractionAtUtc,
            disputeCase.ClosedAtUtc,
            disputeCase.SlaDueAtUtc < nowUtc,
            disputeCase.ResolutionSummary,
            string.IsNullOrWhiteSpace(disputeCase.ServiceRequest?.AddressCity) ? null : disputeCase.ServiceRequest.AddressCity,
            disputeCase.ServiceRequest?.Category.ToString(),
            messages,
            attachments,
            audits);
    }

    public async Task<AdminDisputeOperationResultDto> UpdateWorkflowAsync(
        Guid disputeCaseId,
        Guid actorUserId,
        string actorEmail,
        AdminUpdateDisputeWorkflowRequestDto request)
    {
        if (disputeCaseId == Guid.Empty)
        {
            return new AdminDisputeOperationResultDto(false, ErrorCode: "invalid_dispute", ErrorMessage: "Disputa invalida.");
        }

        var actor = await _userRepository.GetByIdAsync(actorUserId);
        if (actor == null || actor.Role != UserRole.Admin)
        {
            return new AdminDisputeOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Usuario sem permissao administrativa.");
        }

        if (!TryParseStatus(request.Status, out var targetStatus))
        {
            return new AdminDisputeOperationResultDto(false, ErrorCode: "invalid_status", ErrorMessage: "Status de workflow invalido.");
        }

        if (targetStatus is DisputeCaseStatus.Resolved or DisputeCaseStatus.Rejected)
        {
            return new AdminDisputeOperationResultDto(
                false,
                ErrorCode: "decision_required",
                ErrorMessage: "Use o fluxo de decisao para resolver ou rejeitar a disputa.");
        }

        var disputeCase = await _serviceDisputeCaseRepository.GetByIdWithDetailsAsync(disputeCaseId);
        if (disputeCase == null)
        {
            return new AdminDisputeOperationResultDto(false, ErrorCode: "not_found", ErrorMessage: "Disputa nao encontrada.");
        }

        if (IsClosedStatus(disputeCase.Status))
        {
            return new AdminDisputeOperationResultDto(
                false,
                ErrorCode: "dispute_closed",
                ErrorMessage: "Disputa encerrada. Workflow bloqueado para edicao.");
        }

        if (!IsAllowedWorkflowTransition(disputeCase.Status, targetStatus))
        {
            return new AdminDisputeOperationResultDto(
                false,
                ErrorCode: "invalid_transition",
                ErrorMessage: "Transicao de status nao permitida no workflow da disputa.");
        }

        ServiceAppointmentActorRole? waitingForRole = null;
        if (targetStatus == DisputeCaseStatus.WaitingParties)
        {
            if (!TryParseWaitingForRole(request.WaitingForRole, out waitingForRole))
            {
                return new AdminDisputeOperationResultDto(
                    false,
                    ErrorCode: "invalid_waiting_role",
                    ErrorMessage: "Informe WaitingForRole como 'Client' ou 'Provider' para status WaitingParties.");
            }
        }

        var nowUtc = DateTime.UtcNow;
        if (request.ClaimOwnership || disputeCase.OwnedByAdminUserId == null)
        {
            disputeCase.OwnedByAdminUserId = actorUserId;
            disputeCase.OwnedAtUtc = nowUtc;
        }

        var previousStatus = disputeCase.Status;
        disputeCase.Status = targetStatus;
        disputeCase.WaitingForRole = targetStatus == DisputeCaseStatus.WaitingParties
            ? waitingForRole
            : null;
        disputeCase.LastInteractionAtUtc = nowUtc;
        disputeCase.UpdatedAt = nowUtc;
        await _serviceDisputeCaseRepository.UpdateAsync(disputeCase);

        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        await _serviceDisputeCaseRepository.AddAuditEntryAsync(new ServiceDisputeCaseAuditEntry
        {
            ServiceDisputeCaseId = disputeCase.Id,
            ActorUserId = actorUserId,
            ActorRole = ServiceAppointmentActorRole.Admin,
            EventType = "dispute_workflow_updated",
            Message = $"Workflow atualizado de {previousStatus} para {targetStatus}.",
            MetadataJson = JsonSerializer.Serialize(new
            {
                from = previousStatus.ToString(),
                to = targetStatus.ToString(),
                waitingForRole = disputeCase.WaitingForRole?.ToString(),
                note
            })
        });

        await _adminAuditLogRepository.AddAsync(new AdminAuditLog
        {
            ActorUserId = actorUserId,
            ActorEmail = string.IsNullOrWhiteSpace(actorEmail) ? "admin@consertapramim.local" : actorEmail.Trim(),
            Action = "DisputeWorkflowUpdated",
            TargetType = "ServiceDisputeCase",
            TargetId = disputeCase.Id,
            Metadata = JsonSerializer.Serialize(new
            {
                from = previousStatus.ToString(),
                to = targetStatus.ToString(),
                waitingForRole = disputeCase.WaitingForRole?.ToString(),
                note,
                claimOwnership = request.ClaimOwnership
            })
        });

        var details = await GetCaseDetailsAsync(disputeCaseId);
        return new AdminDisputeOperationResultDto(true, Case: details);
    }

    public async Task<AdminDisputeOperationResultDto> RegisterDecisionAsync(
        Guid disputeCaseId,
        Guid actorUserId,
        string actorEmail,
        AdminRegisterDisputeDecisionRequestDto request)
    {
        if (disputeCaseId == Guid.Empty)
        {
            return new AdminDisputeOperationResultDto(false, ErrorCode: "invalid_dispute", ErrorMessage: "Disputa invalida.");
        }

        var actor = await _userRepository.GetByIdAsync(actorUserId);
        if (actor == null || actor.Role != UserRole.Admin)
        {
            return new AdminDisputeOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Usuario sem permissao administrativa.");
        }

        if (!TryParseDecisionOutcome(request.Outcome, out var normalizedOutcome, out var resolutionStatus))
        {
            return new AdminDisputeOperationResultDto(
                false,
                ErrorCode: "invalid_outcome",
                ErrorMessage: "Outcome invalido. Use: procedente, improcedente ou parcial.");
        }

        var justification = request.Justification?.Trim();
        if (string.IsNullOrWhiteSpace(justification) || justification.Length > 3000)
        {
            return new AdminDisputeOperationResultDto(
                false,
                ErrorCode: "invalid_justification",
                ErrorMessage: "Justificativa obrigatoria e deve ter ate 3000 caracteres.");
        }

        var resolutionSummary = string.IsNullOrWhiteSpace(request.ResolutionSummary)
            ? justification
            : request.ResolutionSummary.Trim();
        if (resolutionSummary.Length > 3000)
        {
            return new AdminDisputeOperationResultDto(
                false,
                ErrorCode: "invalid_resolution_summary",
                ErrorMessage: "Resumo da resolucao deve ter ate 3000 caracteres.");
        }

        var disputeCase = await _serviceDisputeCaseRepository.GetByIdWithDetailsAsync(disputeCaseId);
        if (disputeCase == null)
        {
            return new AdminDisputeOperationResultDto(false, ErrorCode: "not_found", ErrorMessage: "Disputa nao encontrada.");
        }

        if (IsClosedStatus(disputeCase.Status))
        {
            return new AdminDisputeOperationResultDto(
                false,
                ErrorCode: "dispute_closed",
                ErrorMessage: "Disputa encerrada. Nao e possivel registrar nova decisao.");
        }

        if (!TryParseFinancialDecision(request.FinancialDecision, out var financialDecision, out var financialValidationError))
        {
            return new AdminDisputeOperationResultDto(
                false,
                ErrorCode: "invalid_financial_decision",
                ErrorMessage: financialValidationError ?? "Impacto financeiro invalido para a decisao.");
        }

        var nowUtc = DateTime.UtcNow;
        var financialExecution = await ExecuteFinancialDecisionAsync(
            disputeCase,
            actorUserId,
            actorEmail,
            normalizedOutcome,
            financialDecision,
            nowUtc);

        if (!financialExecution.Success)
        {
            return new AdminDisputeOperationResultDto(
                false,
                ErrorCode: financialExecution.ErrorCode,
                ErrorMessage: financialExecution.ErrorMessage);
        }

        var decisionMetadata = new
        {
            outcome = normalizedOutcome,
            status = resolutionStatus.ToString(),
            justification,
            financial = financialExecution.Metadata
        };

        disputeCase.OwnedByAdminUserId ??= actorUserId;
        disputeCase.OwnedAtUtc ??= nowUtc;
        disputeCase.Status = resolutionStatus;
        disputeCase.WaitingForRole = null;
        disputeCase.ResolutionSummary = resolutionSummary;
        disputeCase.MetadataJson = JsonSerializer.Serialize(decisionMetadata);
        disputeCase.ClosedAtUtc = nowUtc;
        disputeCase.LastInteractionAtUtc = nowUtc;
        disputeCase.UpdatedAt = nowUtc;
        await _serviceDisputeCaseRepository.UpdateAsync(disputeCase);

        await _serviceDisputeCaseRepository.AddAuditEntryAsync(new ServiceDisputeCaseAuditEntry
        {
            ServiceDisputeCaseId = disputeCase.Id,
            ActorUserId = actorUserId,
            ActorRole = ServiceAppointmentActorRole.Admin,
            EventType = "dispute_decision_recorded",
            Message = $"Decisao registrada ({normalizedOutcome}).",
            MetadataJson = JsonSerializer.Serialize(decisionMetadata)
        });

        await _adminAuditLogRepository.AddAsync(new AdminAuditLog
        {
            ActorUserId = actorUserId,
            ActorEmail = string.IsNullOrWhiteSpace(actorEmail) ? "admin@consertapramim.local" : actorEmail.Trim(),
            Action = "DisputeDecisionRecorded",
            TargetType = "ServiceDisputeCase",
            TargetId = disputeCase.Id,
            Metadata = JsonSerializer.Serialize(decisionMetadata)
        });

        await NotifyDecisionToPartiesAsync(disputeCase, normalizedOutcome, resolutionSummary, actorUserId);

        var details = await GetCaseDetailsAsync(disputeCase.Id);
        return new AdminDisputeOperationResultDto(true, Case: details);
    }

    public async Task RecordCaseAccessAsync(
        Guid disputeCaseId,
        Guid actorUserId,
        string actorEmail,
        string source)
    {
        if (disputeCaseId == Guid.Empty || actorUserId == Guid.Empty)
        {
            return;
        }

        var actor = await _userRepository.GetByIdAsync(actorUserId);
        if (actor == null || actor.Role != UserRole.Admin)
        {
            return;
        }

        var disputeCase = await _serviceDisputeCaseRepository.GetByIdAsync(disputeCaseId);
        if (disputeCase == null)
        {
            return;
        }

        var safeSource = string.IsNullOrWhiteSpace(source) ? "unknown" : source.Trim();
        await _serviceDisputeCaseRepository.AddAuditEntryAsync(new ServiceDisputeCaseAuditEntry
        {
            ServiceDisputeCaseId = disputeCase.Id,
            ActorUserId = actorUserId,
            ActorRole = ServiceAppointmentActorRole.Admin,
            EventType = "dispute_case_viewed",
            Message = "Caso visualizado no painel administrativo.",
            MetadataJson = JsonSerializer.Serialize(new
            {
                source = safeSource
            })
        });

        await _adminAuditLogRepository.AddAsync(new AdminAuditLog
        {
            ActorUserId = actorUserId,
            ActorEmail = string.IsNullOrWhiteSpace(actorEmail) ? "admin@consertapramim.local" : actorEmail.Trim(),
            Action = "DisputeCaseViewed",
            TargetType = "ServiceDisputeCase",
            TargetId = disputeCase.Id,
            Metadata = JsonSerializer.Serialize(new
            {
                source = safeSource
            })
        });
    }

    public async Task<AdminDisputeRetentionRunResultDto> RunRetentionAsync(
        Guid actorUserId,
        string actorEmail,
        AdminDisputeRetentionRunRequestDto request)
    {
        var normalizedRequest = request ?? new AdminDisputeRetentionRunRequestDto();
        var retentionDays = Math.Clamp(normalizedRequest.RetentionDays, 30, 3650);
        var take = Math.Clamp(normalizedRequest.Take, 1, 5000);
        var cutoffUtc = DateTime.UtcNow.AddDays(-retentionDays);
        var dryRun = normalizedRequest.DryRun;

        var actor = await _userRepository.GetByIdAsync(actorUserId);
        if (actor == null || actor.Role != UserRole.Admin)
        {
            return new AdminDisputeRetentionRunResultDto(
                retentionDays,
                cutoffUtc,
                Candidates: 0,
                AnonymizedCases: 0,
                AnonymizedMessages: 0,
                AnonymizedAttachments: 0,
                dryRun);
        }

        var candidates = (await _serviceDisputeCaseRepository.GetClosedCasesClosedBeforeAsync(cutoffUtc, take))
            .Where(c => !IsCaseAlreadyAnonymized(c.MetadataJson))
            .ToList();

        if (dryRun)
        {
            return new AdminDisputeRetentionRunResultDto(
                retentionDays,
                cutoffUtc,
                candidates.Count,
                AnonymizedCases: 0,
                AnonymizedMessages: 0,
                AnonymizedAttachments: 0,
                dryRun);
        }

        var nowUtc = DateTime.UtcNow;
        var anonymizedCases = 0;
        var anonymizedMessages = 0;
        var anonymizedAttachments = 0;

        foreach (var disputeCase in candidates)
        {
            var outcome = TryReadOutcome(disputeCase.MetadataJson, out var parsedOutcome)
                ? parsedOutcome
                : null;

            disputeCase.Description = LgpdTextPlaceholder;
            disputeCase.ResolutionSummary = string.IsNullOrWhiteSpace(disputeCase.ResolutionSummary)
                ? null
                : LgpdTextPlaceholder;
            disputeCase.MetadataJson = JsonSerializer.Serialize(new
            {
                outcome,
                lgpd = new
                {
                    anonymized = true,
                    anonymizedAtUtc = nowUtc,
                    retentionDays
                }
            });
            disputeCase.UpdatedAt = nowUtc;
            await _serviceDisputeCaseRepository.UpdateAsync(disputeCase);
            anonymizedCases++;

            foreach (var message in disputeCase.Messages)
            {
                if (string.Equals(message.MessageText, LgpdTextPlaceholder, StringComparison.Ordinal))
                {
                    continue;
                }

                message.MessageText = LgpdTextPlaceholder;
                message.UpdatedAt = nowUtc;
                await _serviceDisputeCaseRepository.UpdateMessageAsync(message);
                anonymizedMessages++;
            }

            foreach (var attachment in disputeCase.Attachments)
            {
                if (string.Equals(attachment.FileUrl, LgpdFileUrlPlaceholder, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                attachment.FileName = BuildAnonymizedFileName(attachment.FileName);
                attachment.FileUrl = LgpdFileUrlPlaceholder;
                attachment.UpdatedAt = nowUtc;
                await _serviceDisputeCaseRepository.UpdateAttachmentAsync(attachment);
                anonymizedAttachments++;
            }

            await _serviceDisputeCaseRepository.AddAuditEntryAsync(new ServiceDisputeCaseAuditEntry
            {
                ServiceDisputeCaseId = disputeCase.Id,
                ActorUserId = actorUserId,
                ActorRole = ServiceAppointmentActorRole.Admin,
                EventType = "dispute_lgpd_anonymized",
                Message = "Dados da disputa anonimizados por politica de retencao LGPD.",
                MetadataJson = JsonSerializer.Serialize(new
                {
                    retentionDays,
                    anonymizedMessages,
                    anonymizedAttachments
                })
            });
        }

        await _adminAuditLogRepository.AddAsync(new AdminAuditLog
        {
            ActorUserId = actorUserId,
            ActorEmail = string.IsNullOrWhiteSpace(actorEmail) ? "admin@consertapramim.local" : actorEmail.Trim(),
            Action = "DisputeLgpdRetentionRun",
            TargetType = "ServiceDisputeCase",
            TargetId = Guid.Empty,
            Metadata = JsonSerializer.Serialize(new
            {
                retentionDays,
                cutoffUtc,
                candidates = candidates.Count,
                anonymizedCases,
                anonymizedMessages,
                anonymizedAttachments
            })
        });

        return new AdminDisputeRetentionRunResultDto(
            retentionDays,
            cutoffUtc,
            candidates.Count,
            anonymizedCases,
            anonymizedMessages,
            anonymizedAttachments,
            dryRun);
    }

    private static bool IsOpenStatus(DisputeCaseStatus status)
    {
        return status is DisputeCaseStatus.Open or DisputeCaseStatus.UnderReview or DisputeCaseStatus.WaitingParties;
    }

    private static bool IsClosedStatus(DisputeCaseStatus status)
    {
        return status is DisputeCaseStatus.Resolved or DisputeCaseStatus.Rejected or DisputeCaseStatus.Cancelled;
    }

    private static string NormalizeAuditEvent(string? rawEvent)
    {
        if (string.IsNullOrWhiteSpace(rawEvent))
        {
            return string.Empty;
        }

        var value = rawEvent.Trim().ToLowerInvariant();
        value = value.Replace("_", string.Empty, StringComparison.Ordinal);
        value = value.Replace("-", string.Empty, StringComparison.Ordinal);
        value = value.Replace(" ", string.Empty, StringComparison.Ordinal);
        return value;
    }

    private static (DateTime FromUtc, DateTime ToUtc) NormalizeObservabilityRange(DateTime? fromUtc, DateTime? toUtc)
    {
        var end = (toUtc ?? DateTime.UtcNow).ToUniversalTime();
        var start = (fromUtc ?? end.AddDays(-30)).ToUniversalTime();
        if (start > end)
        {
            (start, end) = (end, start);
        }

        return (start, end);
    }

    private static bool TryParseStatus(string? rawStatus, out DisputeCaseStatus status)
    {
        status = default;
        if (string.IsNullOrWhiteSpace(rawStatus))
        {
            return false;
        }

        return Enum.TryParse(rawStatus.Trim(), true, out status);
    }

    private static bool TryParseType(string? rawType, out DisputeCaseType type)
    {
        type = default;
        if (string.IsNullOrWhiteSpace(rawType))
        {
            return false;
        }

        return Enum.TryParse(rawType.Trim(), true, out type);
    }

    private static bool TryParseDecisionOutcome(
        string? rawOutcome,
        out string normalizedOutcome,
        out DisputeCaseStatus resolvedStatus)
    {
        normalizedOutcome = string.Empty;
        resolvedStatus = DisputeCaseStatus.Resolved;
        if (string.IsNullOrWhiteSpace(rawOutcome))
        {
            return false;
        }

        normalizedOutcome = rawOutcome.Trim().ToLowerInvariant();
        switch (normalizedOutcome)
        {
            case "procedente":
                resolvedStatus = DisputeCaseStatus.Resolved;
                return true;
            case "parcial":
                resolvedStatus = DisputeCaseStatus.Resolved;
                return true;
            case "improcedente":
                resolvedStatus = DisputeCaseStatus.Rejected;
                return true;
            default:
                return false;
        }
    }

    private async Task<FinancialDecisionExecutionResult> ExecuteFinancialDecisionAsync(
        ServiceDisputeCase disputeCase,
        Guid actorUserId,
        string actorEmail,
        string normalizedOutcome,
        AdminDisputeFinancialDecisionRequestDto? financialDecision,
        DateTime nowUtc)
    {
        if (financialDecision == null || financialDecision.Action == "none")
        {
            return FinancialDecisionExecutionResult.SuccessResult(new
            {
                action = "none"
            });
        }

        var providerId = disputeCase.ServiceAppointment?.ProviderId ?? Guid.Empty;
        if (providerId == Guid.Empty)
        {
            return FinancialDecisionExecutionResult.FailResult(
                "provider_not_found",
                "Nao foi possivel identificar o prestador para aplicar o impacto financeiro.");
        }

        var amount = decimal.Round(financialDecision.Amount ?? 0m, 2, MidpointRounding.AwayFromZero);
        var reason = string.IsNullOrWhiteSpace(financialDecision.Reason)
            ? $"Decisao de disputa ({normalizedOutcome})"
            : financialDecision.Reason.Trim();

        switch (financialDecision.Action)
        {
            case "refund_client":
            {
                var paidTransactions = await _servicePaymentTransactionRepository.GetByServiceRequestIdAsync(
                    disputeCase.ServiceRequestId,
                    PaymentTransactionStatus.Paid);
                var targetTransaction = paidTransactions
                    .Where(t => t.ProviderId == providerId)
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstOrDefault();

                if (targetTransaction == null)
                {
                    return FinancialDecisionExecutionResult.FailResult(
                        "payment_not_found",
                        "Nao existe transacao paga para reembolso neste caso.");
                }

                var normalizedRefundAmount = amount > 0m ? amount : targetTransaction.Amount;
                if (normalizedRefundAmount <= 0m)
                {
                    return FinancialDecisionExecutionResult.FailResult(
                        "invalid_refund_amount",
                        "Valor de reembolso invalido.");
                }

                if (normalizedRefundAmount > targetTransaction.Amount)
                {
                    return FinancialDecisionExecutionResult.FailResult(
                        "refund_amount_exceeds_paid",
                        "Valor de reembolso nao pode exceder o valor pago.");
                }

                var refunded = await _paymentService.RefundAsync(
                    new PaymentRefundRequestDto(
                        targetTransaction.ProviderTransactionId,
                        normalizedRefundAmount,
                        reason,
                        $"dispute-{disputeCase.Id:N}-{normalizedRefundAmount:F2}"));

                if (!refunded)
                {
                    return FinancialDecisionExecutionResult.FailResult(
                        "refund_failed",
                        "Falha ao solicitar reembolso no provedor de pagamento.");
                }

                targetTransaction.Status = PaymentTransactionStatus.Refunded;
                targetTransaction.RefundedAtUtc = nowUtc;
                targetTransaction.ProcessedAtUtc ??= nowUtc;
                targetTransaction.UpdatedAt = nowUtc;
                await _servicePaymentTransactionRepository.UpdateAsync(targetTransaction);

                await _serviceDisputeCaseRepository.AddAuditEntryAsync(new ServiceDisputeCaseAuditEntry
                {
                    ServiceDisputeCaseId = disputeCase.Id,
                    ActorUserId = actorUserId,
                    ActorRole = ServiceAppointmentActorRole.Admin,
                    EventType = "dispute_financial_refund",
                    Message = $"Reembolso aplicado ao cliente no valor de {normalizedRefundAmount:N2}.",
                    MetadataJson = JsonSerializer.Serialize(new
                    {
                        providerTransactionId = targetTransaction.ProviderTransactionId,
                        amount = normalizedRefundAmount,
                        reason
                    })
                });

                return FinancialDecisionExecutionResult.SuccessResult(new
                {
                    action = financialDecision.Action,
                    amount = normalizedRefundAmount,
                    reason,
                    transactionId = targetTransaction.Id,
                    providerTransactionId = targetTransaction.ProviderTransactionId
                });
            }
            case "credit_provider":
            case "debit_provider":
            {
                if (amount <= 0m)
                {
                    return FinancialDecisionExecutionResult.FailResult(
                        "invalid_financial_amount",
                        "Informe um valor financeiro maior que zero.");
                }

                var entryType = financialDecision.Action == "credit_provider"
                    ? ProviderCreditLedgerEntryType.Grant
                    : ProviderCreditLedgerEntryType.Debit;

                var mutation = await _providerCreditService.ApplyMutationAsync(
                    new ProviderCreditMutationRequestDto(
                        providerId,
                        entryType,
                        amount,
                        reason,
                        Source: "dispute_decision",
                        ReferenceType: "ServiceDisputeCase",
                        ReferenceId: disputeCase.Id,
                        Metadata: JsonSerializer.Serialize(new
                        {
                            disputeCaseId = disputeCase.Id,
                            outcome = normalizedOutcome,
                            action = financialDecision.Action
                        })),
                    actorUserId,
                    actorEmail);

                if (!mutation.Success)
                {
                    return FinancialDecisionExecutionResult.FailResult(
                        mutation.ErrorCode ?? "ledger_mutation_failed",
                        mutation.ErrorMessage ?? "Falha ao aplicar impacto financeiro no ledger.");
                }

                await _serviceDisputeCaseRepository.AddAuditEntryAsync(new ServiceDisputeCaseAuditEntry
                {
                    ServiceDisputeCaseId = disputeCase.Id,
                    ActorUserId = actorUserId,
                    ActorRole = ServiceAppointmentActorRole.Admin,
                    EventType = financialDecision.Action == "credit_provider"
                        ? "dispute_financial_credit_provider"
                        : "dispute_financial_debit_provider",
                    Message = $"Lancamento financeiro para prestador no valor de {amount:N2}.",
                    MetadataJson = JsonSerializer.Serialize(new
                    {
                        action = financialDecision.Action,
                        amount,
                        reason,
                        providerId,
                        balanceAfter = mutation.Balance?.CurrentBalance,
                        ledgerEntryId = mutation.Entry?.EntryId
                    })
                });

                return FinancialDecisionExecutionResult.SuccessResult(new
                {
                    action = financialDecision.Action,
                    amount,
                    reason,
                    providerId,
                    ledgerEntryId = mutation.Entry?.EntryId,
                    balanceAfter = mutation.Balance?.CurrentBalance
                });
            }
            default:
                return FinancialDecisionExecutionResult.FailResult(
                    "invalid_financial_action",
                "Acao financeira invalida para decisao da disputa.");
        }
    }

    private async Task NotifyDecisionToPartiesAsync(
        ServiceDisputeCase disputeCase,
        string outcome,
        string resolutionSummary,
        Guid actorUserId)
    {
        var recipientIds = new[] { disputeCase.OpenedByUserId, disputeCase.CounterpartyUserId }
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (recipientIds.Count == 0)
        {
            return;
        }

        var subject = "Decisao da disputa registrada";
        var message = $"A mediacao da disputa #{disputeCase.Id.ToString("N")[..8]} foi concluida com resultado '{outcome}'. Resumo: {resolutionSummary}";
        var actionUrl = $"/ServiceRequests/Details/{disputeCase.ServiceRequestId:D}?disputeCaseId={disputeCase.Id:D}";
        var failures = new List<object>();

        foreach (var recipientUserId in recipientIds)
        {
            try
            {
                await _notificationService.SendNotificationAsync(
                    recipientUserId.ToString("D"),
                    subject,
                    message,
                    actionUrl);
            }
            catch (Exception ex)
            {
                failures.Add(new
                {
                    recipientUserId,
                    error = ex.Message
                });
            }
        }

        if (failures.Count == 0)
        {
            await _serviceDisputeCaseRepository.AddAuditEntryAsync(new ServiceDisputeCaseAuditEntry
            {
                ServiceDisputeCaseId = disputeCase.Id,
                ActorUserId = actorUserId,
                ActorRole = ServiceAppointmentActorRole.Admin,
                EventType = "dispute_decision_notified",
                Message = "Notificacao de decisao enviada para as partes.",
                MetadataJson = JsonSerializer.Serialize(new
                {
                    recipients = recipientIds
                })
            });
            return;
        }

        await _serviceDisputeCaseRepository.AddAuditEntryAsync(new ServiceDisputeCaseAuditEntry
        {
            ServiceDisputeCaseId = disputeCase.Id,
            ActorUserId = actorUserId,
            ActorRole = ServiceAppointmentActorRole.Admin,
            EventType = "dispute_decision_notification_failed",
            Message = "Falha parcial no envio das notificacoes da decisao.",
            MetadataJson = JsonSerializer.Serialize(new
            {
                recipients = recipientIds,
                failures
            })
        });
    }

    private static bool TryParseFinancialDecision(
        AdminDisputeFinancialDecisionRequestDto? request,
        out AdminDisputeFinancialDecisionRequestDto? normalizedRequest,
        out string? validationError)
    {
        normalizedRequest = null;
        validationError = null;

        if (request == null || string.IsNullOrWhiteSpace(request.Action))
        {
            return true;
        }

        var normalizedAction = request.Action.Trim().ToLowerInvariant();
        if (normalizedAction == "none")
        {
            return true;
        }

        if (normalizedAction is not ("refund_client" or "credit_provider" or "debit_provider"))
        {
            validationError = "Acao financeira invalida. Use none, refund_client, credit_provider ou debit_provider.";
            return false;
        }

        var normalizedAmount = request.Amount.HasValue
            ? decimal.Round(request.Amount.Value, 2, MidpointRounding.AwayFromZero)
            : (decimal?)null;
        if (normalizedAmount.HasValue && normalizedAmount.Value <= 0m)
        {
            validationError = "Valor financeiro deve ser maior que zero.";
            return false;
        }

        var normalizedReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedReason) && normalizedReason.Length > 500)
        {
            validationError = "Motivo financeiro deve ter no maximo 500 caracteres.";
            return false;
        }

        normalizedRequest = new AdminDisputeFinancialDecisionRequestDto(
            normalizedAction,
            normalizedAmount,
            normalizedReason);
        return true;
    }

    private static bool TryParseWaitingForRole(string? rawRole, out ServiceAppointmentActorRole? role)
    {
        role = null;
        if (string.IsNullOrWhiteSpace(rawRole))
        {
            return false;
        }

        if (!Enum.TryParse<ServiceAppointmentActorRole>(rawRole.Trim(), true, out var parsed))
        {
            return false;
        }

        if (parsed is not (ServiceAppointmentActorRole.Client or ServiceAppointmentActorRole.Provider))
        {
            return false;
        }

        role = parsed;
        return true;
    }

    private static bool IsAllowedWorkflowTransition(DisputeCaseStatus currentStatus, DisputeCaseStatus targetStatus)
    {
        if (currentStatus == targetStatus)
        {
            return true;
        }

        return currentStatus switch
        {
            DisputeCaseStatus.Open => targetStatus is DisputeCaseStatus.UnderReview or DisputeCaseStatus.WaitingParties or DisputeCaseStatus.Cancelled,
            DisputeCaseStatus.UnderReview => targetStatus is DisputeCaseStatus.Open or DisputeCaseStatus.WaitingParties or DisputeCaseStatus.Cancelled,
            DisputeCaseStatus.WaitingParties => targetStatus is DisputeCaseStatus.UnderReview or DisputeCaseStatus.Cancelled,
            _ => false
        };
    }

    private static AdminDisputeQueueItemDto MapToQueueItem(ServiceDisputeCase disputeCase)
    {
        var nowUtc = DateTime.UtcNow;
        var openedByName = !string.IsNullOrWhiteSpace(disputeCase.OpenedByUser?.Name)
            ? disputeCase.OpenedByUser.Name
            : $"Usuario {disputeCase.OpenedByUserId.ToString()[..8]}";

        var counterpartyName = !string.IsNullOrWhiteSpace(disputeCase.CounterpartyUser?.Name)
            ? disputeCase.CounterpartyUser.Name
            : $"Usuario {disputeCase.CounterpartyUserId.ToString()[..8]}";

        return new AdminDisputeQueueItemDto(
            disputeCase.Id,
            disputeCase.ServiceRequestId,
            disputeCase.ServiceAppointmentId,
            disputeCase.Type.ToString(),
            disputeCase.Priority.ToString(),
            disputeCase.Status.ToString(),
            disputeCase.ReasonCode,
            disputeCase.Description,
            openedByName,
            disputeCase.OpenedByRole.ToString(),
            counterpartyName,
            disputeCase.CounterpartyRole.ToString(),
            disputeCase.OpenedAtUtc,
            disputeCase.SlaDueAtUtc,
            disputeCase.LastInteractionAtUtc,
            disputeCase.SlaDueAtUtc < nowUtc,
            string.IsNullOrWhiteSpace(disputeCase.ServiceRequest?.AddressCity) ? null : disputeCase.ServiceRequest.AddressCity,
            disputeCase.ServiceRequest?.Category.ToString(),
            disputeCase.Attachments?.Count ?? 0,
            disputeCase.Messages?.Count ?? 0,
            disputeCase.OwnedByAdminUserId,
            ResolveUserDisplayName(disputeCase.OwnedByAdminUserId, disputeCase.OwnedByAdminUser?.Name),
            $"/AdminServiceRequests/Details/{disputeCase.ServiceRequestId:D}?disputeCaseId={disputeCase.Id:D}");
    }

    private static string ResolveUserDisplayName(Guid? userId, string? userName)
    {
        if (!string.IsNullOrWhiteSpace(userName))
        {
            return userName;
        }

        return userId.HasValue
            ? $"Usuario {userId.Value.ToString("N")[..8]}"
            : "Sistema";
    }

    private static bool IsProceedingOutcome(DisputeCaseStatus status, string? metadataJson)
    {
        if (status == DisputeCaseStatus.Rejected)
        {
            return false;
        }

        if (status == DisputeCaseStatus.Resolved && !TryReadOutcome(metadataJson, out var outcome))
        {
            return true;
        }

        if (!TryReadOutcome(metadataJson, out outcome))
        {
            return false;
        }

        return outcome is "procedente" or "parcial";
    }

    private static bool TryReadOutcome(string? metadataJson, out string outcome)
    {
        outcome = string.Empty;
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            if (!document.RootElement.TryGetProperty("outcome", out var outcomeElement))
            {
                return false;
            }

            var value = outcomeElement.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            outcome = value.Trim().ToLowerInvariant();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static decimal CalculateMedian(IReadOnlyList<decimal> sortedValues)
    {
        if (sortedValues.Count == 0)
        {
            return 0m;
        }

        var middle = sortedValues.Count / 2;
        if (sortedValues.Count % 2 == 1)
        {
            return decimal.Round(sortedValues[middle], 2, MidpointRounding.AwayFromZero);
        }

        var median = (sortedValues[middle - 1] + sortedValues[middle]) / 2m;
        return decimal.Round(median, 2, MidpointRounding.AwayFromZero);
    }

    private static IReadOnlyList<AdminDisputeAnomalyAlertDto> BuildAnomalyAlerts(
        IReadOnlyList<ServiceDisputeCase> disputeCases,
        DateTime fromUtc,
        DateTime toUtc)
    {
        if (disputeCases.Count == 0)
        {
            return Array.Empty<AdminDisputeAnomalyAlertDto>();
        }

        var windowDays = Math.Max(1, (int)Math.Ceiling((toUtc - fromUtc).TotalDays));
        var alerts = new List<AdminDisputeAnomalyAlertDto>();
        const decimal frequencyThreshold = 5m;
        const decimal recurrenceThreshold = 3m;
        const decimal rejectedRateThreshold = 60m;
        const decimal minimumRejectedRateSample = 4m;

        var groupedByUser = disputeCases
            .GroupBy(c => c.OpenedByUserId)
            .ToList();

        foreach (var group in groupedByUser)
        {
            if (group.Key == Guid.Empty)
            {
                continue;
            }

            var totalByUser = group.Count();
            var recentDisputeIds = group
                .OrderByDescending(c => c.OpenedAtUtc)
                .Take(5)
                .Select(c => c.Id)
                .ToList();
            var sampleCase = group.First();
            var userName = ResolveUserDisplayName(group.Key, sampleCase.OpenedByUser?.Name);
            var userRole = sampleCase.OpenedByRole.ToString();

            if (totalByUser >= (int)frequencyThreshold)
            {
                var severity = totalByUser >= 8 ? "Critical" : "Warning";
                alerts.Add(new AdminDisputeAnomalyAlertDto(
                    "HIGH_DISPUTE_FREQUENCY",
                    severity,
                    group.Key,
                    userName,
                    userRole,
                    totalByUser,
                    frequencyThreshold,
                    windowDays,
                    $"Usuario abriu {totalByUser} disputas no periodo de {windowDays} dia(s).",
                    "Revisar historico de atendimento e validar possivel uso abusivo do fluxo de disputa.",
                    recentDisputeIds));
            }

            var rejectedCount = group.Count(c => c.Status == DisputeCaseStatus.Rejected);
            if (totalByUser >= (int)minimumRejectedRateSample)
            {
                var rejectedRate = decimal.Round((decimal)rejectedCount * 100m / totalByUser, 2, MidpointRounding.AwayFromZero);
                if (rejectedRate >= rejectedRateThreshold)
                {
                    alerts.Add(new AdminDisputeAnomalyAlertDto(
                        "HIGH_REJECTED_RATE",
                        rejectedRate >= 80m ? "Critical" : "Warning",
                        group.Key,
                        userName,
                        userRole,
                        rejectedRate,
                        rejectedRateThreshold,
                        windowDays,
                        $"Taxa de improcedencia de {rejectedRate:N2}% em {totalByUser} disputa(s).",
                        "Aplicar revisao de abertura de disputa com checklist orientado e, se necessario, escalonar para compliance.",
                        recentDisputeIds));
                }
            }
        }

        var recurringReasonAlerts = disputeCases
            .Where(c => c.OpenedByUserId != Guid.Empty)
            .GroupBy(c => new
            {
                c.OpenedByUserId,
                ReasonCode = string.IsNullOrWhiteSpace(c.ReasonCode) ? "UNSPECIFIED" : c.ReasonCode.Trim().ToUpperInvariant()
            })
            .Where(g => g.Count() >= (int)recurrenceThreshold)
            .Select(g =>
            {
                var first = g.First();
                var ids = g
                    .OrderByDescending(c => c.OpenedAtUtc)
                    .Take(5)
                    .Select(c => c.Id)
                    .ToList();
                var userName = ResolveUserDisplayName(g.Key.OpenedByUserId, first.OpenedByUser?.Name);
                var count = g.Count();
                return new AdminDisputeAnomalyAlertDto(
                    "REPEAT_REASON_PATTERN",
                    count >= 5 ? "Critical" : "Warning",
                    g.Key.OpenedByUserId,
                    userName,
                    first.OpenedByRole.ToString(),
                    count,
                    recurrenceThreshold,
                    windowDays,
                    $"Usuario repetiu o motivo '{g.Key.ReasonCode}' em {count} disputa(s).",
                    "Avaliar evidencia historica, identificar padrao de reincidencia e aplicar governanca preventiva.",
                    ids);
            })
            .ToList();

        alerts.AddRange(recurringReasonAlerts);

        return alerts
            .OrderByDescending(a => ResolveAnomalySeverityRank(a.Severity))
            .ThenByDescending(a => a.MetricValue)
            .ThenBy(a => a.UserName)
            .Take(50)
            .ToList();
    }

    private static int ResolveAnomalySeverityRank(string severity)
    {
        return severity.Equals("Critical", StringComparison.OrdinalIgnoreCase) ? 2 :
            severity.Equals("Warning", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    private static bool IsCaseAlreadyAnonymized(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            if (!document.RootElement.TryGetProperty("lgpd", out var lgpdNode))
            {
                return false;
            }

            if (!lgpdNode.TryGetProperty("anonymized", out var anonymizedNode))
            {
                return false;
            }

            return anonymizedNode.ValueKind == JsonValueKind.True;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildAnonymizedFileName(string? originalFileName)
    {
        var extension = string.IsNullOrWhiteSpace(originalFileName)
            ? string.Empty
            : Path.GetExtension(originalFileName);
        return string.IsNullOrWhiteSpace(extension)
            ? "arquivo-anonimizado"
            : $"arquivo-anonimizado{extension.ToLowerInvariant()}";
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var normalized = value.Replace("\"", "\"\"");
        var mustQuote = normalized.Contains(',') || normalized.Contains('"') || normalized.Contains('\n') || normalized.Contains('\r');
        return mustQuote ? $"\"{normalized}\"" : normalized;
    }

    private sealed record FinancialDecisionExecutionResult(
        bool Success,
        object? Metadata = null,
        string? ErrorCode = null,
        string? ErrorMessage = null)
    {
        public static FinancialDecisionExecutionResult SuccessResult(object metadata)
            => new(true, metadata);

        public static FinancialDecisionExecutionResult FailResult(string errorCode, string errorMessage)
            => new(false, null, errorCode, errorMessage);
    }
}
