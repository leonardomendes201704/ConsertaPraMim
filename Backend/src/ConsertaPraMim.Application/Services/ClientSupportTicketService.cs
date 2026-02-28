using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;

namespace ConsertaPraMim.Application.Services;

public class ClientSupportTicketService : IClientSupportTicketService
{
    private const string TicketCategory = "ClientServiceRequestHelp";
    private const string MetadataSource = "ServiceRequestDetailsHelp";
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
    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly IUserRepository _userRepository;

    public ClientSupportTicketService(
        ISupportTicketRepository supportTicketRepository,
        IServiceRequestRepository serviceRequestRepository,
        IUserRepository userRepository)
    {
        _supportTicketRepository = supportTicketRepository;
        _serviceRequestRepository = serviceRequestRepository;
        _userRepository = userRepository;
    }

    public async Task<ClientSupportTicketDetailsDto?> GetByServiceRequestAsync(Guid clientUserId, Guid serviceRequestId)
    {
        if (clientUserId == Guid.Empty || serviceRequestId == Guid.Empty)
        {
            return null;
        }

        var request = await _serviceRequestRepository.GetByIdAsync(serviceRequestId);
        if (request == null || request.ClientId != clientUserId)
        {
            return null;
        }

        var ticket = await _supportTicketRepository.GetClientTicketByServiceRequestWithMessagesAsync(clientUserId, serviceRequestId);
        return ticket == null ? null : MapDetails(ticket, serviceRequestId);
    }

    public async Task<ClientSupportTicketOperationResultDto> AddMessageAsync(
        Guid clientUserId,
        Guid serviceRequestId,
        ClientSupportTicketMessageRequestDto request)
    {
        if (clientUserId == Guid.Empty)
        {
            return new ClientSupportTicketOperationResultDto(
                false,
                ErrorCode: "client_support_invalid_user",
                ErrorMessage: "Sessao do cliente invalida.");
        }

        if (serviceRequestId == Guid.Empty)
        {
            return new ClientSupportTicketOperationResultDto(
                false,
                ErrorCode: "client_support_invalid_request",
                ErrorMessage: "Pedido invalido.");
        }

        var serviceRequest = await _serviceRequestRepository.GetByIdAsync(serviceRequestId);
        if (serviceRequest == null || serviceRequest.ClientId != clientUserId)
        {
            return new ClientSupportTicketOperationResultDto(
                false,
                ErrorCode: "client_support_request_not_found",
                ErrorMessage: "Pedido nao encontrado para o cliente autenticado.");
        }

        var normalizedText = NormalizeText(request.Message);
        if (!TryNormalizeAttachments(
                request.Attachments,
                out var normalizedAttachments,
                out var attachmentsErrorCode,
                out var attachmentsErrorMessage))
        {
            return new ClientSupportTicketOperationResultDto(
                false,
                ErrorCode: attachmentsErrorCode,
                ErrorMessage: attachmentsErrorMessage);
        }

        if (string.IsNullOrWhiteSpace(normalizedText) && normalizedAttachments.Count == 0)
        {
            return new ClientSupportTicketOperationResultDto(
                false,
                ErrorCode: "client_support_message_required",
                ErrorMessage: "Informe uma mensagem ou anexe ao menos um arquivo.");
        }

        if (!string.IsNullOrWhiteSpace(normalizedText) && normalizedText.Length > 3000)
        {
            return new ClientSupportTicketOperationResultDto(
                false,
                ErrorCode: "client_support_message_too_long",
                ErrorMessage: "Mensagem deve ter no maximo 3000 caracteres.");
        }

        var clientUser = await _userRepository.GetByIdAsync(clientUserId);
        if (clientUser == null || clientUser.Role != UserRole.Client)
        {
            return new ClientSupportTicketOperationResultDto(
                false,
                ErrorCode: "client_support_client_not_found",
                ErrorMessage: "Nao foi possivel identificar o cliente autenticado.");
        }

        var ticket = await _supportTicketRepository.GetClientTicketByServiceRequestWithMessagesAsync(clientUserId, serviceRequestId);
        var isNewTicket = ticket == null;

        if (ticket == null)
        {
            ticket = new SupportTicket
            {
                ProviderId = clientUserId,
                Subject = BuildSubject(serviceRequest),
                Category = TicketCategory,
                Priority = SupportTicketPriority.Medium,
                Status = SupportTicketStatus.Open,
                MetadataJson = BuildMetadataJson(clientUser, serviceRequest)
            };
        }
        else if (ticket.Status == SupportTicketStatus.Closed)
        {
            return new ClientSupportTicketOperationResultDto(
                false,
                ErrorCode: "client_support_ticket_closed",
                ErrorMessage: "Esse atendimento foi encerrado e nao aceita novas mensagens.");
        }

        var effectiveMessageText = string.IsNullOrWhiteSpace(normalizedText)
            ? normalizedAttachments.Count == 1 ? "Anexo enviado." : "Anexos enviados."
            : normalizedText!;

        var createdMessage = ticket.AddMessage(
            clientUserId,
            UserRole.Client,
            effectiveMessageText,
            isInternal: false,
            messageType: isNewTicket ? "ClientOpened" : "ClientReply");
        createdMessage.Attachments = normalizedAttachments;

        if (!isNewTicket && ticket.Status is SupportTicketStatus.WaitingProvider or SupportTicketStatus.Resolved)
        {
            ticket.ChangeStatus(SupportTicketStatus.InProgress);
        }

        if (isNewTicket)
        {
            await _supportTicketRepository.AddAsync(ticket);
        }
        else
        {
            await _supportTicketRepository.UpdateAsync(ticket);
        }

        var persisted = await _supportTicketRepository.GetClientTicketByServiceRequestWithMessagesAsync(clientUserId, serviceRequestId) ?? ticket;
        return new ClientSupportTicketOperationResultDto(
            true,
            Ticket: MapDetails(persisted, serviceRequestId),
            Message: MapMessage(createdMessage));
    }

