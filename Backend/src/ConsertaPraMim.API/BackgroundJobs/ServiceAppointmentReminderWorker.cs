using ConsertaPraMim.Application.Interfaces;

namespace ConsertaPraMim.API.BackgroundJobs;

public class ServiceAppointmentReminderWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ServiceAppointmentReminderWorker> _logger;
    private readonly bool _enabled;
    private readonly TimeSpan _interval;
    private readonly int _batchSize;

    public ServiceAppointmentReminderWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ServiceAppointmentReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        _enabled = ParseBoolean(configuration["ServiceAppointments:Reminders:EnableWorker"], defaultValue: true);
        _interval = TimeSpan.FromSeconds(ParseInt(configuration["ServiceAppointments:Reminders:WorkerIntervalSeconds"], 30, 5, 3600));
        _batchSize = ParseInt(configuration["ServiceAppointments:Reminders:BatchSize"], 200, 1, 2000);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("ServiceAppointmentReminderWorker disabled by configuration.");
            return;
        }

        _logger.LogInformation(
            "ServiceAppointmentReminderWorker started. Interval={IntervalSeconds}s BatchSize={BatchSize}.",
            _interval.TotalSeconds,
            _batchSize);

        using var timer = new PeriodicTimer(_interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing appointment reminders.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var reminderService = scope.ServiceProvider.GetRequiredService<IAppointmentReminderService>();
        var processed = await reminderService.ProcessDueRemindersAsync(_batchSize, cancellationToken);
        if (processed > 0)
        {
            _logger.LogInformation("Processed {ProcessedCount} appointment reminder dispatches.", processed);
        }
    }

    private static int ParseInt(string? raw, int defaultValue, int min, int max)
    {
        if (!int.TryParse(raw, out var parsed))
        {
            return defaultValue;
        }

        return Math.Clamp(parsed, min, max);
    }

    private static bool ParseBoolean(string? raw, bool defaultValue)
    {
        if (!bool.TryParse(raw, out var parsed))
        {
            return defaultValue;
        }

        return parsed;
    }
}
