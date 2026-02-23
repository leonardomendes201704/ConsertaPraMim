using System.Text.RegularExpressions;

namespace ConsertaPraMim.LoadTest.Wpf.Services;

internal sealed class EndpointAccumulator
{
    public EndpointAccumulator(string endpoint) => Endpoint = endpoint;

    public string Endpoint { get; }
    public int Hits { get; set; }
    public int Errors { get; set; }
    public List<double> Durations { get; } = [];
}

internal sealed class MetricsCollector
{
    private readonly object _lock = new();
    private readonly DateTimeOffset _startedAtUtc;
    private int _totalRequests;
    private int _successRequests;
    private int _failedRequests;

    private readonly List<double> _allDurations = [];
    private readonly Dictionary<int, int> _statusCounts = [];
    private readonly Dictionary<string, int> _exceptionCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, EndpointAccumulator> _endpointMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, int> _requestsPerSecond = [];

    private readonly Dictionary<string, int> _errorCatalog = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _errorCatalogEndpoints = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FailureSample> _failureSamples = [];

    public MetricsCollector(DateTimeOffset startedAtUtc)
    {
        _startedAtUtc = startedAtUtc;
    }

    public void Record(
        string endpointKey,
        string method,
        string path,
        string clientId,
        string correlationId,
        int? statusCode,
        double durationMs,
        string? exceptionType,
        string errorMessage,
        bool isFailure)
    {
        lock (_lock)
        {
            _totalRequests++;
            _allDurations.Add(durationMs);

            var secondBucket = (int)Math.Max(0, Math.Floor((DateTimeOffset.UtcNow - _startedAtUtc).TotalSeconds));
            _requestsPerSecond.TryGetValue(secondBucket, out var currentSecondCount);
            _requestsPerSecond[secondBucket] = currentSecondCount + 1;

            if (!_endpointMap.TryGetValue(endpointKey, out var endpoint))
            {
                endpoint = new EndpointAccumulator(endpointKey);
                _endpointMap[endpointKey] = endpoint;
            }

            endpoint.Hits++;
            endpoint.Durations.Add(durationMs);

            if (statusCode.HasValue)
            {
                _statusCounts.TryGetValue(statusCode.Value, out var statusCount);
                _statusCounts[statusCode.Value] = statusCount + 1;
            }

            if (isFailure)
            {
                _failedRequests++;
                endpoint.Errors++;
                if (!string.IsNullOrWhiteSpace(exceptionType))
                {
                    _exceptionCounts.TryGetValue(exceptionType, out var count);
                    _exceptionCounts[exceptionType] = count + 1;
                }

                var normalized = NormalizeErrorMessage(string.IsNullOrWhiteSpace(errorMessage) ? exceptionType ?? "request_failed" : errorMessage);
                _errorCatalog.TryGetValue(normalized, out var errorCount);
                _errorCatalog[normalized] = errorCount + 1;
                if (!_errorCatalogEndpoints.TryGetValue(normalized, out var endpointSet))
                {
                    endpointSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _errorCatalogEndpoints[normalized] = endpointSet;
                }

                endpointSet.Add(endpointKey);

                if (_failureSamples.Count < 20)
                {
                    _failureSamples.Add(new FailureSample
                    {
                        TimestampUtc = DateTimeOffset.UtcNow.ToString("o"),
                        ClientId = clientId,
                        CorrelationId = correlationId,
                        Endpoint = endpointKey,
                        Method = method,
                        Path = path,
                        StatusCode = statusCode,
                        DurationMs = Math.Round(durationMs, 2),
                        ErrorType = exceptionType ?? (statusCode.HasValue ? $"HTTP_{statusCode.Value}" : "Error"),
                        ErrorMessage = normalized
                    });
                }
            }
            else
            {
                _successRequests++;
            }
        }
    }

