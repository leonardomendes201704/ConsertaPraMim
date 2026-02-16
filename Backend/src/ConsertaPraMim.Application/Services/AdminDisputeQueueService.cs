using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using System.Text.Json;

namespace ConsertaPraMim.Application.Services;

public class AdminDisputeQueueService : IAdminDisputeQueueService
{
    private readonly IServiceDisputeCaseRepository _serviceDisputeCaseRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAdminAuditLogRepository _adminAuditLogRepository;

    public AdminDisputeQueueService(
        IServiceDisputeCaseRepository serviceDisputeCaseRepository,
        IUserRepository userRepository,
        IAdminAuditLogRepository adminAuditLogRepository)
    {
        _serviceDisputeCaseRepository = serviceDisputeCaseRepository;
        _userRepository = userRepository;
        _adminAuditLogRepository = adminAuditLogRepository;
    }

    public async Task<AdminDisputesQueueResponseDto> GetQueueAsync(Guid? highlightedDisputeCaseId, int take = 100)
    {
        var normalizedTake = Math.Clamp(take, 1, 200);
        var cases = (await _serviceDisputeCaseRepository.GetOpenCasesAsync(normalizedTake))
            .Where(c => IsOpenStatus(c.Status))
            .ToList();

        var items = cases
            .Select(MapToQueueItem)
            .ToList();

        return new AdminDisputesQueueResponseDto(
            highlightedDisputeCaseId,
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

        var nowUtc = DateTime.UtcNow;
        disputeCase.OwnedByAdminUserId ??= actorUserId;
        disputeCase.OwnedAtUtc ??= nowUtc;
        disputeCase.Status = resolutionStatus;
        disputeCase.WaitingForRole = null;
        disputeCase.ResolutionSummary = resolutionSummary;
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
            MetadataJson = JsonSerializer.Serialize(new
            {
                outcome = normalizedOutcome,
                status = resolutionStatus.ToString(),
                justification
            })
        });

        await _adminAuditLogRepository.AddAsync(new AdminAuditLog
        {
            ActorUserId = actorUserId,
            ActorEmail = string.IsNullOrWhiteSpace(actorEmail) ? "admin@consertapramim.local" : actorEmail.Trim(),
            Action = "DisputeDecisionRecorded",
            TargetType = "ServiceDisputeCase",
            TargetId = disputeCase.Id,
            Metadata = JsonSerializer.Serialize(new
            {
                outcome = normalizedOutcome,
                status = resolutionStatus.ToString(),
                justification
            })
        });

        var details = await GetCaseDetailsAsync(disputeCase.Id);
        return new AdminDisputeOperationResultDto(true, Case: details);
    }

    private static bool IsOpenStatus(DisputeCaseStatus status)
    {
        return status is DisputeCaseStatus.Open or DisputeCaseStatus.UnderReview or DisputeCaseStatus.WaitingParties;
    }

    private static bool IsClosedStatus(DisputeCaseStatus status)
    {
        return status is DisputeCaseStatus.Resolved or DisputeCaseStatus.Rejected or DisputeCaseStatus.Cancelled;
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
}
