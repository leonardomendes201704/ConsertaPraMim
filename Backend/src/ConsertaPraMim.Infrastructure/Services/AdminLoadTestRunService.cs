using System.Globalization;
using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConsertaPraMim.Infrastructure.Services;

public class AdminLoadTestRunService : IAdminLoadTestRunService
{
    private readonly ConsertaPraMimDbContext _dbContext;
    private readonly ILogger<AdminLoadTestRunService> _logger;

    public AdminLoadTestRunService(
        ConsertaPraMimDbContext dbContext,
        ILogger<AdminLoadTestRunService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<AdminLoadTestRunsResponseDto> GetRunsAsync(
        AdminLoadTestRunsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var source = _dbContext.AdminLoadTestRuns.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Scenario))
        {
            var scenario = query.Scenario.Trim();
            source = source.Where(x => x.Scenario == scenario);
        }

        if (query.FromUtc.HasValue)
        {
            source = source.Where(x => x.StartedAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            source = source.Where(x => x.StartedAtUtc <= query.ToUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            source = source.Where(x =>
                x.ExternalRunId.Contains(search) ||
                x.Scenario.Contains(search) ||
                x.BaseUrl.Contains(search) ||
                x.Source.Contains(search));
        }

        var total = await source.CountAsync(cancellationToken);

        var items = await source
            .OrderByDescending(x => x.StartedAtUtc)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AdminLoadTestRunListItemDto(
                x.Id,
                x.ExternalRunId,
                x.Scenario,
                x.BaseUrl,
                x.StartedAtUtc,
                x.FinishedAtUtc,
                x.DurationSeconds,
                x.TotalRequests,
                x.SuccessfulRequests,
                x.FailedRequests,
                x.ErrorRatePercent,
                x.RpsAvg,
                x.RpsPeak,
                x.P95LatencyMs,
                x.Source,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return new AdminLoadTestRunsResponseDto(page, pageSize, total, items);
    }

    public async Task<AdminLoadTestRunDetailsDto?> GetRunByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.AdminLoadTestRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity == null)
        {
            return null;
        }

        return BuildDetailsDto(entity);
    }

    public async Task<AdminLoadTestImportResultDto> ImportRunAsync(
        AdminLoadTestImportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.Report.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Payload de report invalido. Esperado objeto JSON no campo 'report'.");
        }

