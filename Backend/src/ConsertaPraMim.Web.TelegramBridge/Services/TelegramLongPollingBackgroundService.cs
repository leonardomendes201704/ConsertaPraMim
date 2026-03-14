using ConsertaPraMim.Web.TelegramBridge.Models;
using ConsertaPraMim.Web.TelegramBridge.Options;
using ConsertaPraMim.Web.TelegramBridge.Security;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public sealed class TelegramLongPollingBackgroundService : BackgroundService
{
    private readonly ITelegramBotApiClient _telegramBotApiClient;
    private readonly ITelegramChatService _telegramChatService;
    private readonly ILogger<TelegramLongPollingBackgroundService> _logger;
    private readonly TelegramBridgeOptions _options;
    private readonly TelegramAutomationOptions _automationOptions;
    private readonly ITelegramMessageAutomationClient _telegramMessageAutomationClient;
    private readonly ITelegramChatbotObservabilityService _observabilityService;

    public TelegramLongPollingBackgroundService(
        ITelegramBotApiClient telegramBotApiClient,
        ITelegramChatService telegramChatService,
        IOptions<TelegramBridgeOptions> options,
        IOptions<TelegramAutomationOptions> automationOptions,
        ITelegramMessageAutomationClient telegramMessageAutomationClient,
        ILogger<TelegramLongPollingBackgroundService> logger,
        ITelegramChatbotObservabilityService? observabilityService = null)
    {
        _telegramBotApiClient = telegramBotApiClient;
        _telegramChatService = telegramChatService;
        _logger = logger;
        _options = options.Value;
        _automationOptions = automationOptions.Value;
        _telegramMessageAutomationClient = telegramMessageAutomationClient;
        _observabilityService = observabilityService ?? NullTelegramChatbotObservabilityService.Instance;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        long offset = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_telegramBotApiClient.IsConfigured)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                continue;
            }

            try
            {
                var updates = await _telegramBotApiClient.GetUpdatesAsync(
                    offset,
                    _options.PollingTimeoutSeconds,
                    stoppingToken);

                if (updates.Count == 0)
                {
                    await Task.Delay(
                        Math.Clamp(_options.IdleDelayMilliseconds, 100, 5000),
                        stoppingToken);
                    continue;
                }

                foreach (var update in updates.OrderBy(item => item.UpdateId))
                {
                    offset = Math.Max(offset, update.UpdateId + 1);

                    if (update.Message is null)
                    {
                        continue;
                    }

                    try
                    {
                        var attachmentCount = (update.Message.Photo?.Count ?? 0)
                            + (update.Message.Document is null ? 0 : 1)
                            + (update.Message.Audio is null ? 0 : 1)
                            + (update.Message.Video is null ? 0 : 1);
                        _observabilityService.RecordInboundMessage(attachmentCount);

                        var storedMessage = await _telegramChatService.ReceiveFromTelegramAsync(update.Message, stoppingToken);
                        await TryMirrorInboundMessageAsync(update.Message, storedMessage, stoppingToken);
                    }
                    catch (Exception exception)
                    {
                        _observabilityService.RecordIncident(
                            stage: "telegram_polling_update",
                            errorCode: "telegram_update_processing_failed",
                            correlationId: null,
                            message: exception.Message);
                        _logger.LogWarning(
                            exception,
                            "Falha ao processar update Telegram {UpdateId}",
                            update.UpdateId);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                _observabilityService.RecordIncident(
                    stage: "telegram_polling_loop",
                    errorCode: "telegram_polling_failed",
                    correlationId: null,
                    message: exception.Message);
                _logger.LogError(exception, "Falha no polling de updates do Telegram.");
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
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

    private sealed class NullTelegramChatbotObservabilityService : ITelegramChatbotObservabilityService
    {
        public static readonly NullTelegramChatbotObservabilityService Instance = new();

        public void RecordInboundMessage(int attachmentCount)
        {
        }

        public void RecordOutboundMessage()
        {
        }

        public void RecordAiOutcome(TelegramChatbotAssistantReply reply, TelegramAiGatewayResult gatewayResult)
        {
        }

        public void RecordBusinessEvent(string eventName, bool success)
        {
        }

        public void RecordDependency(string dependency, bool success, long latencyMilliseconds, string? errorCode = null)
        {
        }

        public void RecordIncident(string stage, string errorCode, string? correlationId, string? message)
        {
        }

        public TelegramChatbotObservabilitySnapshotDto GetSnapshot()
        {
            return new TelegramChatbotObservabilitySnapshotDto(
                GeneratedAtUtc: DateTime.UtcNow,
                Environment: "unknown",
                Traffic: new TelegramChatbotTrafficMetricsDto(0, 0, 0),
                Ai: new TelegramChatbotAiMetricsDto(0, 0, 0, 0, 0, 0, 0, 0, 0),
                Business: new TelegramChatbotBusinessMetricsDto(0, 0, 0, 0, 0),
                Dependencies: [],
                TopErrors: [],
                RecentIncidents: []);
        }
    }
}

