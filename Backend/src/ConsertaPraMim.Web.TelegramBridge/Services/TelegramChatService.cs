using ConsertaPraMim.Web.TelegramBridge.Models;
using Microsoft.AspNetCore.Http;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public sealed class TelegramChatService : ITelegramChatService
{
    private const string ClientSenderName = "Cliente";
    private const string PanelSenderName = "Atendente";
    private const string AssistantSenderName = "ConsertaPraMim";

    private readonly ITelegramConversationStore _store;
    private readonly ITelegramBotApiClient _telegramBotApiClient;
    private readonly ITelegramAttachmentStorage _attachmentStorage;
    private readonly ITelegramChatRealtimeNotifier _realtimeNotifier;

    public TelegramChatService(
        ITelegramConversationStore store,
        ITelegramBotApiClient telegramBotApiClient,
        ITelegramAttachmentStorage attachmentStorage,
        ITelegramChatRealtimeNotifier realtimeNotifier)
    {
        _store = store;
        _telegramBotApiClient = telegramBotApiClient;
        _attachmentStorage = attachmentStorage;
        _realtimeNotifier = realtimeNotifier;
    }

    public IReadOnlyList<ChatConversationSummaryDto> GetConversations() => _store.GetConversations();

    public IReadOnlyList<ChatMessageDto> GetMessages(long chatId, int take) => _store.GetMessages(chatId, take);

    public async Task<ChatConversationSummaryDto> OpenConversationAsync(
        long chatId,
        string? title,
        CancellationToken cancellationToken)
    {
        var summary = _store.EnsureConversation(chatId, title);
        await _realtimeNotifier.BroadcastConversationUpsertedAsync(summary, cancellationToken);
        return summary;
    }

    public async Task<ChatMessageDto> SendFromPanelAsync(
        long chatId,
        string? text,
        IReadOnlyList<IFormFile> files,
        CancellationToken cancellationToken)
    {
        var safeText = NormalizeOptionalText(text);
        var storedFiles = await _attachmentStorage.SavePanelFilesAsync(chatId, files, cancellationToken);

        if (string.IsNullOrWhiteSpace(safeText) && storedFiles.Count == 0)
        {
            throw new InvalidOperationException("Informe uma mensagem ou adicione anexos.");
        }

        await _telegramBotApiClient.SendMessageAsync(chatId, safeText, storedFiles, cancellationToken);

        var result = _store.AddMessage(
            chatId: chatId,
            title: null,
            isOutgoing: true,
            senderDisplayName: PanelSenderName,
            text: safeText,
            sentAtUtc: DateTimeOffset.UtcNow,
            attachments: storedFiles.Select(MapAttachment).ToList());

        await _realtimeNotifier.BroadcastConversationMessageAsync(result.Summary, result.Message, cancellationToken);
        return result.Message;
    }

    public async Task<ChatMessageDto> SendFromClientAsync(
        long chatId,
        string? text,
        IReadOnlyList<IFormFile> files,
        CancellationToken cancellationToken)
    {
        var safeText = NormalizeOptionalText(text);
        var storedFiles = await _attachmentStorage.SavePanelFilesAsync(chatId, files, cancellationToken);

        if (string.IsNullOrWhiteSpace(safeText) && storedFiles.Count == 0)
        {
            throw new InvalidOperationException("Informe uma mensagem ou adicione anexos.");
        }

        var result = _store.AddMessage(
            chatId: chatId,
            title: null,
            isOutgoing: true,
            senderDisplayName: ClientSenderName,
            text: safeText,
            sentAtUtc: DateTimeOffset.UtcNow,
            attachments: storedFiles.Select(MapAttachment).ToList());

        await _realtimeNotifier.BroadcastConversationMessageAsync(result.Summary, result.Message, cancellationToken);
        return result.Message;
    }

    public async Task<ChatMessageDto> AppendAssistantReplyAsync(
        long chatId,
        string text,
        CancellationToken cancellationToken)
    {
        var safeText = NormalizeOptionalText(text);
        if (string.IsNullOrWhiteSpace(safeText))
        {
            throw new InvalidOperationException("Resposta do assistente nao pode ser vazia.");
        }

        var result = _store.AddMessage(
            chatId: chatId,
            title: null,
            isOutgoing: false,
            senderDisplayName: AssistantSenderName,
            text: safeText,
            sentAtUtc: DateTimeOffset.UtcNow,
            attachments: []);

        await _realtimeNotifier.BroadcastConversationMessageAsync(result.Summary, result.Message, cancellationToken);
        return result.Message;
    }

    public async Task<ChatMessageDto?> ReceiveFromTelegramAsync(TelegramMessage message, CancellationToken cancellationToken)
    {
        if (message.Chat is null)
        {
            return null;
        }

        var chatId = message.Chat.Id;
        if (chatId == 0)
        {
            return null;
        }

        var safeText = NormalizeOptionalText(message.Text) ?? NormalizeOptionalText(message.Caption);
        var storedIncomingFiles = await _attachmentStorage.SaveIncomingTelegramFilesAsync(chatId, message, cancellationToken);

        if (string.IsNullOrWhiteSpace(safeText) && storedIncomingFiles.Count == 0)
        {
            return null;
        }

        var title = ResolveConversationTitle(message);
        var senderName = ResolveSenderName(message);

        var sentAtUtc = message.DateUnix > 0
            ? DateTimeOffset.FromUnixTimeSeconds(message.DateUnix).ToUniversalTime()
            : DateTimeOffset.UtcNow;

        var result = _store.AddMessage(
            chatId: chatId,
            title: title,
            isOutgoing: false,
            senderDisplayName: senderName,
            text: safeText,
            sentAtUtc: sentAtUtc,
            attachments: storedIncomingFiles.Select(MapAttachment).ToList(),
            messageId: BuildInboundTelegramMessageId(chatId, message.MessageId));

        await _realtimeNotifier.BroadcastConversationMessageAsync(result.Summary, result.Message, cancellationToken);
        return result.Message;
    }

    private static ChatAttachmentDto MapAttachment(StoredLocalFile file)
    {
        return new ChatAttachmentDto(
            Id: Guid.NewGuid().ToString("N"),
            FileName: file.FileName,
            ContentType: file.ContentType,
            SizeBytes: file.SizeBytes,
            Url: file.RelativeUrl,
            MediaKind: file.MediaKind);
    }

    private static string ResolveConversationTitle(TelegramMessage message)
    {
        var chat = message.Chat;

        if (!string.IsNullOrWhiteSpace(chat.Title))
        {
            return chat.Title.Trim();
        }

        var fullName = string.Join(
            " ",
            new[]
            {
                chat.FirstName?.Trim(),
                chat.LastName?.Trim()
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName;
        }

        if (!string.IsNullOrWhiteSpace(chat.Username))
        {
            return $"@{chat.Username.Trim()}";
        }

        return $"Chat {chat.Id}";
    }

    private static string ResolveSenderName(TelegramMessage message)
    {
        if (message.From is null)
        {
            return ResolveConversationTitle(message);
        }

        var fullName = string.Join(
            " ",
            new[]
            {
                message.From.FirstName?.Trim(),
                message.From.LastName?.Trim()
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName;
        }

        if (!string.IsNullOrWhiteSpace(message.From.Username))
        {
            return $"@{message.From.Username.Trim()}";
        }

        return $"Telegram {message.From.Id}";
    }

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string? BuildInboundTelegramMessageId(long chatId, long messageId)
    {
        if (chatId <= 0 || messageId <= 0)
        {
            return null;
        }

        return $"telegram:{chatId}:{messageId}";
    }
}
