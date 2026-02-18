using ConsertaPraMim.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.Threading;

namespace ConsertaPraMim.API.Services;

public interface IAdminMonitoringRealtimeNotifier
{
    Task NotifyUpdatedAsync(string source, int affectedCount = 0, CancellationToken cancellationToken = default);
}

public class AdminMonitoringRealtimeNotifier : IAdminMonitoringRealtimeNotifier
{
    private readonly IHubContext<AdminMonitoringHub> _hubContext;
    private readonly ILogger<AdminMonitoringRealtimeNotifier> _logger;
    private readonly bool _enabled;
    private readonly TimeSpan _minInterval;
    private long _nextAllowedTicksUtc;

    public AdminMonitoringRealtimeNotifier(
        IHubContext<AdminMonitoringHub> hubContext,
        IConfiguration configuration,
        ILogger<AdminMonitoringRealtimeNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;

        _enabled = ParseBool(configuration["Monitoring:Realtime:Enabled"], defaultValue: true);
        var minIntervalSeconds = Math.Clamp(ParseInt(configuration["Monitoring:Realtime:MinIntervalSeconds"], 3), 1, 60);
        _minInterval = TimeSpan.FromSeconds(minIntervalSeconds);
        _nextAllowedTicksUtc = 0;
    }

    public async Task NotifyUpdatedAsync(string source, int affectedCount = 0, CancellationToken cancellationToken = default)
    {
        if (!_enabled)
        {
            return;
        }

        var nowTicks = DateTime.UtcNow.Ticks;
        var previousTicks = Volatile.Read(ref _nextAllowedTicksUtc);
        if (nowTicks < previousTicks)
        {
            return;
        }

        var nextTicks = nowTicks + _minInterval.Ticks;
        var original = Interlocked.CompareExchange(ref _nextAllowedTicksUtc, nextTicks, previousTicks);
        if (original != previousTicks)
        {
            return;
        }

        try
        {
            await _hubContext.Clients.Group(AdminMonitoringHub.AdminGroupName).SendAsync(
                "MonitoringUpdated",
                new
                {
                    Source = string.IsNullOrWhiteSpace(source) ? "unknown" : source.Trim(),
                    AffectedCount = affectedCount < 0 ? 0 : affectedCount,
                    AtUtc = DateTime.UtcNow
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Falha ao emitir evento realtime de monitoramento.");
        }
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
