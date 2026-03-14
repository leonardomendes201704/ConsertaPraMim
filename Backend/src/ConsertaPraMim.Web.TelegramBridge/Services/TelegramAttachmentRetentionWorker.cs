using ConsertaPraMim.Web.TelegramBridge.Options;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public sealed class TelegramAttachmentRetentionWorker : BackgroundService
{
    private readonly ITelegramAttachmentStorage _attachmentStorage;
    private readonly TelegramBridgeOptions _options;
    private readonly ILogger<TelegramAttachmentRetentionWorker> _logger;

    public TelegramAttachmentRetentionWorker(
        ITelegramAttachmentStorage attachmentStorage,
        IOptions<TelegramBridgeOptions> options,
        ILogger<TelegramAttachmentRetentionWorker> logger)
    {
        _attachmentStorage = attachmentStorage;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.AttachmentRetentionEnabled)
        {
            _logger.LogInformation("TelegramAttachmentRetentionWorker desabilitado por configuracao.");
            return;
        }

        var interval = TimeSpan.FromMinutes(_options.AttachmentRetentionIntervalMinutes);
        _logger.LogInformation(
            "TelegramAttachmentRetentionWorker iniciado. IntervalMinutes={IntervalMinutes} RetentionDays={RetentionDays}.",
            interval.TotalMinutes,
            _options.AttachmentRetentionDays);

        using var timer = new PeriodicTimer(interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var purgeBeforeUtc = DateTime.UtcNow.AddDays(-Math.Max(1, _options.AttachmentRetentionDays));
                var deletedFiles = _attachmentStorage.PurgeExpiredFiles(purgeBeforeUtc);
                if (deletedFiles > 0)
                {
                    _logger.LogInformation(
                        "Retention de anexos Telegram executada. DeletedFiles={DeletedFiles} PurgeBeforeUtc={PurgeBeforeUtc}",
                        deletedFiles,
                        purgeBeforeUtc);
                }
                else
                {
                    _logger.LogDebug(
                        "Retention de anexos Telegram sem arquivos elegiveis. PurgeBeforeUtc={PurgeBeforeUtc}",
                        purgeBeforeUtc);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Erro inesperado na limpeza de anexos do Telegram Bridge.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
