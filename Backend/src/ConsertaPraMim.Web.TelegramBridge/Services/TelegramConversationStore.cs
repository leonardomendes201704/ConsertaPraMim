using System.Collections.Concurrent;
using ConsertaPraMim.Web.TelegramBridge.Models;
using ConsertaPraMim.Web.TelegramBridge.Options;
using ConsertaPraMim.Web.TelegramBridge.Security;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public sealed class TelegramConversationStore : ITelegramConversationStore
{
    private readonly ConcurrentDictionary<long, ConversationState> _conversations = new();
    private readonly int _maxMessagesPerConversation;

    public TelegramConversationStore(IOptions<TelegramBridgeOptions> options)
    {
        _maxMessagesPerConversation = Math.Max(50, options.Value.MaxMessagesPerConversation);
    }

    public IReadOnlyList<ChatConversationSummaryDto> GetConversations()
    {
        return _conversations.Values
            .Select(static state =>
            {
                lock (state.Sync)
                {
                    return ToSummary(state);
                }
            })
            .OrderByDescending(summary => summary.UpdatedAtUtc)
            .ToList();
    }

    public IReadOnlyList<ChatMessageDto> GetMessages(long chatId, int take)
    {
        var normalizedTake = Math.Clamp(take, 1, 500);
        if (!_conversations.TryGetValue(chatId, out var state))
        {
            return [];
        }

        lock (state.Sync)
        {
            return state.Messages
                .OrderBy(message => message.SentAtUtc)
                .TakeLast(normalizedTake)
                .ToList();
        }
    }

    public ChatConversationSummaryDto EnsureConversation(long chatId, string? title)
    {
        var state = _conversations.GetOrAdd(chatId, static id => new ConversationState(id));
        lock (state.Sync)
        {
            if (!string.IsNullOrWhiteSpace(title))
            {
                state.Title = title.Trim();
            }

            if (string.IsNullOrWhiteSpace(state.Title))
            {
                state.Title = TelegramSecuritySanitizer.BuildMaskedChatLabel(chatId);
            }

            if (state.UpdatedAtUtc == default)
            {
                state.UpdatedAtUtc = DateTimeOffset.UtcNow;
            }

            return ToSummary(state);
        }
    }

    public StoreAppendResult AddMessage(
        long chatId,
        string? title,
        bool isOutgoing,
        string senderDisplayName,
        string? text,
        DateTimeOffset sentAtUtc,
        IReadOnlyList<ChatAttachmentDto> attachments,
        string? messageId = null)
    {
        var state = _conversations.GetOrAdd(chatId, static id => new ConversationState(id));
        lock (state.Sync)
        {
            if (!string.IsNullOrWhiteSpace(title))
            {
                state.Title = title.Trim();
            }

            if (string.IsNullOrWhiteSpace(state.Title))
            {
                state.Title = TelegramSecuritySanitizer.BuildMaskedChatLabel(chatId);
            }

            var message = new ChatMessageDto(
                Id: string.IsNullOrWhiteSpace(messageId) ? Guid.NewGuid().ToString("N") : messageId.Trim(),
                ChatId: chatId,
                IsOutgoing: isOutgoing,
                SenderDisplayName: string.IsNullOrWhiteSpace(senderDisplayName)
                    ? (isOutgoing ? "Atendente" : "Telegram")
                    : senderDisplayName.Trim(),
                Text: string.IsNullOrWhiteSpace(text) ? null : text.Trim(),
                SentAtUtc: sentAtUtc == default ? DateTimeOffset.UtcNow : sentAtUtc.ToUniversalTime(),
                Attachments: attachments);

            state.Messages.Add(message);
            if (state.Messages.Count > _maxMessagesPerConversation)
            {
                state.Messages.RemoveRange(0, state.Messages.Count - _maxMessagesPerConversation);
            }

            state.LastMessagePreview = BuildPreview(message);
            state.UpdatedAtUtc = message.SentAtUtc;

            return new StoreAppendResult(ToSummary(state), message);
        }
    }

    private static string BuildPreview(ChatMessageDto message)
    {
        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            return message.Text.Length <= 100
                ? message.Text
                : string.Concat(message.Text[..97], "...");
        }

        if (message.Attachments.Count == 0)
        {
            return "Mensagem vazia";
        }

        if (message.Attachments.Count == 1)
        {
            return $"Anexo: {message.Attachments[0].FileName}";
        }

        return $"{message.Attachments.Count} anexos";
    }

    private static ChatConversationSummaryDto ToSummary(ConversationState state)
    {
        return new ChatConversationSummaryDto(
            ChatId: state.ChatId,
            Title: state.Title,
            LastMessagePreview: string.IsNullOrWhiteSpace(state.LastMessagePreview)
                ? "Sem mensagens"
                : state.LastMessagePreview,
            UpdatedAtUtc: state.UpdatedAtUtc == default ? DateTimeOffset.UtcNow : state.UpdatedAtUtc,
            TotalMessages: state.Messages.Count);
    }

    private sealed class ConversationState
    {
        public ConversationState(long chatId)
        {
            ChatId = chatId;
            Title = TelegramSecuritySanitizer.BuildMaskedChatLabel(chatId);
            LastMessagePreview = "Sem mensagens";
            UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        public long ChatId { get; }

        public object Sync { get; } = new();

        public string Title { get; set; }

        public string LastMessagePreview { get; set; }

        public DateTimeOffset UpdatedAtUtc { get; set; }

        public List<ChatMessageDto> Messages { get; } = [];
    }
}
