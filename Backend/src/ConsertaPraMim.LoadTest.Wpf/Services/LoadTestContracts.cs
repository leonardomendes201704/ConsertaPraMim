using ConsertaPraMim.LoadTest.Wpf.Models;

namespace ConsertaPraMim.LoadTest.Wpf.Services;

public sealed class LoadTestRunOptions
{
    public required string ConfigPath { get; init; }
    public required LoadTestConfig Config { get; init; }
    public required string ScenarioName { get; init; }
    public required ScenarioConfig Scenario { get; init; }
    public required string BaseUrl { get; init; }
    public required string OutputDirectory { get; init; }
    public required AdminPublishConfig AdminPublish { get; init; }
    public string OpenAiApiKey { get; init; } = string.Empty;
    public string OpenAiModel { get; init; } = "gpt-4.1-mini";
    public int Vus { get; init; }
    public int DurationSeconds { get; init; }
    public double RampUpSeconds { get; init; }
    public int ThinkMinMs { get; init; }
    public int ThinkMaxMs { get; init; }
    public double ErrorInjectionRatePercent { get; init; }
    public double TimeoutSeconds { get; init; } = 20;
    public bool InsecureTls { get; init; }
    public int Seed { get; init; } = 42;
    public double RefreshSeconds { get; init; } = 1;
}

public sealed class LoadTestResult
{
    public required LoadTestReport Report { get; init; }
    public required LoadTestLiveSnapshot FinalSnapshot { get; init; }
    public required string JsonPath { get; init; }
    public required string TxtPath { get; init; }
    public required string HtmlPath { get; init; }
    public required LoadTestPublishResult PublishResult { get; init; }
}

public sealed class LoadTestPublishResult
{
    public bool Attempted { get; init; }
    public bool Succeeded { get; init; }
    public required string Endpoint { get; init; }
    public required string Message { get; init; }
}

public sealed class LoadTestReport
{
    public required string RunId { get; init; }
    public required string Scenario { get; init; }
    public required string BaseUrl { get; init; }
    public required string StartedAtUtc { get; init; }
    public required string FinishedAtUtc { get; init; }
    public required double DurationSeconds { get; init; }
    public required LoadTestLiveSummary Summary { get; init; }
    public required LoadTestLiveLatency LatencyMs { get; init; }
    public required IReadOnlyList<StatusCodeStat> StatusCodes { get; init; }
    public required IReadOnlyList<ExceptionStat> Exceptions { get; init; }
    public required IReadOnlyList<EndpointLiveStat> TopEndpointsByHits { get; init; }
    public required IReadOnlyList<EndpointLiveStat> TopEndpointsByP95 { get; init; }
    public required IReadOnlyList<ErrorCatalogStat> TopErrors { get; init; }
    public required IReadOnlyList<FailureSample> FailureSamples { get; init; }
    public required object ScenarioConfig { get; init; }
    public LoadTestAiAnalysis? AiAnalysis { get; set; }
}

public sealed class LoadTestAiAnalysis
{
    public required string Summary { get; init; }
    public required string GeneratedAtUtc { get; init; }
    public required string Provider { get; init; }
    public required string Model { get; init; }
}

public sealed class LoadTestLiveSnapshot
{
    public required string RunId { get; init; }
    public required string Scenario { get; init; }
    public required string BaseUrl { get; init; }
    public required string StartedAtUtc { get; init; }
    public required string GeneratedAtUtc { get; init; }
    public required string Status { get; init; }
    public required bool IsCompleted { get; init; }
    public required string ErrorMessage { get; init; }
    public required int Vus { get; init; }
    public required int PlannedDurationSeconds { get; init; }
    public required double ElapsedSeconds { get; init; }
    public required double ProgressPercent { get; init; }
    public required LoadTestLiveSummary Summary { get; init; }
    public required LoadTestLiveLatency LatencyMs { get; init; }
    public required IReadOnlyList<StatusCodeStat> StatusCodes { get; init; }
    public required IReadOnlyList<ExceptionStat> Exceptions { get; init; }
    public required IReadOnlyList<EndpointLiveStat> TopEndpointsByHits { get; init; }
    public required IReadOnlyList<EndpointLiveStat> TopEndpointsByP95 { get; init; }
    public required IReadOnlyList<TimeSeriesPoint> RequestsPerSecond { get; init; }
}

public sealed class LoadTestLiveSummary
{
    public int TotalRequests { get; init; }
    public int SuccessfulRequests { get; init; }
    public int FailedRequests { get; init; }
    public double ErrorRatePercent { get; init; }
    public int RpsCurrent { get; init; }
    public double RpsAvg { get; init; }
    public int RpsPeak { get; init; }
}

public sealed class LoadTestLiveLatency
{
    public double Min { get; init; }
    public double Avg { get; init; }
    public double Max { get; init; }
    public double P50 { get; init; }
    public double P95 { get; init; }
    public double P99 { get; init; }
}

public sealed class StatusCodeStat
{
    public int StatusCode { get; init; }
    public int Count { get; init; }
    public double Percentage { get; init; }
}

public sealed class ExceptionStat
{
    public required string Type { get; init; }
    public int Count { get; init; }
}

public sealed class EndpointLiveStat
{
    public required string Endpoint { get; init; }
    public int Hits { get; init; }
    public int Errors { get; init; }
    public double ErrorRatePercent { get; init; }
    public double AvgLatencyMs { get; init; }
    public double P95LatencyMs { get; init; }
}

public sealed class TimeSeriesPoint
{
    public int Second { get; init; }
    public int Requests { get; init; }
}

public sealed class ErrorCatalogStat
{
    public required string Message { get; init; }
    public int Count { get; init; }
    public required IReadOnlyList<string> Endpoints { get; init; }
}

public sealed class FailureSample
{
    public required string TimestampUtc { get; init; }
    public required string ClientId { get; init; }
    public required string CorrelationId { get; init; }
    public required string Endpoint { get; init; }
    public required string Method { get; init; }
    public required string Path { get; init; }
    public int? StatusCode { get; init; }
    public double DurationMs { get; init; }
    public required string ErrorType { get; init; }
    public required string ErrorMessage { get; init; }
}
