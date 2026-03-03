using ConsertaPraMim.Web.TelegramBridge.Options;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public sealed class TelegramLongPollingBackgroundService : BackgroundService
{
    private readonly ITelegramBotApiClient _telegramBotApiClient;
    private readonly ITelegramChatService _telegramChatService;
    private readonly ILogger<TelegramLongPollingBackgroundService> _logger;
    private readonly TelegramBridgeOptions _options;

    public TelegramLongPollingBackgroundService(
        ITelegramBotApiClient telegramBotApiClient,
        ITelegramChatService telegramChatService,
        IOptions<TelegramBridgeOptions> options,
        ILogger<TelegramLongPollingBackgroundService> logger)
    {
        _telegramBotApiClient = telegramBotApiClient;
        _telegramChatService = telegramChatService;
        _logger = logger;
        _options = options.Value;
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
                        await _telegramChatService.ReceiveFromTelegramAsync(update.Message, stoppingToken);
                    }
                    catch (Exception exception)
                    {
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
                _logger.LogError(exception, "Falha no polling de updates do Telegram.");
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }
}
