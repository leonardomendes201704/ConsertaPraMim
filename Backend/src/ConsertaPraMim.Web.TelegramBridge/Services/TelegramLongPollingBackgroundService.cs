using ConsertaPraMim.Web.TelegramBridge.Models;
using ConsertaPraMim.Web.TelegramBridge.Options;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public sealed class TelegramLongPollingBackgroundService : BackgroundService
{
    private readonly ITelegramBotApiClient _telegramBotApiClient;
    private readonly ITelegramInboundUpdateProcessor _updateProcessor;
    private readonly ILogger<TelegramLongPollingBackgroundService> _logger;
    private readonly TelegramBridgeOptions _options;
    private readonly ITelegramChatbotObservabilityService _observabilityService;

    public TelegramLongPollingBackgroundService(
        ITelegramBotApiClient telegramBotApiClient,
        ITelegramInboundUpdateProcessor updateProcessor,
        IOptions<TelegramBridgeOptions> options,
        ILogger<TelegramLongPollingBackgroundService> logger,
        ITelegramChatbotObservabilityService? observabilityService = null)
    {
        _telegramBotApiClient = telegramBotApiClient;
        _updateProcessor = updateProcessor;
        _logger = logger;
        _options = options.Value;
        _observabilityService = observabilityService ?? NullTelegramChatbotObservabilityService.Instance;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.UsesWebhookTransport())
        {
            _logger.LogInformation("TelegramBridge configurado em modo webhook. Worker de long polling desabilitado.");
            return;
        }

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

                    try
                    {
                        await _updateProcessor.ProcessAsync(update, "polling", stoppingToken);
                    }
                    catch (Exception exception)
                    {
                        _logger.LogDebug(exception, "Update Telegram sera ignorado apos falha controlada no polling.");
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

