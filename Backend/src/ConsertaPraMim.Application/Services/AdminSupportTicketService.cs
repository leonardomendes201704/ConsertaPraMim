using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace ConsertaPraMim.Application.Services;

public class AdminSupportTicketService : IAdminSupportTicketService
{
    private const string AuditTargetType = "SupportTicket";
    private const int MaxAttachmentsPerMessage = 10;
    private const long MaxAttachmentSizeBytes = 25_000_000;

    private static readonly HashSet<string> AllowedAttachmentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif",
        ".mp4", ".webm", ".mov", ".avi",
        ".mp3", ".wav", ".ogg", ".m4a", ".aac",
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".csv", ".zip"
    };

    private readonly ISupportTicketRepository _supportTicketRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAdminAuditLogRepository _adminAuditLogRepository;
    private readonly INotificationService? _notificationService;
    private readonly ILogger<AdminSupportTicketService>? _logger;

    public AdminSupportTicketService(
        ISupportTicketRepository supportTicketRepository,
        IUserRepository userRepository,
        IAdminAuditLogRepository adminAuditLogRepository,
        INotificationService? notificationService = null,
        ILogger<AdminSupportTicketService>? logger = null)
    {
        _supportTicketRepository = supportTicketRepository;
        _userRepository = userRepository;
        _adminAuditLogRepository = adminAuditLogRepository;
        _notificationService = notificationService;
        _logger = logger;
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

        var actorValidation = await ValidateAndResolveActorAsync(actorUserId, actorEmail);
        if (!actorValidation.IsValid)
        {
            return new AdminSupportTicketOperationResultDto(
                false,
                ErrorCode: actorValidation.ErrorCode,
                ErrorMessage: actorValidation.ErrorMessage);
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

        if (!TryNormalizeAttachments(
            request.Attachments,
            out var normalizedAttachments,
            out var attachmentsErrorCode,
            out var attachmentsErrorMessage))
        {
            return new AdminSupportTicketOperationResultDto(
                false,
                ErrorCode: attachmentsErrorCode,
                ErrorMessage: attachmentsErrorMessage);
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
            actorValidation.ActorUserId,
            UserRole.Admin,
            messageText,
            request.IsInternal,
            messageType,
            request.MetadataJson);
        createdMessage.Attachments = normalizedAttachments;

        if (!request.IsInternal)
        {
            ticket.ChangeStatus(SupportTicketStatus.WaitingProvider);
        }

        await _supportTicketRepository.UpdateAsync(ticket);

        await RecordAuditAsync(
            actorValidation.ActorUserId,
            actorValidation.ActorEmail,
            "support_ticket_message_added",
            ticket.Id,
            new
            {
                ticket.Status,
                request.IsInternal,
                messageType,
                attachmentsCount = normalizedAttachments.Count
            });

        if (!request.IsInternal)
        {
            await TryNotifyProviderAsync(
                ticket,
                $"Nova resposta no chamado #{BuildTicketShortCode(ticket.Id)}",
                $"O time admin respondeu: {TruncateForPreview(messageText, 220)}",
                $"/SupportTickets/Details/{ticket.Id}",
                "admin_support_message_added");
        }

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

        var actorValidation = await ValidateAndResolveActorAsync(actorUserId, actorEmail);
        if (!actorValidation.IsValid)
        {
            return new AdminSupportTicketOperationResultDto(
                false,
                ErrorCode: actorValidation.ErrorCode,
                ErrorMessage: actorValidation.ErrorMessage);
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
                actorValidation.ActorUserId,
                UserRole.Admin,
                normalizedNote,
                isInternal: true,
                messageType: "AdminStatusNote");
        }

        ticket.ChangeStatus(nextStatus);
        var isReopened = previousStatus == SupportTicketStatus.Closed && nextStatus != SupportTicketStatus.Closed;
        if (nextStatus == SupportTicketStatus.Closed)
        {
            ticket.AddMessage(
                actorValidation.ActorUserId,
                UserRole.Admin,
                "Chamado encerrado pelo admin.",
                isInternal: false,
                messageType: "AdminClosed");
        }

        await _supportTicketRepository.UpdateAsync(ticket);

        await RecordAuditAsync(
            actorValidation.ActorUserId,
            actorValidation.ActorEmail,
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
                actorValidation.ActorUserId,
                actorValidation.ActorEmail,
                "support_ticket_closed",
                ticket.Id,
                new
                {
                    previousStatus = previousStatus.ToString(),
                    note = NormalizeText(request.Note)
                });
        }

        if (isReopened)
        {
            await RecordAuditAsync(
                actorValidation.ActorUserId,
                actorValidation.ActorEmail,
                "support_ticket_reopened",
                ticket.Id,
                new
                {
                    previousStatus = previousStatus.ToString(),
                    nextStatus = nextStatus.ToString(),
                    note = NormalizeText(request.Note)
                });
        }

        await TryNotifyProviderAsync(
            ticket,
            $"Status atualizado no chamado #{BuildTicketShortCode(ticket.Id)}",
            $"Seu chamado foi atualizado para {nextStatus}.",
            $"/SupportTickets/Details/{ticket.Id}",
            "admin_support_status_changed");

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

        var actorValidation = await ValidateAndResolveActorAsync(actorUserId, actorEmail);
        if (!actorValidation.IsValid)
        {
            return new AdminSupportTicketOperationResultDto(
                false,
                ErrorCode: actorValidation.ErrorCode,
                ErrorMessage: actorValidation.ErrorMessage);
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
                actorValidation.ActorUserId,
                UserRole.Admin,
                note,
                isInternal: true,
                messageType: "AdminAssignmentNote");
        }

        await _supportTicketRepository.UpdateAsync(ticket);

        await RecordAuditAsync(
            actorValidation.ActorUserId,
            actorValidation.ActorEmail,
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
            BuildMessagePreview(lastMessage, 240),
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
            (message.Attachments ?? Array.Empty<SupportTicketMessageAttachment>())
                .OrderBy(attachment => attachment.CreatedAt)
                .Select(MapAttachment)
                .ToList(),
            message.CreatedAt);
    }

    private static SupportTicketAttachmentDto MapAttachment(SupportTicketMessageAttachment attachment)
    {
        return new SupportTicketAttachmentDto(
            attachment.Id,
            attachment.FileUrl,
            attachment.FileName,
            attachment.ContentType,
            attachment.SizeBytes,
            attachment.MediaKind);
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
            SupportTicketStatus.Closed => next is SupportTicketStatus.Open
                or SupportTicketStatus.InProgress,
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

    private static string? BuildMessagePreview(SupportTicketMessage? message, int maxChars)
    {
        if (message == null)
        {
            return null;
        }

        var messagePreview = TruncateForPreview(message.MessageText, maxChars);
        if (!string.IsNullOrWhiteSpace(messagePreview))
        {
            return messagePreview;
        }

        var attachmentsCount = message.Attachments?.Count ?? 0;
        if (attachmentsCount <= 0)
        {
            return null;
        }

        return attachmentsCount == 1
            ? "1 anexo."
            : $"{attachmentsCount} anexos.";
    }

    private static bool TryNormalizeAttachments(
        IReadOnlyList<SupportTicketAttachmentInputDto>? attachments,
        out List<SupportTicketMessageAttachment> normalized,
        out string errorCode,
        out string errorMessage)
    {
        normalized = new List<SupportTicketMessageAttachment>();
        errorCode = string.Empty;
        errorMessage = string.Empty;

        if (attachments == null || attachments.Count == 0)
        {
            return true;
        }

        if (attachments.Count > MaxAttachmentsPerMessage)
        {
            errorCode = "admin_support_too_many_attachments";
            errorMessage = $"Cada mensagem aceita no maximo {MaxAttachmentsPerMessage} anexos.";
            return false;
        }

        foreach (var attachment in attachments)
        {
            var fileName = NormalizeText(attachment.FileName);
            var contentType = NormalizeText(attachment.ContentType);
            if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(contentType))
            {
                errorCode = "admin_support_attachment_invalid";
                errorMessage = "Anexo invalido: nome e content type sao obrigatorios.";
                return false;
            }

            if (attachment.SizeBytes <= 0 || attachment.SizeBytes > MaxAttachmentSizeBytes)
            {
                errorCode = "admin_support_attachment_size_invalid";
                errorMessage = $"Anexo '{fileName}' excede o limite de {MaxAttachmentSizeBytes / 1_000_000}MB.";
                return false;
            }

            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedAttachmentExtensions.Contains(extension))
            {
                errorCode = "admin_support_attachment_type_invalid";
                errorMessage = $"Tipo de arquivo nao suportado para '{fileName}'.";
                return false;
            }

            if (!TryNormalizeAttachmentUrl(attachment.FileUrl, out var normalizedUrl))
            {
                errorCode = "admin_support_attachment_url_invalid";
                errorMessage = $"Url do anexo '{fileName}' invalida.";
                return false;
            }

            normalized.Add(new SupportTicketMessageAttachment
            {
                FileUrl = normalizedUrl,
                FileName = fileName,
                ContentType = contentType,
                SizeBytes = attachment.SizeBytes,
                MediaKind = ResolveMediaKind(contentType, extension)
            });
        }

        return true;
    }

    private static bool TryNormalizeAttachmentUrl(string? fileUrl, out string normalizedUrl)
    {
        normalizedUrl = string.Empty;
        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            return false;
        }

        var trimmed = fileUrl.Trim();
        if (trimmed.StartsWith("/uploads/support/", StringComparison.OrdinalIgnoreCase))
        {
            normalizedUrl = trimmed;
            return true;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        if (!uri.AbsolutePath.StartsWith("/uploads/support/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        normalizedUrl = uri.AbsoluteUri;
        return true;
    }

    private static string ResolveMediaKind(string contentType, string extension)
    {
        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return "image";
        }

        if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        {
            return "video";
        }

        if (contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
        {
            return "audio";
        }

        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" => "image",
            ".mp4" or ".webm" or ".mov" or ".avi" => "video",
            ".mp3" or ".wav" or ".ogg" or ".m4a" or ".aac" => "audio",
            _ => "document"
        };
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

    private async Task TryNotifyProviderAsync(
        SupportTicket ticket,
        string subject,
        string message,
        string actionUrl,
        string reason)
    {
        if (_notificationService == null || ticket.ProviderId == Guid.Empty)
        {
            return;
        }

        try
        {
            await _notificationService.SendNotificationAsync(
                ticket.ProviderId.ToString(),
                subject,
                message,
                actionUrl);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(
                ex,
                "Support provider notification failed. Reason={Reason} TicketId={TicketId} ProviderId={ProviderId}",
                reason,
                ticket.Id,
                ticket.ProviderId);
        }
    }

    private static string BuildTicketShortCode(Guid ticketId)
    {
        return ticketId.ToString("N")[..8].ToUpperInvariant();
    }

    private async Task<ActorValidationResult> ValidateAndResolveActorAsync(Guid actorUserId, string actorEmail)
    {
        var normalizedEmail = NormalizeText(actorEmail);
        User? actor = null;

        if (actorUserId != Guid.Empty)
        {
            actor = await _userRepository.GetByIdAsync(actorUserId);
        }

        if (actor == null && !string.IsNullOrWhiteSpace(normalizedEmail))
        {
            actor = await _userRepository.GetByEmailAsync(normalizedEmail);
        }

        if (actor == null)
        {
            return ActorValidationResult.Fail(
                "admin_support_actor_not_found",
                "Sessao admin invalida ou expirada. Faca login novamente.");
        }

        if (actor.Role != UserRole.Admin)
        {
            return ActorValidationResult.Fail(
                "admin_support_actor_not_admin",
                "Usuario autenticado nao possui permissao administrativa.");
        }

        return ActorValidationResult.Success(actor.Id, actor.Email);
    }

    private readonly record struct ActorValidationResult(
        bool IsValid,
        Guid ActorUserId,
        string ActorEmail,
        string? ErrorCode,
        string? ErrorMessage)
    {
        public static ActorValidationResult Success(Guid actorUserId, string actorEmail) =>
            new(true, actorUserId, actorEmail, null, null);

        public static ActorValidationResult Fail(string errorCode, string errorMessage) =>
            new(false, Guid.Empty, string.Empty, errorCode, errorMessage);
    }
}
