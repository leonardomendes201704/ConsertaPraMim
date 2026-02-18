using ConsertaPraMim.Domain.Common;

namespace ConsertaPraMim.Domain.Entities;

public class ApiEndpointMetricHourly : BaseEntity
{
    public DateTime BucketStartUtc { get; set; }
    public string Method { get; set; } = string.Empty;
    public string EndpointTemplate { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string Severity { get; set; } = "info";
    public string TenantId { get; set; } = string.Empty;
    public long RequestCount { get; set; }
    public long ErrorCount { get; set; }
    public long WarningCount { get; set; }
    public long TotalDurationMs { get; set; }
    public int MinDurationMs { get; set; }
    public int MaxDurationMs { get; set; }
    public int P50DurationMs { get; set; }
    public int P95DurationMs { get; set; }
    public int P99DurationMs { get; set; }
}
