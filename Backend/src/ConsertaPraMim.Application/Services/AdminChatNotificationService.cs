using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace ConsertaPraMim.Application.Services;

public class AdminChatNotificationService : IAdminChatNotificationService
{
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly INotificationService _notificationService;
    private readonly IAdminAuditLogRepository _adminAuditLogRepository;
    private readonly ILogger<AdminChatNotificationService> _logger;

    public AdminChatNotificationService(
        IChatMessageRepository chatMessageRepository,
        IServiceRequestRepository serviceRequestRepository,
        IUserRepository userRepository,
        INotificationService notificationService,
        IAdminAuditLogRepository adminAuditLogRepository,
        ILogger<AdminChatNotificationService>? logger = null)
    {
        _chatMessageRepository = chatMessageRepository;
        _serviceRequestRepository = serviceRequestRepository;
        _userRepository = userRepository;
        _notificationService = notificationService;
        _adminAuditLogRepository = adminAuditLogRepository;
        _logger = logger ?? NullLogger<AdminChatNotificationService>.Instance;
    }

    public async Task<AdminChatsListResponseDto> GetChatsAsync(AdminChatsQueryDto query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 100);
        var (fromUtc, toUtc) = NormalizeRange(query.FromUtc, query.ToUtc);
        var normalizedSearchTerm = query.SearchTerm?.Trim();

        var messages = (await _chatMessageRepository.GetByPeriodAsync(fromUtc, toUtc))
            .Where(m => !query.RequestId.HasValue || m.RequestId == query.RequestId.Value)
            .Where(m => !query.ProviderId.HasValue || m.ProviderId == query.ProviderId.Value)
            .Where(m => !query.ClientId.HasValue || m.Request.ClientId == query.ClientId.Value)
            .ToList();

        var usersById = (await _userRepository.GetAllAsync())
            .GroupBy(u => u.Id)
            .ToDictionary(g => g.Key, g => g.First());

        var conversations = messages
            .GroupBy(m => new { m.RequestId, m.ProviderId })
            .Select(g =>
            {
                var orderedMessages = g.OrderBy(m => m.CreatedAt).ToList();
                var lastMessage = orderedMessages[^1];
                var request = lastMessage.Request;
                var client = request.Client;
                usersById.TryGetValue(g.Key.ProviderId, out var provider);

                return new
                {
                    Item = new AdminChatConversationListItemDto(
                        g.Key.RequestId,
                        g.Key.ProviderId,
                        request.Description,
                        request.Status.ToString(),
                        client.Name,
                        MaskEmail(client.Email),
                        MaskPhone(client.Phone),
                        provider?.Name ?? "Prestador",
                        MaskEmail(provider?.Email),
                        MaskPhone(provider?.Phone),
                        lastMessage.CreatedAt,
                        lastMessage.SenderRole.ToString(),
                        BuildPreview(lastMessage),
                        orderedMessages.Count,
                        orderedMessages.Sum(m => m.Attachments.Count)),
                    SearchBlob = BuildSearchBlob(
                        request.Description,
                        request.Status.ToString(),
                        client.Name,
                        client.Email,
                        provider?.Name,
                        provider?.Email,
                        orderedMessages[^1].Text)
                };
            })
            .ToList();

