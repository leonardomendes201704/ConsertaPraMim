using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;

namespace ConsertaPraMim.Application.Services;

public class AdminSupportTicketService : IAdminSupportTicketService
{
    private const string AuditTargetType = "SupportTicket";

    private readonly ISupportTicketRepository _supportTicketRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAdminAuditLogRepository _adminAuditLogRepository;

    public AdminSupportTicketService(
        ISupportTicketRepository supportTicketRepository,
        IUserRepository userRepository,
        IAdminAuditLogRepository adminAuditLogRepository)
    {
        _supportTicketRepository = supportTicketRepository;
        _userRepository = userRepository;
        _adminAuditLogRepository = adminAuditLogRepository;
    }

    public async Task<AdminSupportTicketListResponseDto> GetTicketsAsync(AdminSupportTicketListQueryDto query)
    {
        var safeQuery = query ?? new AdminSupportTicketListQueryDto();
        var safePage = safeQuery.Page < 1 ? 1 : safeQuery.Page;
        var safePageSize = safeQuery.PageSize <= 0 ? 20 : Math.Min(safeQuery.PageSize, 100);
        var safeSlaMinutes = safeQuery.FirstResponseSlaMinutes <= 0
            ? 60
            : Math.Min(safeQuery.FirstResponseSlaMinutes, 24 * 60 * 7);

        var hasStatus = TryParseStatus(safeQuery.Status, out var status);
        var hasPriority = TryParsePriority(safeQuery.Priority, out var priority);

        var (items, totalCount) = await _supportTicketRepository.GetAdminTicketsAsync(
            hasStatus ? status : null,
            hasPriority ? priority : null,
            safeQuery.AssignedAdminUserId,
            safeQuery.AssignedOnly,
            safeQuery.Search,
            safeQuery.SortBy,
            safeQuery.SortDescending,
            safePage,
            safePageSize);

        var indicators = await _supportTicketRepository.GetAdminQueueIndicatorsAsync(
            hasStatus ? status : null,
            hasPriority ? priority : null,
            safeQuery.AssignedAdminUserId,
            safeQuery.AssignedOnly,
            safeQuery.Search,
            DateTime.UtcNow,
            safeSlaMinutes);

        var mapped = items
            .Select(ticket => MapSummary(ticket, safeSlaMinutes))
            .ToList();
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)safePageSize);

        return new AdminSupportTicketListResponseDto(
            mapped,
            safePage,
            safePageSize,
            totalCount,
            totalPages,
            new AdminSupportTicketQueueIndicatorsDto(
                indicators.OpenCount,
                indicators.InProgressCount,
                indicators.WaitingProviderCount,
                indicators.ResolvedCount,
                indicators.ClosedCount,
                indicators.WithoutFirstAdminResponseCount,
                indicators.OverdueWithoutFirstResponseCount,
                indicators.UnassignedCount));
    }

    public async Task<AdminSupportTicketOperationResultDto> GetTicketDetailsAsync(Guid ticketId)
    {
        if (ticketId == Guid.Empty)
        {
            return new AdminSupportTicketOperationResultDto(
                false,
                ErrorCode: "admin_support_invalid_ticket",
                ErrorMessage: "Chamado invalido.");
        }

        var ticket = await _supportTicketRepository.GetAdminTicketByIdWithMessagesAsync(ticketId);
        if (ticket == null)
        {
            return new AdminSupportTicketOperationResultDto(
                false,
                ErrorCode: "admin_support_ticket_not_found",
                ErrorMessage: "Chamado nao encontrado.");
        }

        return new AdminSupportTicketOperationResultDto(
            true,
            Ticket: MapDetails(ticket, 60));
    }

    public async Task<AdminSupportTicketOperationResultDto> AddMessageAsync(
        Guid ticketId,
        Guid actorUserId,
        string actorEmail,
        AdminSupportTicketMessageRequestDto request)
    {
        if (ticketId == Guid.Empty)
        {
            return new AdminSupportTicketOperationResultDto(
                false,
                ErrorCode: "admin_support_invalid_ticket",
                ErrorMessage: "Chamado invalido.");
        }

        if (actorUserId == Guid.Empty)
        {
            return new AdminSupportTicketOperationResultDto(
                false,
                ErrorCode: "admin_support_invalid_actor",
                ErrorMessage: "Usuario admin invalido.");
        }

        var messageText = NormalizeText(request.Message);
        if (string.IsNullOrWhiteSpace(messageText))
        {
            return new AdminSupportTicketOperationResultDto(
                false,
                ErrorCode: "admin_support_message_required",
                ErrorMessage: "Mensagem do chamado e obrigatoria.");
        }

        if (messageText.Length > 3000)
        {
            return new AdminSupportTicketOperationResultDto(
                false,
                ErrorCode: "admin_support_message_too_long",
                ErrorMessage: "Mensagem deve ter no maximo 3000 caracteres.");
        }

        var ticket = await _supportTicketRepository.GetAdminTicketByIdWithMessagesAsync(ticketId);
        if (ticket == null)
        {
            return new AdminSupportTicketOperationResultDto(
                false,
                ErrorCode: "admin_support_ticket_not_found",
                ErrorMessage: "Chamado nao encontrado.");
        }

        if (ticket.Status == SupportTicketStatus.Closed)
        {
            return new AdminSupportTicketOperationResultDto(
                false,
                ErrorCode: "admin_support_invalid_state",
                ErrorMessage: "Chamado fechado nao permite novas mensagens.");
        }

        var messageType = string.IsNullOrWhiteSpace(request.MessageType)
            ? request.IsInternal ? "AdminInternalNote" : "AdminReply"
            : request.MessageType.Trim();

        var createdMessage = ticket.AddMessage(
            actorUserId,
            UserRole.Admin,
            messageText,
            request.IsInternal,
            messageType,
            request.MetadataJson);

        if (!request.IsInternal)
        {
            ticket.ChangeStatus(SupportTicketStatus.WaitingProvider);
        }

        await _supportTicketRepository.UpdateAsync(ticket);

        await RecordAuditAsync(
            actorUserId,
            actorEmail,
            "support_ticket_message_added",
            ticket.Id,
            new
            {
                ticket.Status,
                request.IsInternal,
                messageType
            });

        var refreshed = await _supportTicketRepository.GetAdminTicketByIdWithMessagesAsync(ticket.Id) ?? ticket;

        return new AdminSupportTicketOperationResultDto(
            true,
            Ticket: MapDetails(refreshed, 60),
            Message: MapMessage(createdMessage));
    }

    public async Task<AdminSupportTicketOperationResultDto> UpdateStatusAsync(
        Guid ticketId,
        Guid actorUserId,
        string actorEmail,
        AdminSupportTicketStatusUpdateRequestDto request)
    {
        if (ticketId == Guid.Empty)
        {
            return new AdminSupportTicketOperationResultDto(
                false,
                ErrorCode: "admin_support_invalid_ticket",
                ErrorMessage: "Chamado invalido.");
        }

        if (actorUserId == Guid.Empty)
        {
            return new AdminSupportTicketOperationResultDto(
                false,
                ErrorCode: "admin_support_invalid_actor",
                ErrorMessage: "Usuario admin invalido.");
        }

        if (!TryParseStatus(request.Status, out var nextStatus))
        {
            return new AdminSupportTicketOperationResultDto(
                false,
                ErrorCode: "admin_support_invalid_status",
                ErrorMessage: "Status invalido.");
        }

        var ticket = await _supportTicketRepository.GetAdminTicketByIdWithMessagesAsync(ticketId);
        if (ticket == null)
        {
            return new AdminSupportTicketOperationResultDto(
                false,
                ErrorCode: "admin_support_ticket_not_found",
                ErrorMessage: "Chamado nao encontrado.");
        }

        var previousStatus = ticket.Status;
        if (!IsStatusTransitionAllowed(previousStatus, nextStatus))
        {
            return new AdminSupportTicketOperationResultDto(
                false,
                ErrorCode: "admin_support_invalid_transition",
                ErrorMessage: $"Transicao de status invalida: {previousStatus} -> {nextStatus}.");
        }

        var normalizedNote = NormalizeText(request.Note);
        if (!string.IsNullOrWhiteSpace(normalizedNote))
        {
            ticket.AddMessage(
                actorUserId,
                UserRole.Admin,
                normalizedNote,
                isInternal: true,
                messageType: "AdminStatusNote");
        }

        ticket.ChangeStatus(nextStatus);
        if (nextStatus == SupportTicketStatus.Closed)
        {
            ticket.AddMessage(
                actorUserId,
                UserRole.Admin,
                "Chamado encerrado pelo admin.",
                isInternal: false,
                messageType: "AdminClosed");
        }

        await _supportTicketRepository.UpdateAsync(ticket);

        await RecordAuditAsync(
            actorUserId,
            actorEmail,
            "support_ticket_status_changed",
            ticket.Id,
            new
            {
                previousStatus = previousStatus.ToString(),
                nextStatus = nextStatus.ToString(),
                note = NormalizeText(request.Note)
            });

        if (nextStatus == SupportTicketStatus.Closed)
        {
            await RecordAuditAsync(
                actorUserId,
                actorEmail,
                "support_ticket_closed",
                ticket.Id,
                new
                {
                    previousStatus = previousStatus.ToString(),
                    note = NormalizeText(request.Note)
                });
        }

        var refreshed = await _supportTicketRepository.GetAdminTicketByIdWithMessagesAsync(ticket.Id) ?? ticket;
        return new AdminSupportTicketOperationResultDto(
            true,
            Ticket: MapDetails(refreshed, 60));
    }

    public async Task<AdminSupportTicketOperationResultDto> AssignAsync(
        Guid ticketId,
        Guid actorUserId,
        string actorEmail,
        AdminSupportTicketAssignRequestDto request)
    {
        if (ticketId == Guid.Empty)
        {
            return new AdminSupportTicketOperationResultDto(
                false,
                ErrorCode: "admin_support_invalid_ticket",
                ErrorMessage: "Chamado invalido.");
        }

        if (actorUserId == Guid.Empty)
        {
            return new AdminSupportTicketOperationResultDto(
                false,
                ErrorCode: "admin_support_invalid_actor",
                ErrorMessage: "Usuario admin invalido.");
        }

        var ticket = await _supportTicketRepository.GetAdminTicketByIdWithMessagesAsync(ticketId);
        if (ticket == null)
        {
            return new AdminSupportTicketOperationResultDto(
                false,
                ErrorCode: "admin_support_ticket_not_found",
                ErrorMessage: "Chamado nao encontrado.");
        }

        var previousAssignedAdminUserId = ticket.AssignedAdminUserId;
        if (request.AssignedAdminUserId.HasValue)
        {
            if (request.AssignedAdminUserId.Value == Guid.Empty)
            {
                return new AdminSupportTicketOperationResultDto(
                    false,
                    ErrorCode: "admin_support_invalid_assignee",
                    ErrorMessage: "Usuario admin de atribuicao invalido.");
            }

            var adminUser = await _userRepository.GetByIdAsync(request.AssignedAdminUserId.Value);
            if (adminUser == null || adminUser.Role != UserRole.Admin)
            {
                return new AdminSupportTicketOperationResultDto(
                false,
                ErrorCode: "admin_support_assignee_not_admin",
                ErrorMessage: "Somente usuarios admin podem ser atribuidos.");
            }

            ticket.AssignAdmin(request.AssignedAdminUserId.Value);
        }
        else
        {
            ticket.AssignedAdminUserId = null;
            ticket.AssignedAtUtc = null;
            ticket.UpdatedAt = DateTime.UtcNow;
        }

        var note = NormalizeText(request.Note);
        if (!string.IsNullOrWhiteSpace(note))
        {
            ticket.AddMessage(
                actorUserId,
                UserRole.Admin,
                note,
                isInternal: true,
                messageType: "AdminAssignmentNote");
        }

        await _supportTicketRepository.UpdateAsync(ticket);

        await RecordAuditAsync(
            actorUserId,
            actorEmail,
            "support_ticket_assignment_changed",
            ticket.Id,
            new
            {
                previousAssignedAdminUserId,
                nextAssignedAdminUserId = ticket.AssignedAdminUserId
            });

        var refreshed = await _supportTicketRepository.GetAdminTicketByIdWithMessagesAsync(ticket.Id) ?? ticket;
        return new AdminSupportTicketOperationResultDto(
            true,
            Ticket: MapDetails(refreshed, 60));
    }

    private static AdminSupportTicketDetailsDto MapDetails(SupportTicket ticket, int firstResponseSlaMinutes)
    {
        var summary = MapSummary(ticket, firstResponseSlaMinutes);
        var messages = (ticket.Messages ?? Array.Empty<SupportTicketMessage>())
            .OrderBy(message => message.CreatedAt)
            .Select(MapMessage)
            .ToList();

        return new AdminSupportTicketDetailsDto(summary, ticket.MetadataJson, messages);
    }

    private static AdminSupportTicketSummaryDto MapSummary(SupportTicket ticket, int firstResponseSlaMinutes)
    {
        var orderedMessages = (ticket.Messages ?? Array.Empty<SupportTicketMessage>())
            .OrderBy(message => message.CreatedAt)
            .ToList();
        var lastMessage = orderedMessages.LastOrDefault();

        return new AdminSupportTicketSummaryDto(
            ticket.Id,
            ticket.ProviderId,
            ticket.Provider?.Name ?? "Prestador",
            ticket.Provider?.Email ?? string.Empty,
            ticket.AssignedAdminUserId,
            ticket.AssignedAdminUser?.Name,
            ticket.Subject,
            ticket.Category,
            ticket.Priority.ToString(),
            ticket.Status.ToString(),
            ticket.OpenedAtUtc,
            ticket.LastInteractionAtUtc,
            ticket.FirstAdminResponseAtUtc,
            ticket.ClosedAtUtc,
            orderedMessages.Count,
            TruncateForPreview(lastMessage?.MessageText, 240),
            IsOverdueFirstResponse(ticket, firstResponseSlaMinutes));
    }

    private static AdminSupportTicketMessageDto MapMessage(SupportTicketMessage message)
    {
        return new AdminSupportTicketMessageDto(
            message.Id,
            message.AuthorUserId,
            message.AuthorRole.ToString(),
            ResolveAuthorName(message),
            message.MessageType,
            message.MessageText,
            message.IsInternal,
            message.MetadataJson,
            message.CreatedAt);
    }

    private static string ResolveAuthorName(SupportTicketMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.AuthorUser?.Name))
        {
            return message.AuthorUser.Name;
        }

        return message.AuthorRole switch
        {
            UserRole.Admin => "Admin",
            UserRole.Provider => "Prestador",
            _ => "Sistema"
        };
    }

    private static bool IsOverdueFirstResponse(SupportTicket ticket, int firstResponseSlaMinutes)
    {
        if (ticket.FirstAdminResponseAtUtc.HasValue)
        {
            return false;
        }

        if (!IsActiveStatus(ticket.Status))
        {
            return false;
        }

        var elapsed = DateTime.UtcNow - ticket.OpenedAtUtc;
        return elapsed.TotalMinutes >= firstResponseSlaMinutes;
    }

    private static bool IsActiveStatus(SupportTicketStatus status)
    {
        return status is SupportTicketStatus.Open
            or SupportTicketStatus.InProgress
            or SupportTicketStatus.WaitingProvider;
    }

    private static bool IsStatusTransitionAllowed(SupportTicketStatus current, SupportTicketStatus next)
    {
        if (current == next)
        {
            return true;
        }

        return current switch
        {
            SupportTicketStatus.Open => next is SupportTicketStatus.InProgress
                or SupportTicketStatus.WaitingProvider
                or SupportTicketStatus.Resolved
                or SupportTicketStatus.Closed,
            SupportTicketStatus.InProgress => next is SupportTicketStatus.Open
                or SupportTicketStatus.WaitingProvider
                or SupportTicketStatus.Resolved
                or SupportTicketStatus.Closed,
            SupportTicketStatus.WaitingProvider => next is SupportTicketStatus.InProgress
                or SupportTicketStatus.Resolved
                or SupportTicketStatus.Closed,
            SupportTicketStatus.Resolved => next is SupportTicketStatus.InProgress
                or SupportTicketStatus.WaitingProvider
                or SupportTicketStatus.Closed,
            SupportTicketStatus.Closed => false,
            _ => false
        };
    }

    private static bool TryParseStatus(string? raw, out SupportTicketStatus parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var normalized = raw.Trim();
        if (Enum.TryParse(normalized, true, out parsed))
        {
            return true;
        }

        if (int.TryParse(normalized, out var numeric) &&
            Enum.IsDefined(typeof(SupportTicketStatus), numeric))
        {
            parsed = (SupportTicketStatus)numeric;
            return true;
        }

        return false;
    }

    private static bool TryParsePriority(string? raw, out SupportTicketPriority parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var normalized = raw.Trim();
        if (Enum.TryParse(normalized, true, out parsed))
        {
            return true;
        }

        if (int.TryParse(normalized, out var numeric) &&
            Enum.IsDefined(typeof(SupportTicketPriority), numeric))
        {
            parsed = (SupportTicketPriority)numeric;
            return true;
        }

        return false;
    }

    private static string? NormalizeText(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? TruncateForPreview(string? value, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length <= maxChars)
        {
            return normalized;
        }

        return $"{normalized[..Math.Max(1, maxChars - 3)]}...";
    }

    private async Task RecordAuditAsync(
        Guid actorUserId,
        string actorEmail,
        string action,
        Guid ticketId,
        object metadata)
    {
        await _adminAuditLogRepository.AddAsync(new AdminAuditLog
        {
            ActorUserId = actorUserId,
            ActorEmail = actorEmail,
            Action = action,
            TargetType = AuditTargetType,
            TargetId = ticketId,
            Metadata = JsonSerializer.Serialize(metadata)
        });
    }
}
