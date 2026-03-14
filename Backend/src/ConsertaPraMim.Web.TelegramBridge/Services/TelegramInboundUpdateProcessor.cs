using ConsertaPraMim.Web.TelegramBridge.Models;
using ConsertaPraMim.Web.TelegramBridge.Options;
using ConsertaPraMim.Web.TelegramBridge.Security;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public sealed class TelegramInboundUpdateProcessor : ITelegramInboundUpdateProcessor
{
    private readonly ITelegramChatService _telegramChatService;
    private readonly TelegramAutomationOptions _automationOptions;
    private readonly ITelegramMessageAutomationClient _telegramMessageAutomationClient;
    private readonly ITelegramChatbotObservabilityService _observabilityService;
    private readonly ILogger<TelegramInboundUpdateProcessor> _logger;

    public TelegramInboundUpdateProcessor(
        ITelegramChatService telegramChatService,
        IOptions<TelegramAutomationOptions> automationOptions,
        ITelegramMessageAutomationClient telegramMessageAutomationClient,
        ITelegramChatbotObservabilityService observabilityService,
        ILogger<TelegramInboundUpdateProcessor> logger)
    {
        _telegramChatService = telegramChatService;
        _automationOptions = automationOptions.Value;
        _telegramMessageAutomationClient = telegramMessageAutomationClient;
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
            await TryMirrorInboundMessageAsync(update.Message, storedMessage, cancellationToken);
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

    private async Task TryMirrorInboundMessageAsync(
        TelegramMessage updateMessage,
        ChatMessageDto? storedMessage,
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
}
