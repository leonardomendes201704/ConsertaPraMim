using ConsertaPraMim.Application.Interfaces;

namespace ConsertaPraMim.API.BackgroundJobs;

public class ServiceAppointmentExpirationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ServiceAppointmentExpirationWorker> _logger;
    private readonly bool _enabled;
    private readonly TimeSpan _interval;
    private readonly int _batchSize;

    public ServiceAppointmentExpirationWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ServiceAppointmentExpirationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        _enabled = ParseBoolean(configuration["ServiceAppointments:EnableExpirationWorker"], defaultValue: true);
        _interval = TimeSpan.FromSeconds(ParseInt(configuration["ServiceAppointments:ExpirationWorkerIntervalSeconds"], 60, 5, 3600));
        _batchSize = ParseInt(configuration["ServiceAppointments:ExpirationBatchSize"], 200, 1, 2000);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("ServiceAppointmentExpirationWorker disabled by configuration.");
            return;
        }

        _logger.LogInformation(
            "ServiceAppointmentExpirationWorker started. Interval={IntervalSeconds}s BatchSize={BatchSize}.",
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
                _logger.LogError(ex, "Unexpected error processing service appointment expirations.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var appointmentService = scope.ServiceProvider.GetRequiredService<IServiceAppointmentService>();

        var expiredCount = await appointmentService.ExpirePendingAppointmentsAsync(_batchSize);
        if (expiredCount > 0)
        {
            _logger.LogInformation("Expired {ExpiredCount} service appointments.", expiredCount);
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
