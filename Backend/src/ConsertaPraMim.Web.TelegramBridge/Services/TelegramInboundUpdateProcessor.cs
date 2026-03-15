using ConsertaPraMim.Web.TelegramBridge.Models;
using ConsertaPraMim.Web.TelegramBridge.Options;
using ConsertaPraMim.Web.TelegramBridge.Security;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public sealed class TelegramInboundUpdateProcessor : ITelegramInboundUpdateProcessor
{
    private const string ClientsBoardType = "clientes";
    private const string ProvidersBoardType = "prestadores";
    private static readonly Regex EmailRegex = new(
        @"(?<![\w.+-])[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}(?![\w.\-])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PhoneRegex = new(
        @"(?<!\d)(?:\+?\d[\d\-\s().]{7,}\d)(?!\d)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

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
            var capturedContact = ResolveCapturedContact(update.Message);
            var bootstrap = await TryBootstrapLeadAsync(update.Message, storedMessage, capturedContact, cancellationToken);
            await TryMirrorInboundMessageAsync(update.Message, storedMessage, bootstrap.ChatbotConversationId, cancellationToken);
            await TrySendAutomaticResponseAsync(update.Message, bootstrap, capturedContact, cancellationToken);
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
        TelegramCapturedContact capturedContact,
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
                    UserPhone = capturedContact.Phone,
                    UserEmail = capturedContact.Email,
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

    private async Task TrySendAutomaticResponseAsync(
        TelegramMessage updateMessage,
        TelegramInboundBootstrapResult bootstrap,
        TelegramCapturedContact capturedContact,
        CancellationToken cancellationToken)
    {
        var chatId = updateMessage.Chat?.Id ?? 0;
        if (!bootstrap.Enabled || chatId <= 0 || !_telegramBotApiClient.IsConfigured)
        {
            return;
        }

        if (_humanHandoffStateService.IsActive(chatId))
        {
            return;
        }

        try
        {
            var response = BuildAutomaticResponse(bootstrap, capturedContact);
            if (response is null)
            {
                return;
            }

            await _telegramBotApiClient.SendMessageAsync(
                chatId,
                response.Value.Text,
                [],
                cancellationToken,
                response.Value.Options);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Falha ao enviar resposta automatica do bot Telegram para o chat {ChatId}.",
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

    private static TelegramAutomaticResponse? BuildAutomaticResponse(
        TelegramInboundBootstrapResult bootstrap,
        TelegramCapturedContact capturedContact)
    {
        if ((capturedContact.HasPhone || capturedContact.HasEmail) && !bootstrap.Succeeded)
        {
            return null;
        }

        if (capturedContact.HasPhone)
        {
            var text = capturedContact.HasEmail
                ? "Recebi seu telefone e seu e-mail, e atualizei seu atendimento na ConsertaPraMim. Nosso time segue acompanhando por aqui."
                : "Recebi seu telefone e atualizei seu atendimento na ConsertaPraMim. Se quiser, voce tambem pode enviar seu e-mail por mensagem.";

            return new TelegramAutomaticResponse(
                text,
                new TelegramMessageSendOptions
                {
                    RemoveReplyKeyboard = true
                });
        }

        if (capturedContact.HasEmail)
        {
            return new TelegramAutomaticResponse(
                "Recebi seu e-mail e atualizei seu atendimento. Se quiser agilizar, compartilhe seu telefone no botao abaixo ou envie o numero por mensagem.",
                new TelegramMessageSendOptions
                {
                    RequestContactButton = true,
                    ContactButtonLabel = "Compartilhar telefone"
                });
        }

        if (!bootstrap.LeadCreated)
        {
            return null;
        }

        var acknowledgement = string.Equals(bootstrap.BoardType, ProvidersBoardType, StringComparison.OrdinalIgnoreCase)
            ? "Recebi seu contato e ja registrei seu atendimento no funil de prestadores da ConsertaPraMim. Para agilizar, toque no botao abaixo e compartilhe seu telefone ou envie o numero por mensagem."
            : "Recebi sua mensagem e ja registrei seu atendimento na ConsertaPraMim. Para agilizar, toque no botao abaixo e compartilhe seu telefone ou envie o numero por mensagem.";

        return new TelegramAutomaticResponse(
            acknowledgement,
            new TelegramMessageSendOptions
            {
                RequestContactButton = true,
                ContactButtonLabel = "Compartilhar telefone"
            });
    }

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

    private static TelegramCapturedContact ResolveCapturedContact(TelegramMessage message)
    {
        var phone = ExtractPhone(message);
        var email = ExtractEmail(message);

        return new TelegramCapturedContact(
            Phone: phone ?? string.Empty,
            Email: email ?? string.Empty,
            SharedNativeContact: !string.IsNullOrWhiteSpace(phone) && message.Contact is not null);
    }

    private static string? ExtractPhone(TelegramMessage message)
    {
        if (message.Contact is not null &&
            IsTrustedSharedContact(message.Contact, message) &&
            TryNormalizePhone(message.Contact.PhoneNumber, out var sharedPhone))
        {
            return sharedPhone;
        }

        var candidateText = NormalizeOptionalText(message.Text) ?? NormalizeOptionalText(message.Caption);
        if (string.IsNullOrWhiteSpace(candidateText))
        {
            return null;
        }

        var match = PhoneRegex.Match(candidateText);
        if (!match.Success || !ShouldAcceptTextualPhone(candidateText))
        {
            return null;
        }

        return TryNormalizePhone(match.Value, out var normalizedPhone)
            ? normalizedPhone
            : null;
    }

    private static string? ExtractEmail(TelegramMessage message)
    {
        var candidateText = NormalizeOptionalText(message.Text) ?? NormalizeOptionalText(message.Caption);
        if (string.IsNullOrWhiteSpace(candidateText))
        {
            return null;
        }

        var match = EmailRegex.Match(candidateText);
        if (!match.Success || !ShouldAcceptTextualEmail(candidateText, match.Value))
        {
            return null;
        }

        return match.Value.Trim();
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

    private static bool IsTrustedSharedContact(TelegramContact contact, TelegramMessage message)
    {
        if (!contact.UserId.HasValue)
        {
            return true;
        }

        if (message.From?.Id > 0)
        {
            return contact.UserId.Value == message.From.Id;
        }

        return contact.UserId.Value == message.Chat?.Id;
    }

    private static bool ShouldAcceptTextualPhone(string value)
    {
        var normalized = NormalizeMessage(value) ?? string.Empty;
        var digitsOnly = new string(value.Where(char.IsDigit).ToArray());
        var compact = new string(value.Where(character => !char.IsWhiteSpace(character)).ToArray());
        var looksLikeDirectPhone = digitsOnly.Length is >= 10 and <= 15 && compact.Length <= 24;

        return looksLikeDirectPhone ||
               normalized.Contains("telefone", StringComparison.Ordinal) ||
               normalized.Contains("fone", StringComparison.Ordinal) ||
               normalized.Contains("contato", StringComparison.Ordinal) ||
               normalized.Contains("numero", StringComparison.Ordinal) ||
               normalized.Contains("whatsapp", StringComparison.Ordinal);
    }

    private static bool ShouldAcceptTextualEmail(string value, string email)
    {
        var trimmed = value.Trim();
        if (string.Equals(trimmed, email, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalized = NormalizeMessage(value) ?? string.Empty;
        return normalized.Contains("email", StringComparison.Ordinal) ||
               normalized.Contains("e-mail", StringComparison.Ordinal) ||
               normalized.Contains("meu email", StringComparison.Ordinal);
    }

    private static bool TryNormalizePhone(string? value, out string normalizedPhone)
    {
        normalizedPhone = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        if (digits.Length is < 10 or > 15)
        {
            return false;
        }

        normalizedPhone = trimmed.StartsWith('+')
            ? $"+{digits}"
            : digits;

        return true;
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

    private readonly record struct TelegramCapturedContact(
        string Phone,
        string Email,
        bool SharedNativeContact)
    {
        public bool HasPhone => !string.IsNullOrWhiteSpace(Phone);
        public bool HasEmail => !string.IsNullOrWhiteSpace(Email);
    }

    private readonly record struct TelegramAutomaticResponse(
        string Text,
        TelegramMessageSendOptions? Options);
}
