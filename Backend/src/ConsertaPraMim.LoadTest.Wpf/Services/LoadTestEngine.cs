using System.Net;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace ConsertaPraMim.LoadTest.Wpf.Services;

public sealed class LoadTestEngine
{
    private readonly Action<string> _logger;

    public LoadTestEngine(Action<string>? logger = null)
    {
        _logger = logger ?? (_ => { });
    }

    public async Task<LoadTestResult> RunAsync(
        LoadTestRunOptions options,
        IProgress<LoadTestLiveSnapshot>? progress,
        CancellationToken cancellationToken)
    {
        var runId = Guid.NewGuid().ToString();
        var startedAtUtc = DateTimeOffset.UtcNow;
        var metrics = new MetricsCollector(startedAtUtc);
        var stopAtUtc = startedAtUtc.AddSeconds(options.DurationSeconds);
        var refreshInterval = TimeSpan.FromSeconds(Math.Max(0.3, options.RefreshSeconds));

        _logger($"Run {runId} iniciado. Scenario={options.ScenarioName} BaseUrl={options.BaseUrl}");

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var timer = new PeriodicTimer(refreshInterval);

        var progressTask = Task.Run(async () =>
        {
            try
            {
                while (await timer.WaitForNextTickAsync(linkedCts.Token).ConfigureAwait(false))
                {
                    progress?.Report(metrics.BuildSnapshot(
                        runId,
                        options.ScenarioName,
                        options.BaseUrl,
                        options.Vus,
                        options.DurationSeconds,
                        "running",
                        false,
                        string.Empty));
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, linkedCts.Token);

        var workerTasks = Enumerable.Range(1, options.Vus)
            .Select(vuIndex => RunVuWorkerAsync(vuIndex, options, metrics, stopAtUtc, cancellationToken))
            .ToArray();

        try
        {
            await Task.WhenAll(workerTasks).ConfigureAwait(false);
            _logger($"Run {runId} concluido.");
        }
        finally
        {
            linkedCts.Cancel();
            try
            {
                await progressTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        var finishedAtUtc = DateTimeOffset.UtcNow;
        var elapsedSeconds = Math.Max((finishedAtUtc - startedAtUtc).TotalSeconds, 0.001);
        var finalSnapshot = metrics.BuildSnapshot(
            runId,
            options.ScenarioName,
            options.BaseUrl,
            options.Vus,
            options.DurationSeconds,
            "completed",
            true,
            string.Empty);
        progress?.Report(finalSnapshot);

        var report = metrics.BuildReport(
            runId,
            options.ScenarioName,
            options.BaseUrl,
            startedAtUtc,
            finishedAtUtc,
            elapsedSeconds,
            options.Scenario);

        var outputPaths = SaveReports(report, options.OutputDirectory);
        return new LoadTestResult
        {
            Report = report,
            FinalSnapshot = finalSnapshot,
            JsonPath = outputPaths.JsonPath,
            TxtPath = outputPaths.TxtPath,
            HtmlPath = outputPaths.HtmlPath
        };
    }

    private async Task RunVuWorkerAsync(
        int vuIndex,
        LoadTestRunOptions options,
        MetricsCollector metrics,
        DateTimeOffset stopAtUtc,
        CancellationToken cancellationToken)
    {
        if (options.RampUpSeconds > 0 && options.Vus > 1)
        {
            var delaySeconds = (options.RampUpSeconds / Math.Max(options.Vus - 1, 1)) * (vuIndex - 1);
            if (delaySeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken).ConfigureAwait(false);
            }
        }

        using var session = new VuSession(vuIndex, options, _logger);
        while (DateTimeOffset.UtcNow < stopAtUtc && !cancellationToken.IsCancellationRequested)
        {
            var endpoint = session.ChooseEndpoint();
            await session.ExecuteRequestAsync(endpoint, metrics, cancellationToken).ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromMilliseconds(session.NextThinkDelay()), cancellationToken).ConfigureAwait(false);
        }
    }

    private static (string JsonPath, string TxtPath, string HtmlPath) SaveReports(LoadTestReport report, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        var runId = report.RunId;
        var jsonPath = Path.Combine(outputDirectory, $"loadtest-report-{runId}.json");
        var txtPath = Path.Combine(outputDirectory, $"loadtest-summary-{runId}.txt");
        var htmlPath = Path.Combine(outputDirectory, $"loadtest-report-{runId}.html");

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(jsonPath, json, Encoding.UTF8);
        File.WriteAllText(Path.Combine(outputDirectory, "loadtest-report-latest.json"), json, Encoding.UTF8);

        var text = new StringBuilder()
            .AppendLine($"Run ID: {report.RunId}")
            .AppendLine($"Scenario: {report.Scenario}")
            .AppendLine($"Base URL: {report.BaseUrl}")
            .AppendLine($"Started: {report.StartedAtUtc}")
            .AppendLine($"Finished: {report.FinishedAtUtc}")
            .AppendLine($"Duration(s): {report.DurationSeconds}")
            .AppendLine()
            .AppendLine($"Total: {report.Summary.TotalRequests}")
            .AppendLine($"Success: {report.Summary.SuccessfulRequests}")
            .AppendLine($"Failed: {report.Summary.FailedRequests}")
            .AppendLine($"ErrorRate(%): {report.Summary.ErrorRatePercent}")
            .AppendLine($"RPS avg/peak: {report.Summary.RpsAvg}/{report.Summary.RpsPeak}")
            .AppendLine($"Latency p95/p99: {report.LatencyMs.P95}/{report.LatencyMs.P99}")
            .ToString();
        File.WriteAllText(txtPath, text, Encoding.UTF8);
        File.WriteAllText(Path.Combine(outputDirectory, "loadtest-summary-latest.txt"), text, Encoding.UTF8);

        var html = $$"""
                     <!doctype html>
                     <html lang="en">
                     <head>
                       <meta charset="utf-8" />
                       <title>Load Test Report {{report.RunId}}</title>
                       <style>
                         body { font-family: Segoe UI, Arial, sans-serif; margin: 20px; color: #1f2937; }
                         .card { border: 1px solid #d1d5db; border-radius: 8px; padding: 10px; margin-bottom: 10px; background: #f8fafc; }
                         table { width:100%; border-collapse: collapse; margin-top: 14px; }
                         th,td { border: 1px solid #d1d5db; padding: 6px; text-align:left; }
                         th { background: #eff6ff; }
                       </style>
                     </head>
                     <body>
                       <h1>ConsertaPraMim Load Test Report</h1>
                       <div class="card">
                         <strong>RunId:</strong> {{WebUtility.HtmlEncode(report.RunId)}}<br/>
                         <strong>Scenario:</strong> {{WebUtility.HtmlEncode(report.Scenario)}}<br/>
                         <strong>Base URL:</strong> {{WebUtility.HtmlEncode(report.BaseUrl)}}<br/>
                         <strong>Duration(s):</strong> {{report.DurationSeconds}}
                       </div>
                       <div class="card">
                         <strong>Total:</strong> {{report.Summary.TotalRequests}} |
                         <strong>Success:</strong> {{report.Summary.SuccessfulRequests}} |
                         <strong>Failed:</strong> {{report.Summary.FailedRequests}} |
                         <strong>Error(%):</strong> {{report.Summary.ErrorRatePercent}}
                       </div>
                       <h2>Top endpoints by hits</h2>
                       <table>
                         <thead><tr><th>Endpoint</th><th>Hits</th><th>Errors</th><th>P95(ms)</th></tr></thead>
                         <tbody>
                         {{string.Join(Environment.NewLine, report.TopEndpointsByHits.Select(item => $"<tr><td>{WebUtility.HtmlEncode(item.Endpoint)}</td><td>{item.Hits}</td><td>{item.Errors}</td><td>{item.P95LatencyMs}</td></tr>"))}}
                         </tbody>
                       </table>
                     </body>
                     </html>
                     """;
        File.WriteAllText(htmlPath, html, Encoding.UTF8);
        File.WriteAllText(Path.Combine(outputDirectory, "loadtest-report-latest.html"), html, Encoding.UTF8);

        return (jsonPath, txtPath, htmlPath);
    }
}
