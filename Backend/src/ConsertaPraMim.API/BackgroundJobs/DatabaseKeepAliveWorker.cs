using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace ConsertaPraMim.API.BackgroundJobs;

public class DatabaseKeepAliveWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatabaseKeepAliveWorker> _logger;
    private readonly bool _enabled;
    private readonly TimeSpan _interval;
    private readonly string _commandText;

    public DatabaseKeepAliveWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<DatabaseKeepAliveWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        _enabled = ParseBoolean(configuration["DatabaseKeepAlive:Enabled"], defaultValue: true);
        _interval = TimeSpan.FromSeconds(ParseInt(configuration["DatabaseKeepAlive:IntervalSeconds"], 60, 10, 3600));
        _commandText = string.IsNullOrWhiteSpace(configuration["DatabaseKeepAlive:CommandText"])
            ? "SELECT 1"
            : configuration["DatabaseKeepAlive:CommandText"]!;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("DatabaseKeepAliveWorker disabled by configuration.");
            return;
        }

        _logger.LogInformation(
            "DatabaseKeepAliveWorker started. Interval={IntervalSeconds}s Command={Command}.",
            _interval.TotalSeconds,
            _commandText);

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
                _logger.LogError(ex, "Unexpected error executing database keep-alive query.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    public async Task<long> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ConsertaPraMimDbContext>();

        var stopwatch = Stopwatch.StartNew();
        await dbContext.Database.ExecuteSqlRawAsync(_commandText, cancellationToken);
        stopwatch.Stop();

        _logger.LogDebug("Database keep-alive query succeeded in {ElapsedMs}ms.", stopwatch.ElapsedMilliseconds);
        return stopwatch.ElapsedMilliseconds;
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