        if (!string.IsNullOrWhiteSpace(normalizedSearchTerm))
        {
            conversations = conversations
                .Where(c => c.SearchBlob.Contains(normalizedSearchTerm, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var ordered = conversations
            .OrderByDescending(c => c.Item.LastMessageAt)
            .Select(c => c.Item)
            .ToList();

        var totalCount = ordered.Count;
        var items = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new AdminChatsListResponseDto(page, pageSize, totalCount, items);
    }

    public async Task<AdminChatDetailsDto?> GetChatAsync(Guid requestId, Guid providerId)
    {
        var request = await _serviceRequestRepository.GetByIdAsync(requestId);
        if (request == null) return null;

        var hasProposalFromProvider = request.Proposals.Any(p => p.ProviderId == providerId);
        if (!hasProposalFromProvider) return null;

        var provider = await _userRepository.GetByIdAsync(providerId);
        if (provider == null) return null;

        var messages = await _chatMessageRepository.GetConversationAsync(requestId, providerId);
        var mappedMessages = messages
            .OrderBy(m => m.CreatedAt)
            .Select(m => new AdminChatMessageDto(
                m.Id,
                m.SenderId,
                ResolveSenderName(m, request, provider),
                m.SenderRole.ToString(),
                m.Text,
                m.CreatedAt,
                m.Attachments
                    .OrderBy(a => a.CreatedAt)
                    .Select(a => new AdminChatMessageAttachmentDto(
                        a.Id,
                        a.FileUrl,
                        a.FileName,
                        a.ContentType,
                        a.SizeBytes,
                        a.MediaKind,
                        a.CreatedAt))
                    .ToList()))
            .ToList();

        return new AdminChatDetailsDto(
            request.Id,
            providerId,
            request.Description,
            request.Status.ToString(),
            request.Client.Name,
            MaskEmail(request.Client.Email),
            MaskPhone(request.Client.Phone),
            provider.Name,
            MaskEmail(provider.Email),
            MaskPhone(provider.Phone),
            mappedMessages);
    }

    public async Task<AdminChatAttachmentsListResponseDto> GetChatAttachmentsAsync(AdminChatAttachmentsQueryDto query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 100);
        var (fromUtc, toUtc) = NormalizeRange(query.FromUtc, query.ToUtc);
        var normalizedMediaKind = query.MediaKind?.Trim().ToLowerInvariant();

        var messages = (await _chatMessageRepository.GetByPeriodAsync(fromUtc, toUtc))
            .Where(m => !query.RequestId.HasValue || m.RequestId == query.RequestId.Value)
            .Where(m => !query.UserId.HasValue || m.SenderId == query.UserId.Value)
            .ToList();

        var attachments = messages
            .SelectMany(m => m.Attachments.Select(a => new { Message = m, Attachment = a }))
            .Where(x => string.IsNullOrWhiteSpace(normalizedMediaKind) ||
                        x.Attachment.MediaKind.Equals(normalizedMediaKind, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Attachment.CreatedAt)
            .Select(x => new AdminChatAttachmentListItemDto(
                x.Attachment.Id,
                x.Message.Id,
                x.Message.RequestId,
                x.Message.ProviderId,
                x.Message.SenderId,
                x.Message.Sender?.Name ?? "Usuario",
                x.Message.SenderRole.ToString(),
                x.Message.Request.Description,
                x.Attachment.FileUrl,
                x.Attachment.FileName,
                x.Attachment.ContentType,
                x.Attachment.SizeBytes,
                x.Attachment.MediaKind,
                x.Attachment.CreatedAt))
            .ToList();

        var totalCount = attachments.Count;
        var items = attachments
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new AdminChatAttachmentsListResponseDto(page, pageSize, totalCount, items);
    }

    public async Task<AdminSendNotificationResultDto> SendNotificationAsync(
        AdminSendNotificationRequestDto request,
        Guid actorUserId,
        string actorEmail)
    {
        if (request.RecipientUserId == Guid.Empty)
        {
            _logger.LogWarning(
                "Admin notification send failed: invalid recipient user id. ActorUserId={ActorUserId}",
                actorUserId);
            return new AdminSendNotificationResultDto(false, "invalid_payload", "Usuario destinatario invalido.");
        }

        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Message))
        {
            _logger.LogWarning(
                "Admin notification send failed: missing subject or message. ActorUserId={ActorUserId}, RecipientUserId={RecipientUserId}",
                actorUserId,
                request.RecipientUserId);
            return new AdminSendNotificationResultDto(false, "invalid_payload", "Assunto e mensagem sao obrigatorios.");
        }

        var recipient = await _userRepository.GetByIdAsync(request.RecipientUserId);
        if (recipient == null)
        {
            _logger.LogWarning(
                "Admin notification send failed: recipient not found. ActorUserId={ActorUserId}, RecipientUserId={RecipientUserId}",
                actorUserId,
                request.RecipientUserId);
            return new AdminSendNotificationResultDto(false, "not_found", "Usuario destinatario nao encontrado.");
        }

        if (!recipient.IsActive)
        {
            _logger.LogWarning(
                "Admin notification send failed: recipient inactive. ActorUserId={ActorUserId}, RecipientUserId={RecipientUserId}",
                actorUserId,
                recipient.Id);
            return new AdminSendNotificationResultDto(false, "recipient_inactive", "Usuario destinatario esta inativo.");
        }

        var subject = request.Subject.Trim();
        var message = request.Message.Trim();
        var actionUrl = string.IsNullOrWhiteSpace(request.ActionUrl) ? null : request.ActionUrl.Trim();
        var recipientChannel = recipient.Id.ToString("N");

        await _notificationService.SendNotificationAsync(recipientChannel, subject, message, actionUrl);

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "-" : request.Reason.Trim();
        var metadata = JsonSerializer.Serialize(new
        {
            before = new
            {
                notificationQueued = false
            },
            after = new
            {
                notificationQueued = true,
                recipientChannel,
                subject = Truncate(subject, 120),
                actionUrl = actionUrl ?? "-"
            },
            reason = Truncate(reason, 180)
        });

        await _adminAuditLogRepository.AddAsync(new AdminAuditLog
        {
            ActorUserId = actorUserId,
            ActorEmail = actorEmail,
            Action = "ManualNotificationSent",
            TargetType = "User",
            TargetId = recipient.Id,
            Metadata = metadata
        });

        _logger.LogInformation(
            "Admin notification sent. ActorUserId={ActorUserId}, RecipientUserId={RecipientUserId}, RecipientChannel={RecipientChannel}",
            actorUserId,
            recipient.Id,
            recipientChannel);

        return new AdminSendNotificationResultDto(true);
    }

