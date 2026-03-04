using System.Diagnostics.Metrics;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.Services;

public static class GoogleCalendarSyncTelemetry
{
    private static readonly Meter Meter = new("ConsertaPraMim.GoogleCalendarSync", "1.0.0");
    private static readonly Counter<long> CreatedCounter = Meter.CreateCounter<long>("cpm.google_calendar_sync.created");
    private static readonly Counter<long> UpdatedCounter = Meter.CreateCounter<long>("cpm.google_calendar_sync.updated");
    private static readonly Counter<long> DeletedCounter = Meter.CreateCounter<long>("cpm.google_calendar_sync.deleted");
    private static readonly Counter<long> FailedCounter = Meter.CreateCounter<long>("cpm.google_calendar_sync.failed");
    private static readonly Counter<long> RetryCounter = Meter.CreateCounter<long>("cpm.google_calendar_sync.retry_count");
    private static readonly Histogram<double> LatencyHistogram = Meter.CreateHistogram<double>("cpm.google_calendar_sync.latency_ms");

    public static void RecordSuccess(ServiceAppointmentCalendarSyncOperation operation, double latencyMs, int retryCount)
    {
        var tags = BuildTags(operation, success: true, retryCount);

        switch (operation)
        {
            case ServiceAppointmentCalendarSyncOperation.Create:
                CreatedCounter.Add(1, tags);
                break;
            case ServiceAppointmentCalendarSyncOperation.Update:
                UpdatedCounter.Add(1, tags);
                break;
            case ServiceAppointmentCalendarSyncOperation.Delete:
                DeletedCounter.Add(1, tags);
                break;
        }

        LatencyHistogram.Record(Math.Max(0, latencyMs), tags);
    }

    public static void RecordFailure(
        ServiceAppointmentCalendarSyncOperation operation,
        double latencyMs,
        int retryCount,
        string? errorCode)
    {
        var tags = BuildTags(operation, success: false, retryCount, errorCode);
        FailedCounter.Add(1, tags);
        LatencyHistogram.Record(Math.Max(0, latencyMs), tags);
    }

    public static void RecordRetryScheduled(ServiceAppointmentCalendarSyncOperation operation, int retryCount)
    {
        var tags = BuildTags(operation, success: false, retryCount);
        RetryCounter.Add(1, tags);
    }

    private static KeyValuePair<string, object?>[] BuildTags(
        ServiceAppointmentCalendarSyncOperation operation,
        bool success,
        int retryCount,
        string? errorCode = null)
    {
        return
        [
            new("operation", operation.ToString()),
            new("success", success),
            new("retry_count", Math.Max(0, retryCount)),
            new("error_code", string.IsNullOrWhiteSpace(errorCode) ? null : errorCode.Trim())
        ];
    }
}
