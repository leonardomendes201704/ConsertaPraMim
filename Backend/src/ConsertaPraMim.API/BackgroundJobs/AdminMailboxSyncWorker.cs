using ConsertaPraMim.Application.Interfaces;

namespace ConsertaPraMim.API.BackgroundJobs;

public class AdminMailboxSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AdminMailboxSyncWorker> _logger;
    private readonly bool _enabled;
    private readonly TimeSpan _interval;

    public AdminMailboxSyncWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<AdminMailboxSyncWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _enabled = ParseBool(configuration["AdminMailbox:SyncWorker:Enabled"], defaultValue: true);
        var intervalSeconds = Math.Clamp(ParseInt(configuration["AdminMailbox:SyncWorker:IntervalSeconds"], 120), 30, 3600);
        _interval = TimeSpan.FromSeconds(intervalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("AdminMailboxSyncWorker disabled by configuration.");
            return;
        }

        _logger.LogInformation(
            "AdminMailboxSyncWorker started. Interval={IntervalSeconds}s",
            _interval.TotalSeconds);

        using var timer = new PeriodicTimer(_interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected failure in AdminMailboxSyncWorker.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var mailboxService = scope.ServiceProvider.GetRequiredService<IAdminMailboxService>();
        var result = await mailboxService.SyncInboxAsync(notifyAdmins: true, cancellationToken);
        if (!result.Success)
        {
            _logger.LogWarning(
                "Mailbox sync failed. ErrorCode={ErrorCode} Error={ErrorMessage}",
                result.ErrorCode,
                result.ErrorMessage);
            return;
        }

        _logger.LogDebug(
            "Mailbox sync completed. Fetched={Fetched} New={New}",
            result.FetchedCount,
            result.NewMessagesCount);
    }

    private static bool ParseBool(string? raw, bool defaultValue)
    {
        return bool.TryParse(raw, out var parsed)
            ? parsed
            : defaultValue;
    }

    private static int ParseInt(string? raw, int defaultValue)
    {
        return int.TryParse(raw, out var parsed)
            ? parsed
            : defaultValue;
    }
}
