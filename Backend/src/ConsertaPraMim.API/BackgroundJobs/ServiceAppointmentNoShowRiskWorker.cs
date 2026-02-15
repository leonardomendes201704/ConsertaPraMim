using ConsertaPraMim.Application.Interfaces;

namespace ConsertaPraMim.API.BackgroundJobs;

public class ServiceAppointmentNoShowRiskWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ServiceAppointmentNoShowRiskWorker> _logger;
    private readonly bool _enabled;
    private readonly TimeSpan _interval;
    private readonly int _batchSize;

    public ServiceAppointmentNoShowRiskWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ServiceAppointmentNoShowRiskWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        _enabled = ParseBoolean(configuration["ServiceAppointments:NoShowRisk:EnableWorker"], defaultValue: true);
        _interval = TimeSpan.FromSeconds(ParseInt(configuration["ServiceAppointments:NoShowRisk:WorkerIntervalSeconds"], 60, 5, 3600));
        _batchSize = ParseInt(configuration["ServiceAppointments:NoShowRisk:BatchSize"], 200, 1, 2000);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("ServiceAppointmentNoShowRiskWorker disabled by configuration.");
            return;
        }

        _logger.LogInformation(
            "ServiceAppointmentNoShowRiskWorker started. Interval={IntervalSeconds}s BatchSize={BatchSize}.",
            _interval.TotalSeconds,
            _batchSize);

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
                _logger.LogError(ex, "Unexpected error processing no-show risk evaluation.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var noShowRiskService = scope.ServiceProvider.GetRequiredService<IServiceAppointmentNoShowRiskService>();
        var operationalAlertService = scope.ServiceProvider.GetRequiredService<IAdminNoShowOperationalAlertService>();
        var processed = await noShowRiskService.EvaluateNoShowRiskAsync(_batchSize, cancellationToken);
        if (processed > 0)
        {
            _logger.LogInformation("Processed {ProcessedCount} no-show risk assessments.", processed);
        }

        var alertRecipients = await operationalAlertService.EvaluateAndNotifyAsync(cancellationToken);
        if (alertRecipients > 0)
        {
            _logger.LogInformation(
                "Operational no-show alert dispatched to {RecipientCount} recipient(s).",
                alertRecipients);
        }

        return processed;
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