        var now = DateTime.UtcNow;
        var externalRunId = GetString(request.Report, "runId")?.Trim();
        if (string.IsNullOrWhiteSpace(externalRunId))
        {
            externalRunId = Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture);
        }

        var scenario = TruncateOrDefault(GetString(request.Report, "scenario"), 64, "unknown");
        var baseUrl = TruncateOrDefault(GetString(request.Report, "baseUrl"), 400, string.Empty);
        var source = TruncateOrDefault(request.Source, 64, "loadtest_runner");

        var startedAtUtc = GetDateTime(request.Report, "startedAtUtc") ?? now;
        var finishedAtUtc = GetDateTime(request.Report, "finishedAtUtc") ?? startedAtUtc;
        var durationSeconds = GetDouble(request.Report, "durationSeconds");
        if (durationSeconds <= 0 && finishedAtUtc >= startedAtUtc)
        {
            durationSeconds = (finishedAtUtc - startedAtUtc).TotalSeconds;
        }

        var summary = TryGetObject(request.Report, "summary");
        var totalRequests = GetLong(summary, "totalRequests");
        var successfulRequests = GetLong(summary, "successfulRequests");
        var failedRequests = GetLong(summary, "failedRequests");
        var errorRatePercent = GetDouble(summary, "errorRatePercent");
        var rpsAvg = GetDouble(summary, "rpsAvg");
        var rpsPeak = (int)Math.Max(0, GetLong(summary, "rpsPeak"));

        var latency = TryGetObject(request.Report, "latencyMs");
        var minLatencyMs = GetDouble(latency, "min");
        var p50LatencyMs = GetDouble(latency, "p50");
        var p95LatencyMs = GetDouble(latency, "p95");
        var p99LatencyMs = GetDouble(latency, "p99");
        var maxLatencyMs = GetDouble(latency, "max");

        var rawJson = request.Report.GetRawText();

        var entity = await _dbContext.AdminLoadTestRuns
            .FirstOrDefaultAsync(x => x.ExternalRunId == externalRunId, cancellationToken);

        var created = entity == null;
        if (entity == null)
        {
            entity = new AdminLoadTestRun
            {
                ExternalRunId = externalRunId
            };
            _dbContext.AdminLoadTestRuns.Add(entity);
        }

        entity.Scenario = scenario;
        entity.BaseUrl = baseUrl;
        entity.StartedAtUtc = startedAtUtc;
        entity.FinishedAtUtc = finishedAtUtc;
        entity.DurationSeconds = Math.Max(0, durationSeconds);
        entity.TotalRequests = Math.Max(0, totalRequests);
        entity.SuccessfulRequests = Math.Max(0, successfulRequests);
        entity.FailedRequests = Math.Max(0, failedRequests);
        entity.ErrorRatePercent = Math.Max(0, errorRatePercent);
        entity.RpsAvg = Math.Max(0, rpsAvg);
        entity.RpsPeak = Math.Max(0, rpsPeak);
        entity.MinLatencyMs = Math.Max(0, minLatencyMs);
        entity.P50LatencyMs = Math.Max(0, p50LatencyMs);
        entity.P95LatencyMs = Math.Max(0, p95LatencyMs);
        entity.P99LatencyMs = Math.Max(0, p99LatencyMs);
        entity.MaxLatencyMs = Math.Max(0, maxLatencyMs);
        entity.Source = source;
        entity.RawReportJson = rawJson;
        entity.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Load test run imported. RunId={RunId} Created={Created} Scenario={Scenario} TotalRequests={TotalRequests}",
            entity.ExternalRunId,
            created,
            entity.Scenario,
            entity.TotalRequests);

        return new AdminLoadTestImportResultDto(
            entity.Id,
            entity.ExternalRunId,
            created,
            created ? "Run de carga importado com sucesso." : "Run de carga atualizado com sucesso.");
    }

    private static AdminLoadTestRunDetailsDto BuildDetailsDto(AdminLoadTestRun entity)
    {
        using var doc = ParseJsonSafely(entity.RawReportJson);
        var root = doc.RootElement;

        var statusCodes = ParseStatusCodes(root);
        var topByHits = ParseEndpointSnapshots(root, "topEndpointsByHits");
        var topByP95 = ParseEndpointSnapshots(root, "topEndpointsByP95");
        var topErrors = ParseTopErrors(root);
        var failures = ParseFailureSamples(root);

        return new AdminLoadTestRunDetailsDto(
            entity.Id,
            entity.ExternalRunId,
            entity.Scenario,
            entity.BaseUrl,
            entity.StartedAtUtc,
            entity.FinishedAtUtc,
            entity.DurationSeconds,
            entity.TotalRequests,
            entity.SuccessfulRequests,
            entity.FailedRequests,
            entity.ErrorRatePercent,
            entity.RpsAvg,
            entity.RpsPeak,
            entity.MinLatencyMs,
            entity.P50LatencyMs,
            entity.P95LatencyMs,
            entity.P99LatencyMs,
            entity.MaxLatencyMs,
            entity.Source,
            entity.CreatedAt,
            entity.UpdatedAt,
            statusCodes,
            topByHits,
            topByP95,
            topErrors,
            failures,
            entity.RawReportJson);
    }

    private static JsonDocument ParseJsonSafely(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return JsonDocument.Parse("{}", new JsonDocumentOptions { AllowTrailingCommas = true });
        }

        try
        {
            return JsonDocument.Parse(rawJson, new JsonDocumentOptions { AllowTrailingCommas = true });
        }
        catch
        {
            return JsonDocument.Parse("{}", new JsonDocumentOptions { AllowTrailingCommas = true });
        }
    }

    private static IReadOnlyList<AdminLoadTestStatusCodeSnapshotDto> ParseStatusCodes(JsonElement root)
    {
        if (!TryGetArray(root, "statusCodes", out var array))
        {
            return Array.Empty<AdminLoadTestStatusCodeSnapshotDto>();
        }

        var items = new List<AdminLoadTestStatusCodeSnapshotDto>();
        foreach (var item in array.EnumerateArray())
        {
            items.Add(new AdminLoadTestStatusCodeSnapshotDto(
                (int)GetLong(item, "statusCode"),
                GetLong(item, "count"),
                GetDouble(item, "percentage")));
        }

        return items;
    }

    private static IReadOnlyList<AdminLoadTestEndpointSnapshotDto> ParseEndpointSnapshots(JsonElement root, string propertyName)
    {
        if (!TryGetArray(root, propertyName, out var array))
        {
            return Array.Empty<AdminLoadTestEndpointSnapshotDto>();
        }

        var items = new List<AdminLoadTestEndpointSnapshotDto>();
        foreach (var item in array.EnumerateArray())
        {
            items.Add(new AdminLoadTestEndpointSnapshotDto(
                GetString(item, "endpoint") ?? "unknown",
                GetLong(item, "hits"),
                GetLong(item, "errors"),
                GetDouble(item, "errorRatePercent"),
                GetDouble(item, "avgLatencyMs"),
                GetDouble(item, "p95LatencyMs")));
        }

        return items;
    }

    private static IReadOnlyList<AdminLoadTestErrorSnapshotDto> ParseTopErrors(JsonElement root)
    {
        if (!TryGetArray(root, "topErrors", out var array))
        {
            return Array.Empty<AdminLoadTestErrorSnapshotDto>();
        }

        var items = new List<AdminLoadTestErrorSnapshotDto>();
        foreach (var item in array.EnumerateArray())
        {
            var endpoints = new List<string>();
            if (TryGetArray(item, "endpoints", out var endpointArray))
            {
                foreach (var endpoint in endpointArray.EnumerateArray())
                {
                    if (endpoint.ValueKind == JsonValueKind.String)
                    {
                        var endpointValue = endpoint.GetString();
                        if (!string.IsNullOrWhiteSpace(endpointValue))
                        {
                            endpoints.Add(endpointValue.Trim());
                        }
                    }
                }
            }

            items.Add(new AdminLoadTestErrorSnapshotDto(
                GetString(item, "message") ?? "unknown_error",
                GetLong(item, "count"),
                endpoints));
        }

        return items;
    }

    private static IReadOnlyList<AdminLoadTestFailureSampleSnapshotDto> ParseFailureSamples(JsonElement root)
    {
        if (!TryGetArray(root, "failureSamples", out var array))
        {
            return Array.Empty<AdminLoadTestFailureSampleSnapshotDto>();
        }

        var items = new List<AdminLoadTestFailureSampleSnapshotDto>();
        foreach (var item in array.EnumerateArray())
        {
            items.Add(new AdminLoadTestFailureSampleSnapshotDto(
                GetDateTime(item, "timestampUtc"),
                GetString(item, "method") ?? string.Empty,
                GetString(item, "path") ?? string.Empty,
                TryGetInt(item, "statusCode"),
                GetString(item, "correlationId") ?? string.Empty,
                GetString(item, "errorType") ?? string.Empty,
                GetString(item, "errorMessage") ?? string.Empty));
        }

        return items;
    }

    private static JsonElement TryGetObject(JsonElement source, string propertyName)
    {
        if (source.ValueKind == JsonValueKind.Object && source.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Object)
        {
            return value;
        }

        return default;
    }

    private static bool TryGetArray(JsonElement source, string propertyName, out JsonElement value)
    {
        if (source.ValueKind == JsonValueKind.Object && source.TryGetProperty(propertyName, out var propertyValue) && propertyValue.ValueKind == JsonValueKind.Array)
        {
            value = propertyValue;
            return true;
        }

        value = default;
        return false;
    }

    private static string? GetString(JsonElement source, string propertyName)
    {
        if (source.ValueKind != JsonValueKind.Object || !source.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.ToString();
    }

    private static DateTime? GetDateTime(JsonElement source, string propertyName)
    {
        var raw = GetString(source, propertyName);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var value)
            ? value.ToUniversalTime()
            : null;
    }

    private static long GetLong(JsonElement source, string propertyName)
    {
        if (source.ValueKind != JsonValueKind.Object || !source.TryGetProperty(propertyName, out var value))
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var intValue))
        {
            return intValue;
        }

        if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return 0;
    }

    private static int? TryGetInt(JsonElement source, string propertyName)
    {
        if (source.ValueKind != JsonValueKind.Object || !source.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var intValue))
        {
            return intValue;
        }

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static double GetDouble(JsonElement source, string propertyName)
    {
        if (source.ValueKind != JsonValueKind.Object || !source.TryGetProperty(propertyName, out var value))
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var doubleValue))
        {
            return doubleValue;
        }

        if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return 0;
    }

    private static string TruncateOrDefault(string? value, int maxLength, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }
}