    public LoadTestLiveSnapshot BuildSnapshot(
        string runId,
        string scenario,
        string baseUrl,
        int vus,
        int plannedDurationSeconds,
        string status,
        bool isCompleted,
        string errorMessage)
    {
        lock (_lock)
        {
            var elapsedSeconds = Math.Max((DateTimeOffset.UtcNow - _startedAtUtc).TotalSeconds, 0.0);
            var safeElapsed = Math.Max(elapsedSeconds, 0.001);
            var progressPercent = Math.Min(100, elapsedSeconds / Math.Max(plannedDurationSeconds, 1) * 100);

            var minLatency = _allDurations.Count > 0 ? _allDurations.Min() : 0;
            var avgLatency = _allDurations.Count > 0 ? _allDurations.Average() : 0;
            var maxLatency = _allDurations.Count > 0 ? _allDurations.Max() : 0;
            var p50 = Percentile(_allDurations, 50);
            var p95 = Percentile(_allDurations, 95);
            var p99 = Percentile(_allDurations, 99);

            var errorRate = _totalRequests > 0
                ? (double)_failedRequests / _totalRequests * 100
                : 0;

            var currentSecond = (int)Math.Floor(elapsedSeconds);
            var currentRps = _requestsPerSecond.TryGetValue(currentSecond, out var bucket) ? bucket : 0;
            var peakRps = _requestsPerSecond.Count == 0 ? 0 : _requestsPerSecond.Values.Max();

            var statusCodes = _statusCounts
                .OrderByDescending(x => x.Value)
                .Select(x => new StatusCodeStat
                {
                    StatusCode = x.Key,
                    Count = x.Value,
                    Percentage = _totalRequests > 0 ? Math.Round((double)x.Value / _totalRequests * 100, 2) : 0
                })
                .ToList();

            var exceptions = _exceptionCounts
                .OrderByDescending(x => x.Value)
                .Select(x => new ExceptionStat
                {
                    Type = x.Key,
                    Count = x.Value
                })
                .ToList();

            var endpointStats = _endpointMap.Values
                .Select(ToEndpointStat)
                .ToList();
            var topByHits = endpointStats.OrderByDescending(x => x.Hits).Take(12).ToList();
            var topByP95 = endpointStats.OrderByDescending(x => x.P95LatencyMs).Take(12).ToList();
            var timeSeries = _requestsPerSecond
                .OrderBy(x => x.Key)
                .Select(x => new TimeSeriesPoint { Second = x.Key, Requests = x.Value })
                .ToList();

            return new LoadTestLiveSnapshot
            {
                RunId = runId,
                Scenario = scenario,
                BaseUrl = baseUrl,
                StartedAtUtc = _startedAtUtc.ToString("o"),
                GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("o"),
                Status = status,
                IsCompleted = isCompleted,
                ErrorMessage = errorMessage,
                Vus = vus,
                PlannedDurationSeconds = plannedDurationSeconds,
                ElapsedSeconds = Math.Round(elapsedSeconds, 2),
                ProgressPercent = Math.Round(progressPercent, 2),
                Summary = new LoadTestLiveSummary
                {
                    TotalRequests = _totalRequests,
                    SuccessfulRequests = _successRequests,
                    FailedRequests = _failedRequests,
                    ErrorRatePercent = Math.Round(errorRate, 2),
                    RpsCurrent = currentRps,
                    RpsAvg = Math.Round(_totalRequests / safeElapsed, 2),
                    RpsPeak = peakRps
                },
                LatencyMs = new LoadTestLiveLatency
                {
                    Min = Math.Round(minLatency, 2),
                    Avg = Math.Round(avgLatency, 2),
                    Max = Math.Round(maxLatency, 2),
                    P50 = Math.Round(p50, 2),
                    P95 = Math.Round(p95, 2),
                    P99 = Math.Round(p99, 2)
                },
                StatusCodes = statusCodes,
                Exceptions = exceptions,
                TopEndpointsByHits = topByHits,
                TopEndpointsByP95 = topByP95,
                RequestsPerSecond = timeSeries
            };
        }
    }

