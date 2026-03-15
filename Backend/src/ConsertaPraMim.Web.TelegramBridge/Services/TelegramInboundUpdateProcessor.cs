using ConsertaPraMim.Web.TelegramBridge.Models;
using ConsertaPraMim.Web.TelegramBridge.Options;
using ConsertaPraMim.Web.TelegramBridge.Security;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public sealed class TelegramInboundUpdateProcessor : ITelegramInboundUpdateProcessor
{
    private const string ClientsBoardType = "clientes";
    private const string ProvidersBoardType = "prestadores";

    private readonly ITelegramChatService _telegramChatService;
    private readonly TelegramAutomationOptions _automationOptions;
    private readonly ITelegramLeadAutomationClient _telegramLeadAutomationClient;
    private readonly ITelegramMessageAutomationClient _telegramMessageAutomationClient;
    private readonly ITelegramBotApiClient _telegramBotApiClient;
    private readonly ITelegramHumanHandoffStateService _humanHandoffStateService;
    private readonly ITelegramChatbotObservabilityService _observabilityService;
    private readonly ILogger<TelegramInboundUpdateProcessor> _logger;

    public TelegramInboundUpdateProcessor(
        ITelegramChatService telegramChatService,
        IOptions<TelegramAutomationOptions> automationOptions,
        ITelegramLeadAutomationClient telegramLeadAutomationClient,
        ITelegramMessageAutomationClient telegramMessageAutomationClient,
        ITelegramBotApiClient telegramBotApiClient,
        ITelegramHumanHandoffStateService humanHandoffStateService,
        ITelegramChatbotObservabilityService observabilityService,
        ILogger<TelegramInboundUpdateProcessor> logger)
    {
        _telegramChatService = telegramChatService;
        _automationOptions = automationOptions.Value;
        _telegramLeadAutomationClient = telegramLeadAutomationClient;
        _telegramMessageAutomationClient = telegramMessageAutomationClient;
        _telegramBotApiClient = telegramBotApiClient;
        _humanHandoffStateService = humanHandoffStateService;
        _observabilityService = observabilityService;
        _logger = logger;
    }

    public async Task<bool> ProcessAsync(TelegramUpdate update, string source, CancellationToken cancellationToken)
    {
        if (update.Message is null)
        {
            return false;
        }

        try
        {
            var attachmentCount = (update.Message.Photo?.Count ?? 0)
                + (update.Message.Document is null ? 0 : 1)
                + (update.Message.Audio is null ? 0 : 1)
                + (update.Message.Video is null ? 0 : 1)
                + (update.Message.Voice is null ? 0 : 1);

            _observabilityService.RecordInboundMessage(attachmentCount);

            var storedMessage = await _telegramChatService.ReceiveFromTelegramAsync(update.Message, cancellationToken);
            var bootstrap = await TryBootstrapLeadAsync(update.Message, storedMessage, cancellationToken);
            await TryMirrorInboundMessageAsync(update.Message, storedMessage, bootstrap.ChatbotConversationId, cancellationToken);
            await TrySendAutomaticAcknowledgementAsync(update.Message, bootstrap, cancellationToken);
            return storedMessage is not null;
        }
        catch (Exception exception)
        {
            var stageName = string.Equals(source, "webhook", StringComparison.OrdinalIgnoreCase)
                ? "telegram_webhook_update"
                : "telegram_polling_update";

            _observabilityService.RecordIncident(
                stage: stageName,
                errorCode: "telegram_update_processing_failed",
                correlationId: null,
                message: exception.Message);

            _logger.LogWarning(
                exception,
                "Falha ao processar update Telegram {UpdateId} via {Source}",
                update.UpdateId,
                source);

            throw;
        }
    }

    private async Task<TelegramInboundBootstrapResult> TryBootstrapLeadAsync(
        TelegramMessage updateMessage,
        ChatMessageDto? storedMessage,
        CancellationToken cancellationToken)
    {
        var chatId = updateMessage.Chat?.Id ?? 0;
        if (!_automationOptions.Enabled || chatId <= 0)
        {
            return TelegramInboundBootstrapResult.Disabled;
        }

        var chatbotConversationId = BuildDeterministicGuid("telegram-chatbot-conversation", chatId.ToString(CultureInfo.InvariantCulture));
        var userKey = updateMessage.From?.Id > 0
            ? updateMessage.From.Id.ToString(CultureInfo.InvariantCulture)
            : chatId.ToString(CultureInfo.InvariantCulture);
        var userId = BuildDeterministicGuid("telegram-user", userKey);
        var boardType = ResolveBoardType(updateMessage);
        var senderName = storedMessage?.SenderDisplayName;
        if (string.IsNullOrWhiteSpace(senderName))
        {
            senderName = ResolveSenderName(updateMessage);
        }

        var messageText = storedMessage?.Text;
        if (string.IsNullOrWhiteSpace(messageText))
        {
            messageText = NormalizeOptionalText(updateMessage.Text) ?? NormalizeOptionalText(updateMessage.Caption) ?? string.Empty;
        }

        try
        {
            var result = await _telegramLeadAutomationClient.UpsertLeadAsync(
                new TelegramLeadAutomationUpsertRequest
                {
                    BoardType = boardType,
                    ChatbotConversationId = chatbotConversationId,
                    ChannelConversationId = chatId.ToString(CultureInfo.InvariantCulture),
                    TelegramChatId = chatId,
                    UserId = userId,
                    UserName = senderName,
                    UserEmail = string.Empty,
                    StatusNote = "Contato inicial recebido pelo bot Telegram.",
                    InternalNotes = BuildInitialInternalNotes(updateMessage, boardType, messageText),
                    LastContactAtUtc = storedMessage?.SentAtUtc.UtcDateTime ?? ResolveSentAtUtc(updateMessage)
                },
                cancellationToken);

            if (!result.Success)
            {
                _logger.LogWarning(
                    "Bootstrap do lead Telegram falhou para o chat {ChatId}. BoardType={BoardType} StatusCode={StatusCode} Message={Message}",
                    TelegramSecuritySanitizer.MaskChatId(chatId),
                    boardType,
                    result.HttpStatusCode,
                    TelegramSecuritySanitizer.SanitizeMessage(result.Message, 300));
            }

            return new TelegramInboundBootstrapResult(
                Enabled: true,
                ChatbotConversationId: chatbotConversationId,
                BoardType: boardType,
                LeadCreated: result.Success && result.Created,
                LeadId: result.LeadId,
                Succeeded: result.Success);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Falha ao bootstrapar lead Telegram para o chat {ChatId}.",
                TelegramSecuritySanitizer.MaskChatId(chatId));

            return new TelegramInboundBootstrapResult(
                Enabled: true,
                ChatbotConversationId: chatbotConversationId,
                BoardType: boardType,
                LeadCreated: false,
                LeadId: 0,
                Succeeded: false);
        }
    }

    private async Task TryMirrorInboundMessageAsync(
        TelegramMessage updateMessage,
        ChatMessageDto? storedMessage,
        Guid? chatbotConversationId,
        CancellationToken cancellationToken)
    {
        if (!_automationOptions.Enabled || !_automationOptions.MirrorMessagesEnabled || storedMessage is null)
        {
            return;
        }

        var chatId = updateMessage.Chat?.Id ?? 0;
        if (chatId <= 0 || string.IsNullOrWhiteSpace(storedMessage.Id))
        {
            return;
        }

        try
        {
            await _telegramMessageAutomationClient.MirrorInboundMessageAsync(
                new TelegramInboundMessageAutomationRequest
                {
                    ChatbotConversationId = chatbotConversationId,
                    ChannelConversationId = chatId.ToString(),
                    ChannelMessageId = storedMessage.Id,
                    TelegramChatId = chatId,
                    SenderDisplayName = storedMessage.SenderDisplayName,
                    MessageText = storedMessage.Text ?? string.Empty,
                    SentAtUtc = storedMessage.SentAtUtc.UtcDateTime,
                    Attachments = storedMessage.Attachments
                        .Select(attachment => new TelegramInboundAttachmentDto
                        {
                            FileName = attachment.FileName,
                            MediaKind = attachment.MediaKind,
                            Url = attachment.Url
                        })
                        .ToList()
                },
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Falha ao espelhar mensagem Telegram para o CPM Full. ChatId={ChatId} MessageId={MessageId}",
                TelegramSecuritySanitizer.MaskChatId(chatId),
                storedMessage.Id);
        }
    }

    private async Task TrySendAutomaticAcknowledgementAsync(
        TelegramMessage updateMessage,
        TelegramInboundBootstrapResult bootstrap,
        CancellationToken cancellationToken)
    {
        var chatId = updateMessage.Chat?.Id ?? 0;
        if (!bootstrap.Enabled || !bootstrap.LeadCreated || chatId <= 0 || !_telegramBotApiClient.IsConfigured)
        {
            return;
        }

        if (_humanHandoffStateService.IsActive(chatId))
        {
            return;
        }

        try
        {
            await _telegramBotApiClient.SendMessageAsync(
                chatId,
                BuildAutomaticAcknowledgement(bootstrap.BoardType),
                [],
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Falha ao enviar ACK inicial do bot Telegram para o chat {ChatId}.",
                TelegramSecuritySanitizer.MaskChatId(chatId));
        }
    }

    private static string BuildInitialInternalNotes(TelegramMessage updateMessage, string boardType, string? messageText)
    {
        var messageSummary = string.IsNullOrWhiteSpace(messageText)
            ? "sem texto legivel"
            : TrimTo(messageText, 400);

        var username = updateMessage.From?.Username;
        var usernameFragment = string.IsNullOrWhiteSpace(username)
            ? string.Empty
            : $" Username: @{username.Trim()}.";

        return $"Lead originado automaticamente pelo bot Telegram no board {boardType}.{usernameFragment} Mensagem inicial: {messageSummary}";
    }

    private static string BuildAutomaticAcknowledgement(string boardType) =>
        string.Equals(boardType, ProvidersBoardType, StringComparison.OrdinalIgnoreCase)
            ? "Recebi seu contato e ja registrei seu atendimento no funil de prestadores da ConsertaPraMim. Nosso time vai continuar por aqui em instantes."
            : "Recebi sua mensagem e ja registrei seu atendimento na ConsertaPraMim. Nosso time vai continuar por aqui em instantes.";

    private static string ResolveBoardType(TelegramMessage message)
    {
        var normalized = NormalizeMessage(message.Text) ?? NormalizeMessage(message.Caption) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return ClientsBoardType;
        }

        return normalized.Contains("prestador", StringComparison.Ordinal)
               || normalized.Contains("prestadores", StringComparison.Ordinal)
               || normalized.Contains("cadastro de prestador", StringComparison.Ordinal)
               || normalized.Contains("quero me cadastrar", StringComparison.Ordinal)
               || normalized.Contains("quero trabalhar", StringComparison.Ordinal)
               || normalized.Contains("sou parceiro", StringComparison.Ordinal)
               || normalized.Contains("quero ser parceiro", StringComparison.Ordinal)
               || normalized.Contains("sou tecnico", StringComparison.Ordinal)
               || normalized.Contains("sou prestadora", StringComparison.Ordinal)
               || normalized.Contains("sou prestador", StringComparison.Ordinal)
            ? ProvidersBoardType
            : ClientsBoardType;
    }

    private static string ResolveSenderName(TelegramMessage message)
    {
        var fullName = string.Join(
            " ",
            new[]
            {
                message.From?.FirstName?.Trim(),
                message.From?.LastName?.Trim()
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName;
        }

        if (!string.IsNullOrWhiteSpace(message.From?.Username))
        {
            return $"@{message.From.Username.Trim()}";
        }

        if (!string.IsNullOrWhiteSpace(message.Chat?.Title))
        {
            return message.Chat.Title.Trim();
        }

        return "Contato Telegram";
    }

    private static DateTime ResolveSentAtUtc(TelegramMessage message)
    {
        return message.DateUnix > 0
            ? DateTimeOffset.FromUnixTimeSeconds(message.DateUnix).UtcDateTime
            : DateTime.UtcNow;
    }

    private static Guid BuildDeterministicGuid(string scope, string value)
    {
        var raw = Encoding.UTF8.GetBytes($"{scope}:{value}");
        var hash = SHA256.HashData(raw);
        var bytes = hash[..16];
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    private static string? NormalizeMessage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var decomposed = value.Trim()
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string TrimTo(string value, int maxLength)
    {
        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }

    private readonly record struct TelegramInboundBootstrapResult(
        bool Enabled,
        Guid ChatbotConversationId,
        string BoardType,
        bool LeadCreated,
        int LeadId,
        bool Succeeded)
    {
        public static TelegramInboundBootstrapResult Disabled =>
            new(false, Guid.Empty, string.Empty, false, 0, false);
    }
}
