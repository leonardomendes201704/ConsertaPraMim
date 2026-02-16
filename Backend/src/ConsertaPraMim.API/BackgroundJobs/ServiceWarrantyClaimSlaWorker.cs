using ConsertaPraMim.Application.Interfaces;

namespace ConsertaPraMim.API.BackgroundJobs;

public class ServiceWarrantyClaimSlaWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ServiceWarrantyClaimSlaWorker> _logger;
    private readonly bool _enabled;
    private readonly TimeSpan _interval;
    private readonly int _batchSize;

    public ServiceWarrantyClaimSlaWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ServiceWarrantyClaimSlaWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        _enabled = ParseBoolean(configuration["ServiceAppointments:Warranty:EnableSlaWorker"], defaultValue: true);
        _interval = TimeSpan.FromSeconds(ParseInt(configuration["ServiceAppointments:Warranty:SlaWorkerIntervalSeconds"], 60, 5, 3600));
        _batchSize = ParseInt(configuration["ServiceAppointments:Warranty:SlaEscalationBatchSize"], 200, 1, 2000);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("ServiceWarrantyClaimSlaWorker disabled by configuration.");
            return;
        }

        _logger.LogInformation(
            "ServiceWarrantyClaimSlaWorker started. Interval={IntervalSeconds}s BatchSize={BatchSize}.",
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
                _logger.LogError(ex, "Unexpected error escalating warranty claims by SLA.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var appointmentService = scope.ServiceProvider.GetRequiredService<IServiceAppointmentService>();
        var escalated = await appointmentService.EscalateWarrantyClaimsBySlaAsync(_batchSize);
        if (escalated > 0)
        {
            _logger.LogInformation("Escalated {EscalatedCount} warranty claims by SLA.", escalated);
        }

        return escalated;
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