    public LoadTestReport BuildReport(
        string runId,
        string scenarioName,
        string baseUrl,
        DateTimeOffset startedAtUtc,
        DateTimeOffset finishedAtUtc,
        double durationSeconds,
        object scenarioConfig)
    {
        var snapshot = BuildSnapshot(
            runId,
            scenarioName,
            baseUrl,
            vus: 0,
            plannedDurationSeconds: (int)Math.Ceiling(durationSeconds),
            status: "completed",
            isCompleted: true,
            errorMessage: string.Empty);

        lock (_lock)
        {
            var topErrors = _errorCatalog
                .OrderByDescending(x => x.Value)
                .Take(12)
                .Select(x => new ErrorCatalogStat
                {
                    Message = x.Key,
                    Count = x.Value,
                    Endpoints = (_errorCatalogEndpoints.TryGetValue(x.Key, out var set)
                        ? set.OrderBy(y => y, StringComparer.OrdinalIgnoreCase).ToArray()
                        : Array.Empty<string>())
                })
                .ToArray();

            return new LoadTestReport
            {
                RunId = runId,
                Scenario = scenarioName,
                BaseUrl = baseUrl,
                StartedAtUtc = startedAtUtc.ToString("o"),
                FinishedAtUtc = finishedAtUtc.ToString("o"),
                DurationSeconds = Math.Round(durationSeconds, 2),
                Summary = snapshot.Summary,
                LatencyMs = snapshot.LatencyMs,
                StatusCodes = snapshot.StatusCodes,
                Exceptions = snapshot.Exceptions,
                TopEndpointsByHits = snapshot.TopEndpointsByHits,
                TopEndpointsByP95 = snapshot.TopEndpointsByP95,
                TopErrors = topErrors,
                FailureSamples = _failureSamples.ToArray(),
                ScenarioConfig = scenarioConfig
            };
        }
    }

    private static EndpointLiveStat ToEndpointStat(EndpointAccumulator endpoint)
    {
        var hits = endpoint.Hits;
        var errors = endpoint.Errors;
        var avg = endpoint.Durations.Count > 0 ? endpoint.Durations.Average() : 0;
        var p95 = Percentile(endpoint.Durations, 95);
        return new EndpointLiveStat
        {
            Endpoint = endpoint.Endpoint,
            Hits = hits,
            Errors = errors,
            ErrorRatePercent = hits > 0 ? Math.Round((double)errors / hits * 100, 2) : 0,
            AvgLatencyMs = Math.Round(avg, 2),
            P95LatencyMs = Math.Round(p95, 2)
        };
    }

    private static double Percentile(IReadOnlyList<double> values, double percentile)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        if (percentile <= 0)
        {
            return values.Min();
        }

        if (percentile >= 100)
        {
            return values.Max();
        }

        var sorted = values.OrderBy(x => x).ToArray();
        var rank = (sorted.Length - 1) * (percentile / 100.0);
        var low = (int)Math.Floor(rank);
        var high = (int)Math.Ceiling(rank);
        if (low == high)
        {
            return sorted[low];
        }

        var fraction = rank - low;
        return sorted[low] + (sorted[high] - sorted[low]) * fraction;
    }

    private static string NormalizeErrorMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "unknown_error";
        }

        var normalized = message.Trim();
        normalized = Regex.Replace(normalized, @"[\r\n\t]+", " ");
        normalized = Regex.Replace(normalized, @"\s+", " ");
        normalized = Regex.Replace(normalized, @"\b[0-9a-f]{8}\-[0-9a-f]{4}\-[0-9a-f]{4}\-[0-9a-f]{4}\-[0-9a-f]{12}\b", "{guid}", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\b\d{2,}\b", "{n}");
        if (normalized.Length > 220)
        {
            normalized = normalized[..217] + "...";
        }

        return normalized;
    }
}