    private static string ResolveSenderName(ChatMessage message, ServiceRequest request, User provider)
    {
        if (!string.IsNullOrWhiteSpace(message.Sender?.Name))
        {
            return message.Sender.Name;
        }

        if (message.SenderId == request.ClientId)
        {
            return request.Client.Name;
        }

        if (message.SenderId == provider.Id)
        {
            return provider.Name;
        }

        return "Usuario";
    }

    private static (DateTime FromUtc, DateTime ToUtc) NormalizeRange(DateTime? fromUtc, DateTime? toUtc)
    {
        var end = toUtc ?? DateTime.UtcNow;
        var start = fromUtc ?? end.AddDays(-30);
        if (start > end)
        {
            (start, end) = (end, start);
        }

        return (start, end);
    }

    private static string BuildPreview(ChatMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            var text = message.Text.Trim();
            return text.Length <= 120 ? text : $"{text[..120]}...";
        }

        var attachmentCount = message.Attachments.Count;
        return attachmentCount > 0
            ? $"Mensagem com {attachmentCount} anexo(s)."
            : "Mensagem sem conteudo.";
    }

    private static string BuildSearchBlob(
        string requestDescription,
        string requestStatus,
        string clientName,
        string clientEmail,
        string? providerName,
        string? providerEmail,
        string? lastMessageText)
    {
        return string.Join(
            " ",
            new[]
            {
                requestDescription,
                requestStatus,
                clientName,
                clientEmail,
                providerName ?? string.Empty,
                providerEmail ?? string.Empty,
                lastMessageText ?? string.Empty
            });
    }

    private static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return string.Empty;

        var trimmed = email.Trim();
        var atIndex = trimmed.IndexOf('@');
        if (atIndex <= 0 || atIndex == trimmed.Length - 1) return "***";

        var local = trimmed[..atIndex];
        var domain = trimmed[(atIndex + 1)..];
        var localMasked = local.Length switch
        {
            1 => "*",
            2 => $"{local[0]}*",
            _ => $"{local[0]}{new string('*', Math.Min(local.Length - 2, 6))}{local[^1]}"
        };

        var domainParts = domain.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (domainParts.Length == 0)
        {
            return $"{localMasked}@***";
        }

        var host = domainParts[0];
        var maskedHost = host.Length <= 1
            ? "*"
            : $"{host[0]}{new string('*', Math.Min(host.Length - 1, 4))}";

        var tld = domainParts.Length > 1 ? $".{string.Join('.', domainParts.Skip(1))}" : string.Empty;
        return $"{localMasked}@{maskedHost}{tld}";
    }

    private static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return "***";
        if (digits.Length <= 4) return new string('*', digits.Length);

        return $"{new string('*', digits.Length - 4)}{digits[^4..]}";
    }

    private static string Truncate(string value, int length)
    {
        if (value.Length <= length) return value;
        return value[..length];
    }
}
