using ConsertaPraMim.Domain.Common;

namespace ConsertaPraMim.Domain.Entities;

public class AdminLoadTestRun : BaseEntity
{
    public string ExternalRunId { get; set; } = string.Empty;
    public string Scenario { get; set; } = "unknown";
    public string BaseUrl { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime FinishedAtUtc { get; set; }
    public double DurationSeconds { get; set; }

    public long TotalRequests { get; set; }
    public long SuccessfulRequests { get; set; }
    public long FailedRequests { get; set; }
    public double ErrorRatePercent { get; set; }
    public double RpsAvg { get; set; }
    public int RpsPeak { get; set; }

    public double MinLatencyMs { get; set; }
    public double P50LatencyMs { get; set; }
    public double P95LatencyMs { get; set; }
    public double P99LatencyMs { get; set; }
    public double MaxLatencyMs { get; set; }

    public string Source { get; set; } = "loadtest_runner";
    public string RawReportJson { get; set; } = "{}";
}
