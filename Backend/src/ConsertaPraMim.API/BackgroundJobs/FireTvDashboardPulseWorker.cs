using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace ConsertaPraMim.API.BackgroundJobs;

public sealed class FireTvDashboardPulseWorker : BackgroundService
{
    private readonly IHubContext<FireTvDashboardHub> _hubContext;
    private readonly IFireTvDashboardRuntimeSettings _runtimeSettings;
    private readonly ILogger<FireTvDashboardPulseWorker> _logger;

    public FireTvDashboardPulseWorker(
        IHubContext<FireTvDashboardHub> hubContext,
        IFireTvDashboardRuntimeSettings runtimeSettings,
        ILogger<FireTvDashboardPulseWorker> logger)
    {
        _hubContext = hubContext;
        _runtimeSettings = runtimeSettings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FireTvDashboardPulseWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var config = await _runtimeSettings.GetConfigAsync(stoppingToken);
                var intervalSeconds = Math.Clamp(config.SignalRPulseSeconds, 5, 60);

                if (config.Enabled && (config.ShowLandingView || config.ShowOperationsView))
                {
                    await _hubContext.Clients
                        .Group(FireTvDashboardHub.FireTvDashboardGroupName)
                        .SendAsync(
                            "FireTvDashboardPulse",
                            new
                            {
                                AtUtc = DateTime.UtcNow,
                                IntervalSeconds = intervalSeconds
                            },
                            stoppingToken);
                }

                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error sending Fire TV dashboard pulse.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