    private static ClientSupportTicketDetailsDto MapDetails(SupportTicket ticket, Guid serviceRequestId)
    {
        var visibleMessages = GetVisibleMessages(ticket)
            .Select(MapMessage)
            .ToList();

        return new ClientSupportTicketDetailsDto(
            new ClientSupportTicketSummaryDto(
                ticket.Id,
                serviceRequestId,
                ticket.Subject,
                ticket.Category,
                ticket.Priority.ToString(),
                ticket.Status.ToString(),
                ticket.OpenedAtUtc,
                ticket.LastInteractionAtUtc,
                ticket.FirstAdminResponseAtUtc,
                ticket.ClosedAtUtc,
                ticket.AssignedAdminUserId,
                ticket.AssignedAdminUser?.Name,
                visibleMessages.Count),
            visibleMessages);
    }

    private static ClientSupportTicketMessageDto MapMessage(SupportTicketMessage message)
    {
        return new ClientSupportTicketMessageDto(
            message.Id,
            message.AuthorUserId,
            message.AuthorRole.ToString(),
            ResolveAuthorName(message),
            message.MessageType,
            message.MessageText,
            (message.Attachments ?? Array.Empty<SupportTicketMessageAttachment>())
                .OrderBy(attachment => attachment.CreatedAt)
                .Select(attachment => new SupportTicketAttachmentDto(
                    attachment.Id,
                    attachment.FileUrl,
                    attachment.FileName,
                    attachment.ContentType,
                    attachment.SizeBytes,
                    attachment.MediaKind))
                .ToList(),
            message.CreatedAt);
    }

    private static IReadOnlyList<SupportTicketMessage> GetVisibleMessages(SupportTicket ticket)
    {
        return (ticket.Messages ?? Array.Empty<SupportTicketMessage>())
            .Where(message => !message.IsInternal)
            .OrderBy(message => message.CreatedAt)
            .ToList();
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
            UserRole.Client => "Cliente",
            UserRole.Provider => "Prestador",
            _ => "Sistema"
        };
    }

    private static string BuildSubject(ServiceRequest serviceRequest)
    {
        var category = string.IsNullOrWhiteSpace(serviceRequest.Category.ToString())
            ? "pedido"
            : serviceRequest.Category.ToString().Trim();

        var description = string.IsNullOrWhiteSpace(serviceRequest.Description)
            ? category
            : serviceRequest.Description.Trim();

        if (description.Length > 120)
        {
            description = $"{description[..120]}...";
        }

        return $"Ajuda sobre o pedido - {description}";
    }

    private static string BuildMetadataJson(User clientUser, ServiceRequest serviceRequest)
    {
        return JsonSerializer.Serialize(new
        {
            requesterRole = UserRole.Client.ToString(),
            requesterUserId = clientUser.Id,
            requesterName = clientUser.Name,
            serviceRequestId = serviceRequest.Id,
            serviceRequestCategory = serviceRequest.Category.ToString(),
            serviceRequestDescription = TrimForMetadata(serviceRequest.Description, 240),
            source = MetadataSource
        });
    }

    private static string? NormalizeText(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? TrimForMetadata(string? value, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxChars
            ? normalized
            : $"{normalized[..Math.Max(1, maxChars - 3)]}...";
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
            errorCode = "client_support_too_many_attachments";
            errorMessage = $"Cada mensagem aceita no maximo {MaxAttachmentsPerMessage} anexos.";
            return false;
        }

        foreach (var attachment in attachments)
        {
            var fileName = NormalizeText(attachment.FileName);
            var contentType = NormalizeText(attachment.ContentType);

            if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(contentType))
            {
                errorCode = "client_support_attachment_invalid";
                errorMessage = "Anexo invalido: nome e tipo de conteudo sao obrigatorios.";
                return false;
            }

            if (attachment.SizeBytes <= 0 || attachment.SizeBytes > MaxAttachmentSizeBytes)
            {
                errorCode = "client_support_attachment_size_invalid";
                errorMessage = $"Anexo '{fileName}' excede o limite de {MaxAttachmentSizeBytes / 1_000_000}MB.";
                return false;
            }

            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedAttachmentExtensions.Contains(extension))
            {
                errorCode = "client_support_attachment_type_invalid";
                errorMessage = $"Tipo de arquivo nao suportado para '{fileName}'.";
                return false;
            }

            if (!TryNormalizeAttachmentUrl(attachment.FileUrl, out var normalizedUrl))
            {
                errorCode = "client_support_attachment_url_invalid";
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
}
