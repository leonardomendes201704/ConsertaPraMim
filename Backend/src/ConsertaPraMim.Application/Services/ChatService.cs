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
                .ToList());
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
                .ToList());
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
}
