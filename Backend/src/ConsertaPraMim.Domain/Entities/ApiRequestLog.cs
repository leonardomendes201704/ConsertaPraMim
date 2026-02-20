using ConsertaPraMim.Domain.Common;

namespace ConsertaPraMim.Domain.Entities;

public class ApiRequestLog : BaseEntity
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string CorrelationId { get; set; } = string.Empty;
    public string TraceId { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string EndpointTemplate { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public int DurationMs { get; set; }
    public string Severity { get; set; } = "info";
    public bool IsError { get; set; }
    public int WarningCount { get; set; }
    public string? WarningCodesJson { get; set; }
    public string? ErrorType { get; set; }
    public string? NormalizedErrorMessage { get; set; }
    public string? NormalizedErrorKey { get; set; }
    public string? IpHash { get; set; }
    public string? UserAgent { get; set; }
    public Guid? UserId { get; set; }
    public string? TenantId { get; set; }
    public long? RequestSizeBytes { get; set; }
    public long? ResponseSizeBytes { get; set; }
    public string? RequestBodyJson { get; set; }
    public string? ResponseBodyJson { get; set; }
    public string? RequestHeadersJson { get; set; }
    public string? QueryStringJson { get; set; }
    public string? RouteValuesJson { get; set; }
    public string Scheme { get; set; } = string.Empty;
    public string? Host { get; set; }
}
