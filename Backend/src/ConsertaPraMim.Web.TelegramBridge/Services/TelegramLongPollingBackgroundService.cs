using ConsertaPraMim.Web.TelegramBridge.Models;
using ConsertaPraMim.Web.TelegramBridge.Options;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public sealed class TelegramLongPollingBackgroundService : BackgroundService
{
    private readonly ITelegramBotApiClient _telegramBotApiClient;
    private readonly ITelegramChatService _telegramChatService;
    private readonly ILogger<TelegramLongPollingBackgroundService> _logger;
    private readonly TelegramBridgeOptions _options;
    private readonly ITelegramChatbotObservabilityService _observabilityService;

    public TelegramLongPollingBackgroundService(
        ITelegramBotApiClient telegramBotApiClient,
        ITelegramChatService telegramChatService,
        IOptions<TelegramBridgeOptions> options,
        ILogger<TelegramLongPollingBackgroundService> logger,
        ITelegramChatbotObservabilityService? observabilityService = null)
    {
        _telegramBotApiClient = telegramBotApiClient;
        _telegramChatService = telegramChatService;
        _logger = logger;
        _options = options.Value;
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

                        await _telegramChatService.ReceiveFromTelegramAsync(update.Message, stoppingToken);
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

