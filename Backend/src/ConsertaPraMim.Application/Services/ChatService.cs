using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;

namespace ConsertaPraMim.Application.Services;

public class ChatService : IChatService
{
    private readonly IChatMessageRepository _chatRepository;
    private readonly IServiceRequestRepository _requestRepository;
    private readonly IUserRepository _userRepository;

    public ChatService(
        IChatMessageRepository chatRepository,
        IServiceRequestRepository requestRepository,
        IUserRepository userRepository)
    {
        _chatRepository = chatRepository;
        _requestRepository = requestRepository;
        _userRepository = userRepository;
    }

    public async Task<bool> CanAccessConversationAsync(Guid requestId, Guid providerId, Guid userId, string role)
    {
        var request = await _requestRepository.GetByIdAsync(requestId);
        if (request == null) return false;

        var hasProposal = request.Proposals.Any(p => p.ProviderId == providerId);
        if (!hasProposal) return false;

        if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase)) return true;

        if (role.Equals("Client", StringComparison.OrdinalIgnoreCase))
        {
            return request.ClientId == userId;
        }

        if (role.Equals("Provider", StringComparison.OrdinalIgnoreCase))
        {
            return userId == providerId;
        }

        return false;
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetConversationHistoryAsync(Guid requestId, Guid providerId, Guid userId, string role)
    {
        var allowed = await CanAccessConversationAsync(requestId, providerId, userId, role);
        if (!allowed) return Array.Empty<ChatMessageDto>();

        var messages = await _chatRepository.GetConversationAsync(requestId, providerId);
        return messages.Select(MapMessage).ToList();
    }

    public async Task<Guid?> ResolveRecipientIdAsync(Guid requestId, Guid providerId, Guid senderId)
    {
        var request = await _requestRepository.GetByIdAsync(requestId);
        if (request == null) return null;

        var hasProposal = request.Proposals.Any(p => p.ProviderId == providerId);
        if (!hasProposal) return null;

        if (senderId == providerId)
        {
            return request.ClientId;
        }

        if (senderId == request.ClientId)
        {
            return providerId;
        }

        return null;
    }

    public async Task<ChatMessageDto?> SendMessageAsync(
        Guid requestId,
        Guid providerId,
        Guid senderId,
        string role,
        string? text,
        IEnumerable<ChatAttachmentInputDto>? attachments)
    {
        var allowed = await CanAccessConversationAsync(requestId, providerId, senderId, role);
        if (!allowed) return null;

        var cleanText = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        var cleanAttachments = new List<ChatAttachment>();
        foreach (var attachment in attachments ?? Array.Empty<ChatAttachmentInputDto>())
        {
            if (!TryNormalizeAttachmentUrl(attachment.FileUrl, out var normalizedFileUrl))
            {
                continue;
            }

            cleanAttachments.Add(new ChatAttachment
            {
                FileUrl = normalizedFileUrl,
                FileName = attachment.FileName?.Trim() ?? string.Empty,
                ContentType = attachment.ContentType?.Trim() ?? string.Empty,
                SizeBytes = attachment.SizeBytes,
                MediaKind = ResolveMediaKind(attachment.ContentType)
            });
        }

        if (cleanText == null && cleanAttachments.Count == 0) return null;

        var sender = await _userRepository.GetByIdAsync(senderId);
        if (sender == null) return null;

        var senderRole = sender.Role;
        if (Enum.TryParse<UserRole>(role, true, out var parsedRole))
        {
            senderRole = parsedRole;
        }

        var message = new ChatMessage
        {
            RequestId = requestId,
            ProviderId = providerId,
            SenderId = sender.Id,
            SenderRole = senderRole,
            Text = cleanText,
            Attachments = cleanAttachments
        };

        await _chatRepository.AddAsync(message);

        return new ChatMessageDto(
            message.Id,
            message.RequestId,
            message.ProviderId,
            message.SenderId,
            sender.Name,
            message.SenderRole.ToString(),
            message.Text,
            message.CreatedAt,
            message.Attachments
                .Select(a => new ChatAttachmentDto(a.Id, a.FileUrl, a.FileName, a.ContentType, a.SizeBytes, a.MediaKind))
                .ToList(),
            message.DeliveredAt,
            message.ReadAt);
    }

    public Task<IReadOnlyList<ChatMessageReceiptDto>> MarkConversationDeliveredAsync(Guid requestId, Guid providerId, Guid userId, string role)
    {
        return UpdateConversationReceiptsAsync(requestId, providerId, userId, role, onlyUnread: false, markRead: false);
    }

    public Task<IReadOnlyList<ChatMessageReceiptDto>> MarkConversationReadAsync(Guid requestId, Guid providerId, Guid userId, string role)
    {
        return UpdateConversationReceiptsAsync(requestId, providerId, userId, role, onlyUnread: true, markRead: true);
    }

    public async Task<IReadOnlyList<ChatConversationSummaryDto>> GetActiveConversationsAsync(Guid userId, string role)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(role))
        {
            return Array.Empty<ChatConversationSummaryDto>();
        }

        var normalizedRole = role.Trim();
        var isProvider = normalizedRole.Equals("Provider", StringComparison.OrdinalIgnoreCase);
        var isClient = normalizedRole.Equals("Client", StringComparison.OrdinalIgnoreCase);
        var isAdmin = normalizedRole.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        if (!isProvider && !isClient && !isAdmin)
        {
            return Array.Empty<ChatConversationSummaryDto>();
        }

        var messages = await _chatRepository.GetConversationsByParticipantAsync(userId, normalizedRole)
            ?? Array.Empty<ChatMessage>();
        if (messages.Count == 0)
        {
            return Array.Empty<ChatConversationSummaryDto>();
        }

        var summaries = new List<ChatConversationSummaryDto>();

        foreach (var group in messages.GroupBy(m => new { m.RequestId, m.ProviderId }))
        {
            var latestMessage = group
                .OrderByDescending(m => m.CreatedAt)
                .First();

            var request = latestMessage.Request;
            if (request == null)
            {
                continue;
            }

            var provider = request.Proposals
                .FirstOrDefault(p => p.ProviderId == group.Key.ProviderId)
                ?.Provider;

            var client = request.Client;

            Guid counterpartId;
            string counterpartRole;
            string counterpartName;
            string title;

            if (isProvider)
            {
                if (client == null) continue;
                counterpartId = client.Id;
                counterpartRole = "Client";
                counterpartName = client.Name;
                title = BuildConversationTitle(counterpartRole, counterpartName, request.Description);
            }
            else if (isClient || isAdmin)
            {
                if (provider == null)
                {
                    var providerUser = await _userRepository.GetByIdAsync(group.Key.ProviderId);
                    if (providerUser == null) continue;

                    counterpartId = providerUser.Id;
                    counterpartRole = "Provider";
                    counterpartName = providerUser.Name;
                }
                else
                {
                    counterpartId = provider.Id;
                    counterpartRole = "Provider";
                    counterpartName = provider.Name;
                }

                title = BuildConversationTitle(counterpartRole, counterpartName, request.Description);
            }
            else
            {
                continue;
            }

            var unreadMessages = group.Count(m => m.SenderId != userId && !m.ReadAt.HasValue);
            summaries.Add(new ChatConversationSummaryDto(
                RequestId: group.Key.RequestId,
                ProviderId: group.Key.ProviderId,
                CounterpartUserId: counterpartId,
                CounterpartRole: counterpartRole,
                CounterpartName: counterpartName,
                Title: title,
                LastMessagePreview: BuildConversationPreview(latestMessage),
                LastMessageAt: latestMessage.CreatedAt,
                UnreadMessages: unreadMessages));
        }

        return summaries
            .OrderByDescending(s => s.LastMessageAt)
            .ToList();
    }

    private async Task<IReadOnlyList<ChatMessageReceiptDto>> UpdateConversationReceiptsAsync(
        Guid requestId,
        Guid providerId,
        Guid userId,
        string role,
        bool onlyUnread,
        bool markRead)
    {
        var allowed = await CanAccessConversationAsync(requestId, providerId, userId, role);
        if (!allowed) return Array.Empty<ChatMessageReceiptDto>();

        var pendingMessages = await _chatRepository.GetPendingReceiptsAsync(requestId, providerId, userId, onlyUnread);
        if (pendingMessages.Count == 0) return Array.Empty<ChatMessageReceiptDto>();

        var now = DateTime.UtcNow;
        var changed = new List<ChatMessage>(pendingMessages.Count);
        foreach (var message in pendingMessages)
        {
            var isChanged = false;
            if (!markRead && !message.DeliveredAt.HasValue)
            {
                message.DeliveredAt = now;
                isChanged = true;
            }

            if (markRead && !message.ReadAt.HasValue)
            {
                message.ReadAt = now;
                if (!message.DeliveredAt.HasValue)
                {
                    message.DeliveredAt = now;
                }

                isChanged = true;
            }

            if (isChanged)
            {
                changed.Add(message);
            }
        }

        if (changed.Count == 0)
        {
            return Array.Empty<ChatMessageReceiptDto>();
        }

        await _chatRepository.UpdateRangeAsync(changed);
        return changed.Select(MapReceipt).ToList();
    }

    private static ChatMessageDto MapMessage(ChatMessage message)
    {
        return new ChatMessageDto(
            message.Id,
            message.RequestId,
            message.ProviderId,
            message.SenderId,
            message.Sender?.Name ?? "Usuario",
            message.SenderRole.ToString(),
            message.Text,
            message.CreatedAt,
            message.Attachments
                .OrderBy(a => a.CreatedAt)
                .Select(a => new ChatAttachmentDto(a.Id, a.FileUrl, a.FileName, a.ContentType, a.SizeBytes, a.MediaKind))
                .ToList(),
            message.DeliveredAt,
            message.ReadAt);
    }

    private static ChatMessageReceiptDto MapReceipt(ChatMessage message)
    {
        return new ChatMessageReceiptDto(
            message.Id,
            message.RequestId,
            message.ProviderId,
            message.DeliveredAt,
            message.ReadAt);
    }

    private static string ResolveMediaKind(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return "file";
        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return "image";
        if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)) return "video";
        return "file";
    }

    private static bool TryNormalizeAttachmentUrl(string? fileUrl, out string normalizedUrl)
    {
        normalizedUrl = string.Empty;
        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            return false;
        }

        var trimmed = fileUrl.Trim();
        if (trimmed.StartsWith("/uploads/chat/", StringComparison.OrdinalIgnoreCase))
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

        if (!uri.AbsolutePath.StartsWith("/uploads/chat/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        normalizedUrl = uri.AbsoluteUri;
        return true;
    }

    private static string BuildConversationPreview(ChatMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            return message.Text.Length > 120
                ? $"{message.Text[..120]}..."
                : message.Text;
        }

        var attachmentCount = message.Attachments?.Count ?? 0;
        if (attachmentCount > 0)
        {
            return attachmentCount == 1
                ? "Anexo enviado."
                : $"{attachmentCount} anexos enviados.";
        }

        return "Mensagem";
    }

    private static string BuildConversationTitle(string counterpartRole, string counterpartName, string requestDescription)
    {
        var safeName = string.IsNullOrWhiteSpace(counterpartName) ? "Contato" : counterpartName.Trim();
        var trimmedRequest = string.IsNullOrWhiteSpace(requestDescription) ? "Pedido" : requestDescription.Trim();
        if (trimmedRequest.Length > 64)
        {
            trimmedRequest = $"{trimmedRequest[..64]}...";
        }

        var roleLabel = counterpartRole.Equals("Provider", StringComparison.OrdinalIgnoreCase)
            ? "Prestador"
            : "Cliente";

        return $"{roleLabel}: {safeName} - {trimmedRequest}";
    }
}
