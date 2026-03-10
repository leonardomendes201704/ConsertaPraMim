using ConsertaPraMim.Web.TelegramBridge.Models;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public sealed class TelegramChatbotObservabilityService : ITelegramChatbotObservabilityService
{
    private const int LatencySampleLimit = 400;
    private const int IncidentSampleLimit = 100;

    private readonly object _sync = new();
    private readonly string _environmentName;

    private long _inboundMessages;
    private long _outboundMessages;
    private long _messagesWithAttachments;

    private long _aiRequests;
    private long _aiFallbacks;
    private long _aiFailures;
    private long _guardrailInterventions;
    private long _humanHandoffs;
    private long _totalTokens;
    private decimal _estimatedCostUsd;
    private readonly List<double> _aiLatencies = [];

    private long _triageRequestsOpened;
    private long _schedulingAttempts;
    private long _schedulingConfirmed;
    private long _schedulingFailures;
    private long _queryRequests;

    private readonly Dictionary<string, DependencyStats> _dependencyStats =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, long> _errors =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Queue<TelegramChatbotRecentIncidentDto> _recentIncidents = new();

    public TelegramChatbotObservabilityService(IWebHostEnvironment environment)
    {
        _environmentName = environment.EnvironmentName;
    }

    public void RecordInboundMessage(int attachmentCount)
    {
        lock (_sync)
        {
            _inboundMessages++;
            if (attachmentCount > 0)
            {
                _messagesWithAttachments++;
            }
        }
    }

    public void RecordOutboundMessage()
    {
        lock (_sync)
        {
            _outboundMessages++;
        }
    }

    public void RecordAiOutcome(TelegramChatbotAssistantReply reply, TelegramAiGatewayResult gatewayResult)
    {
        lock (_sync)
        {
            _aiRequests++;

            if (reply.UsedFallback)
            {
                _aiFallbacks++;
            }

            if (!gatewayResult.Success)
            {
                _aiFailures++;
            }

            if (reply.TotalTokens.HasValue && reply.TotalTokens.Value > 0)
            {
                _totalTokens += reply.TotalTokens.Value;
                _estimatedCostUsd += EstimateCostUsd(
                    reply.ModelName,
                    reply.PromptTokens,
                    reply.CompletionTokens);
            }

            if (reply.NextStep.StartsWith("handoff_human", StringComparison.OrdinalIgnoreCase))
            {
                _humanHandoffs++;
            }

            if (reply.NextStep.StartsWith("handoff_", StringComparison.OrdinalIgnoreCase))
            {
                _guardrailInterventions++;
            }

            var latency = gatewayResult.LatencyMilliseconds > 0
                ? gatewayResult.LatencyMilliseconds
                : reply.LatencyMilliseconds;

            if (latency > 0)
            {
                AppendLatency(_aiLatencies, latency);
            }
        }
    }

    public void RecordBusinessEvent(string eventName, bool success)
    {
        lock (_sync)
        {
            switch (eventName)
            {
                case "triage_request_opened":
                    if (success)
                    {
                        _triageRequestsOpened++;
                    }
                    break;
                case "scheduling_attempt":
                    _schedulingAttempts++;
                    if (success)
                    {
                        _schedulingConfirmed++;
                    }
                    else
                    {
                        _schedulingFailures++;
                    }
                    break;
                case "query_request":
                    _queryRequests++;
                    break;
                case "guardrail_intervention":
                    _guardrailInterventions++;
                    break;
                case "human_handoff":
                    _humanHandoffs++;
                    break;
            }
        }
    }

    public void RecordDependency(string dependency, bool success, long latencyMilliseconds, string? errorCode = null)
    {
        if (string.IsNullOrWhiteSpace(dependency))
        {
            return;
        }

        lock (_sync)
        {
            if (!_dependencyStats.TryGetValue(dependency, out var stats))
            {
                stats = new DependencyStats();
                _dependencyStats[dependency] = stats;
            }

            stats.Calls++;
            if (success)
            {
                stats.Successes++;
            }
            else
            {
                stats.Failures++;
            }

            if (latencyMilliseconds > 0)
            {
                AppendLatency(stats.Latencies, latencyMilliseconds);
            }

            if (!success && !string.IsNullOrWhiteSpace(errorCode))
            {
                IncrementError(errorCode);
            }
        }
    }

    public void RecordIncident(string stage, string errorCode, string? correlationId, string? message)
    {
        lock (_sync)
        {
            IncrementError(errorCode);

            _recentIncidents.Enqueue(new TelegramChatbotRecentIncidentDto(
                OccurredAtUtc: DateTime.UtcNow,
                Stage: string.IsNullOrWhiteSpace(stage) ? "unknown" : stage,
                ErrorCode: string.IsNullOrWhiteSpace(errorCode) ? "unknown" : errorCode,
                CorrelationId: correlationId,
                Message: message));

            while (_recentIncidents.Count > IncidentSampleLimit)
            {
                _recentIncidents.Dequeue();
            }
        }
    }

    public TelegramChatbotObservabilitySnapshotDto GetSnapshot()
    {
        lock (_sync)
        {
            var dependencies = _dependencyStats
                .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .Select(item => new TelegramChatbotDependencyMetricsDto(
                    Dependency: item.Key,
                    Calls: item.Value.Calls,
                    Successes: item.Value.Successes,
                    Failures: item.Value.Failures,
                    AvgLatencyMs: CalculateAverage(item.Value.Latencies),
                    P95LatencyMs: CalculatePercentile(item.Value.Latencies, 95)))
                .ToList();

            var topErrors = _errors
                .OrderByDescending(item => item.Value)
                .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .Select(item => new TelegramChatbotErrorMetricsDto(item.Key, item.Value))
                .ToList();

            var incidents = _recentIncidents
                .OrderByDescending(item => item.OccurredAtUtc)
                .ToList();

            return new TelegramChatbotObservabilitySnapshotDto(
                GeneratedAtUtc: DateTime.UtcNow,
                Environment: _environmentName,
                Traffic: new TelegramChatbotTrafficMetricsDto(
                    InboundMessages: _inboundMessages,
                    OutboundMessages: _outboundMessages,
                    MessagesWithAttachments: _messagesWithAttachments),
                Ai: new TelegramChatbotAiMetricsDto(
                    Requests: _aiRequests,
                    Fallbacks: _aiFallbacks,
                    Failures: _aiFailures,
                    GuardrailInterventions: _guardrailInterventions,
                    HumanHandoffs: _humanHandoffs,
                    AvgLatencyMs: CalculateAverage(_aiLatencies),
                    P95LatencyMs: CalculatePercentile(_aiLatencies, 95),
                    Tokens: _totalTokens,
                    EstimatedCostUsd: decimal.Round(_estimatedCostUsd, 6, MidpointRounding.AwayFromZero)),
                Business: new TelegramChatbotBusinessMetricsDto(
                    TriageRequestsOpened: _triageRequestsOpened,
                    SchedulingAttempts: _schedulingAttempts,
                    SchedulingConfirmed: _schedulingConfirmed,
                    SchedulingFailures: _schedulingFailures,
                    QueryRequests: _queryRequests),
                Dependencies: dependencies,
                TopErrors: topErrors,
                RecentIncidents: incidents);
        }
    }

    private void IncrementError(string errorCode)
    {
        var normalized = string.IsNullOrWhiteSpace(errorCode)
            ? "unknown"
            : errorCode.Trim();

        if (_errors.TryGetValue(normalized, out var current))
        {
            _errors[normalized] = current + 1;
            return;
        }

        _errors[normalized] = 1;
    }

    private static void AppendLatency(List<double> values, double value)
    {
        values.Add(value);
        if (values.Count > LatencySampleLimit)
        {
            values.RemoveAt(0);
        }
    }

    private static double CalculateAverage(IReadOnlyCollection<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        return Math.Round(values.Average(), 2, MidpointRounding.AwayFromZero);
    }

    private static double CalculatePercentile(IReadOnlyCollection<double> values, int percentile)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var normalizedPercentile = Math.Clamp(percentile, 1, 99);
        var ordered = values.OrderBy(item => item).ToArray();
        var index = (int)Math.Ceiling((normalizedPercentile / 100d) * ordered.Length) - 1;
        index = Math.Clamp(index, 0, ordered.Length - 1);
        return Math.Round(ordered[index], 2, MidpointRounding.AwayFromZero);
    }

    private static decimal EstimateCostUsd(
        string? modelName,
        int? promptTokens,
        int? completionTokens)
    {
        var input = Math.Max(0, promptTokens ?? 0);
        var output = Math.Max(0, completionTokens ?? 0);
        if (input == 0 && output == 0)
        {
            return 0;
        }

        // Valores estimados por 1M tokens para monitoramento operacional.
        decimal inputPerMillion;
        decimal outputPerMillion;
        if (!string.IsNullOrWhiteSpace(modelName) &&
            modelName.Contains("gpt-4.1-mini", StringComparison.OrdinalIgnoreCase))
        {
            inputPerMillion = 0.40m;
            outputPerMillion = 1.60m;
        }
        else
        {
            inputPerMillion = 0.50m;
            outputPerMillion = 2.00m;
        }

        var inputCost = (input / 1_000_000m) * inputPerMillion;
        var outputCost = (output / 1_000_000m) * outputPerMillion;
        return inputCost + outputCost;
    }

    private sealed class DependencyStats
    {
        public long Calls { get; set; }

        public long Successes { get; set; }

        public long Failures { get; set; }

        public List<double> Latencies { get; } = [];
    }
}
