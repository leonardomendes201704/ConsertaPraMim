using System.Diagnostics;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ConsertaPraMim.Application.Services;

public sealed class GoogleCalendarSyncOperationsService : IGoogleCalendarSyncOperationsService
{
    private static readonly HashSet<ServiceAppointmentStatus> DeleteEligibleStatuses =
    [
        ServiceAppointmentStatus.CancelledByClient,
        ServiceAppointmentStatus.CancelledByProvider,
        ServiceAppointmentStatus.ExpiredWithoutProviderAction,
        ServiceAppointmentStatus.RejectedByProvider
    ];

    private static readonly HashSet<string> NonRetryableErrorCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "google_calendar_disabled",
        "google_calendar_invalid_payload",
        "google_calendar_invalid_title",
        "google_calendar_invalid_window",
        "google_calendar_invalid_idempotency_key",
        "google_calendar_invalid_event_id"
    };

    private readonly IServiceAppointmentCalendarSyncRepository _syncRepository;
    private readonly IServiceAppointmentRepository _serviceAppointmentRepository;
    private readonly IGoogleCalendarService _googleCalendarService;
    private readonly ILogger<GoogleCalendarSyncOperationsService> _logger;
    private readonly bool _retryEnabled;
    private readonly int _retryMaxAttempts;
    private readonly int _retryBaseDelaySeconds;
    private readonly int _retryMaxDelaySeconds;
    private readonly int _retryJitterMaxSeconds;

    public GoogleCalendarSyncOperationsService(
        IServiceAppointmentCalendarSyncRepository syncRepository,
        IServiceAppointmentRepository serviceAppointmentRepository,
        IGoogleCalendarService googleCalendarService,
        IConfiguration configuration,
        ILogger<GoogleCalendarSyncOperationsService> logger)
    {
        _syncRepository = syncRepository;
        _serviceAppointmentRepository = serviceAppointmentRepository;
        _googleCalendarService = googleCalendarService;
        _logger = logger;

        _retryEnabled = ParseBoolean(configuration["GoogleCalendarSync:RetryEnabled"], defaultValue: true);
        _retryMaxAttempts = ParseInt(configuration["GoogleCalendarSync:RetryMaxAttempts"], 5, 1, 20);
        _retryBaseDelaySeconds = ParseInt(configuration["GoogleCalendarSync:RetryBaseDelaySeconds"], 30, 1, 3600);
        _retryMaxDelaySeconds = ParseInt(configuration["GoogleCalendarSync:RetryMaxDelaySeconds"], 900, 1, 21600);
        _retryJitterMaxSeconds = ParseInt(configuration["GoogleCalendarSync:RetryJitterMaxSeconds"], 20, 0, 300);
    }

    public async Task<GoogleCalendarSyncOverviewDto> GetOverviewAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default)
    {
        var normalizedFrom = NormalizeNullableUtc(fromUtc);
        var normalizedTo = NormalizeNullableUtc(toUtc);
        var statuses = new[]
        {
            ServiceAppointmentCalendarSyncStatus.Pending,
            ServiceAppointmentCalendarSyncStatus.Synced,
            ServiceAppointmentCalendarSyncStatus.Failed,
            ServiceAppointmentCalendarSyncStatus.Deleted,
            ServiceAppointmentCalendarSyncStatus.DeadLetter
        };

        var items = await _syncRepository.QueryForReprocessAsync(
            appointmentId: null,
            fromUtc: normalizedFrom,
            toUtc: normalizedTo,
            statuses: statuses,
            take: 5000);

        var nowUtc = DateTime.UtcNow;
        var latencies = items
            .Where(x => x.LastLatencyMs.HasValue && x.LastLatencyMs.Value >= 0)
            .Select(x => x.LastLatencyMs!.Value)
            .OrderBy(x => x)
            .ToArray();

        var statusBuckets = items
            .GroupBy(x => x.SyncStatus)
            .OrderBy(g => g.Key)
            .Select(g => new GoogleCalendarSyncStatusBucketDto(g.Key, g.Count()))
            .ToList();

        return new GoogleCalendarSyncOverviewDto(
            GeneratedAtUtc: nowUtc,
            FromUtc: normalizedFrom,
            ToUtc: normalizedTo,
            Total: items.Count,
            PendingCount: items.Count(x => x.SyncStatus == ServiceAppointmentCalendarSyncStatus.Pending),
            SyncedCount: items.Count(x => x.SyncStatus == ServiceAppointmentCalendarSyncStatus.Synced),
            FailedRetryableCount: items.Count(x => x.SyncStatus == ServiceAppointmentCalendarSyncStatus.Failed),
            DeadLetterCount: items.Count(x => x.SyncStatus == ServiceAppointmentCalendarSyncStatus.DeadLetter),
            DeletedCount: items.Count(x => x.SyncStatus == ServiceAppointmentCalendarSyncStatus.Deleted),
            RetryQueueCount: items.Count(x =>
                x.SyncStatus == ServiceAppointmentCalendarSyncStatus.Failed &&
                x.NextRetryAtUtc.HasValue &&
                x.NextRetryAtUtc.Value >= nowUtc),
            RetryInLast24hCount: items.Count(x =>
                x.RetryCount > 0 &&
                x.LastSyncAtUtc.HasValue &&
                x.LastSyncAtUtc.Value >= nowUtc.AddHours(-24)),
            AverageLatencyMs: latencies.Length == 0 ? 0 : Math.Round(latencies.Average(), 2),
            P95LatencyMs: CalculateP95(latencies),
            StatusBuckets: statusBuckets);
    }

    public async Task<GoogleCalendarSyncReprocessResultDto> ReprocessAsync(
        GoogleCalendarSyncReprocessRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var startedAtUtc = DateTime.UtcNow;
        var statuses = NormalizeRequestedStatuses(request.Statuses);
        var items = await _syncRepository.QueryForReprocessAsync(
            request.AppointmentId,
            NormalizeNullableUtc(request.FromUtc),
            NormalizeNullableUtc(request.ToUtc),
            statuses,
            Math.Clamp(request.MaxItems, 1, 2000));

        var results = new List<GoogleCalendarSyncReprocessItemResultDto>(items.Count);
        var successCount = 0;
        var failedCount = 0;
        var deadLetterCount = 0;

        foreach (var sync in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request.ForceResetRetry)
            {
                sync.RetryCount = 0;
                sync.NextRetryAtUtc = null;
                sync.DeadLetterAtUtc = null;
            }

            var success = await ProcessSingleSyncAsync(sync, "admin_manual_reprocess", cancellationToken);
            if (success)
            {
                successCount++;
            }
            else if (sync.SyncStatus == ServiceAppointmentCalendarSyncStatus.DeadLetter)
            {
                deadLetterCount++;
            }
            else
            {
                failedCount++;
            }

            results.Add(new GoogleCalendarSyncReprocessItemResultDto(
                sync.AppointmentId,
                sync.LastOperation,
                sync.SyncStatus,
                sync.RetryCount,
                sync.NextRetryAtUtc,
                sync.LastSyncAtUtc,
                sync.LastLatencyMs,
                sync.LastErrorCode,
                sync.Error,
                sync.GoogleEventId));
        }

        return new GoogleCalendarSyncReprocessResultDto(
            StartedAtUtc: startedAtUtc,
            FinishedAtUtc: DateTime.UtcNow,
            RequestedCount: items.Count,
            ProcessedCount: results.Count,
            SuccessCount: successCount,
            FailedCount: failedCount,
            DeadLetterCount: deadLetterCount,
            Items: results);
    }

    public async Task<int> ProcessDueRetriesAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        if (!_retryEnabled)
        {
            return 0;
        }

        var dueItems = await _syncRepository.GetRetryDueAsync(DateTime.UtcNow, batchSize);
        if (dueItems.Count == 0)
        {
            return 0;
        }

        var processed = 0;
        foreach (var sync in dueItems)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessSingleSyncAsync(sync, "retry_worker", cancellationToken);
            processed++;
        }

        return processed;
    }

    public async Task<bool> SyncAppointmentAsync(
        Guid appointmentId,
        bool forceResetRetry = false,
        CancellationToken cancellationToken = default)
    {
        if (appointmentId == Guid.Empty)
        {
            return false;
        }

        var sync = await _syncRepository.GetByAppointmentIdAsync(appointmentId);
        if (sync == null)
        {
            sync = new ServiceAppointmentCalendarSync
            {
                AppointmentId = appointmentId,
                SyncStatus = ServiceAppointmentCalendarSyncStatus.Pending,
                MaxRetryAttempts = _retryMaxAttempts,
                Error = null,
                LastErrorCode = null
            };
            await _syncRepository.AddAsync(sync);
            sync = await _syncRepository.GetByAppointmentIdAsync(appointmentId) ?? sync;
        }

        if (forceResetRetry)
        {
            sync.RetryCount = 0;
            sync.NextRetryAtUtc = null;
            sync.DeadLetterAtUtc = null;
            sync.SyncStatus = ServiceAppointmentCalendarSyncStatus.Pending;
            sync.Error = null;
            sync.LastErrorCode = null;
            await _syncRepository.UpdateAsync(sync);
        }

        return await ProcessSingleSyncAsync(sync, "application_flow", cancellationToken);
    }

    private async Task<bool> ProcessSingleSyncAsync(
        ServiceAppointmentCalendarSync sync,
        string trigger,
        CancellationToken cancellationToken)
    {
        var appointment = sync.Appointment;
        if (appointment == null)
        {
            appointment = await _serviceAppointmentRepository.GetByIdAsync(sync.AppointmentId);
            sync.Appointment = appointment!;
        }

        if (appointment == null)
        {
            await MarkAsDeadLetterAsync(
                sync,
                operation: ServiceAppointmentCalendarSyncOperation.Unknown,
                errorCode: "appointment_not_found",
                errorMessage: "Agendamento nao encontrado para reprocessamento do Google Calendar.",
                latencyMs: 0,
                traceId: Activity.Current?.TraceId.ToString(),
                trigger,
                providerId: null);
            return false;
        }

        var operation = ResolveOperation(appointment, sync);
        var stopwatch = Stopwatch.StartNew();
        var traceId = Activity.Current?.TraceId.ToString();

        GoogleExecutionResult executionResult;
        try
        {
            executionResult = await ExecuteAsync(operation, appointment, sync, cancellationToken);
        }
        catch (Exception ex)
        {
            executionResult = new GoogleExecutionResult(
                Success: false,
                EventId: null,
                ErrorCode: "google_calendar_unexpected_error",
                ErrorMessage: ex.Message,
                ClearGoogleEventId: operation != ServiceAppointmentCalendarSyncOperation.Delete);
        }
        finally
        {
            stopwatch.Stop();
        }

        var latencyMs = stopwatch.Elapsed.TotalMilliseconds;
        if (executionResult.Success)
        {
            await MarkAsSyncedAsync(sync, operation, executionResult.EventId, latencyMs, traceId, trigger, appointment.ProviderId);
            return true;
        }

        await MarkAsFailedOrDeadLetterAsync(
            sync,
            operation,
            executionResult.ErrorCode,
            executionResult.ErrorMessage,
            executionResult.ClearGoogleEventId,
            latencyMs,
            traceId,
            trigger,
            appointment.ProviderId);
        return false;
    }

    private async Task<GoogleExecutionResult> ExecuteAsync(
        ServiceAppointmentCalendarSyncOperation operation,
        ServiceAppointment appointment,
        ServiceAppointmentCalendarSync sync,
        CancellationToken cancellationToken)
    {
        if (operation == ServiceAppointmentCalendarSyncOperation.Delete)
        {
            if (string.IsNullOrWhiteSpace(sync.GoogleEventId))
            {
                return new GoogleExecutionResult(Success: true, EventId: null, ErrorCode: null, ErrorMessage: null, ClearGoogleEventId: false);
            }

            var deleteResult = await _googleCalendarService.DeleteEventAsync(sync.GoogleEventId.Trim(), cancellationToken);
            return new GoogleExecutionResult(
                deleteResult.Success,
                sync.GoogleEventId,
                deleteResult.ErrorCode,
                deleteResult.ErrorMessage,
                ClearGoogleEventId: false);
        }

        var payload = BuildGoogleCalendarUpsertRequest(appointment);

        if (operation == ServiceAppointmentCalendarSyncOperation.Update && !string.IsNullOrWhiteSpace(sync.GoogleEventId))
        {
            var updateResult = await _googleCalendarService.UpdateEventAsync(sync.GoogleEventId.Trim(), payload, cancellationToken);
            if (updateResult.Success)
            {
                return new GoogleExecutionResult(
                    Success: true,
                    EventId: updateResult.EventId ?? sync.GoogleEventId,
                    ErrorCode: null,
                    ErrorMessage: null,
                    ClearGoogleEventId: false);
            }

            if (string.Equals(updateResult.ErrorCode, "google_calendar_event_not_found", StringComparison.OrdinalIgnoreCase))
            {
                var recreateResult = await _googleCalendarService.CreateEventAsync(payload, cancellationToken);
                return new GoogleExecutionResult(
                    Success: recreateResult.Success,
                    EventId: recreateResult.EventId,
                    ErrorCode: recreateResult.ErrorCode,
                    ErrorMessage: recreateResult.ErrorMessage,
                    ClearGoogleEventId: !recreateResult.Success);
            }

            return new GoogleExecutionResult(
                Success: false,
                EventId: null,
                ErrorCode: updateResult.ErrorCode,
                ErrorMessage: updateResult.ErrorMessage,
                ClearGoogleEventId: false);
        }

        var createResult = await _googleCalendarService.CreateEventAsync(payload, cancellationToken);
        return new GoogleExecutionResult(
            Success: createResult.Success,
            EventId: createResult.EventId,
            ErrorCode: createResult.ErrorCode,
            ErrorMessage: createResult.ErrorMessage,
            ClearGoogleEventId: !createResult.Success);
    }

    private async Task MarkAsSyncedAsync(
        ServiceAppointmentCalendarSync sync,
        ServiceAppointmentCalendarSyncOperation operation,
        string? eventId,
        double latencyMs,
        string? traceId,
        string trigger,
        Guid providerId)
    {
        var retryCountBeforeReset = sync.RetryCount;
        sync.LastOperation = operation;
        sync.SyncStatus = operation == ServiceAppointmentCalendarSyncOperation.Delete
            ? ServiceAppointmentCalendarSyncStatus.Deleted
            : ServiceAppointmentCalendarSyncStatus.Synced;
        if (!string.IsNullOrWhiteSpace(eventId))
        {
            sync.GoogleEventId = eventId.Trim();
        }

        sync.MaxRetryAttempts = _retryMaxAttempts;
        sync.RetryCount = 0;
        sync.NextRetryAtUtc = null;
        sync.DeadLetterAtUtc = null;
        sync.LastSyncAtUtc = DateTime.UtcNow;
        sync.LastLatencyMs = Math.Round(Math.Max(0, latencyMs), 2);
        sync.LastErrorCode = null;
        sync.Error = null;
        await _syncRepository.UpdateAsync(sync);

        GoogleCalendarSyncTelemetry.RecordSuccess(operation, latencyMs, retryCountBeforeReset);
        _logger.LogInformation(
            "Google Calendar sync bem-sucedido. Trigger={Trigger} AppointmentId={AppointmentId} ProviderId={ProviderId} Operation={Operation} GoogleEventId={GoogleEventId} RetryCount={RetryCount} LatencyMs={LatencyMs} TraceId={TraceId}",
            trigger,
            sync.AppointmentId,
            providerId,
            operation,
            sync.GoogleEventId,
            retryCountBeforeReset,
            sync.LastLatencyMs,
            traceId);
    }

    private async Task MarkAsFailedOrDeadLetterAsync(
        ServiceAppointmentCalendarSync sync,
        ServiceAppointmentCalendarSyncOperation operation,
        string? errorCode,
        string? errorMessage,
        bool clearGoogleEventId,
        double latencyMs,
        string? traceId,
        string trigger,
        Guid providerId)
    {
        var normalizedErrorCode = string.IsNullOrWhiteSpace(errorCode)
            ? "google_calendar_unknown_error"
            : errorCode.Trim();
        var nextRetryAttempt = sync.RetryCount + 1;

        sync.LastOperation = operation;
        sync.LastSyncAtUtc = DateTime.UtcNow;
        sync.LastLatencyMs = Math.Round(Math.Max(0, latencyMs), 2);
        sync.LastErrorCode = normalizedErrorCode;
        sync.Error = TruncateError(ComposeSyncError(normalizedErrorCode, errorMessage));
        sync.MaxRetryAttempts = _retryMaxAttempts;
        sync.RetryCount = nextRetryAttempt;
        if (clearGoogleEventId)
        {
            sync.GoogleEventId = null;
        }

        var retryable = _retryEnabled && IsRetryable(normalizedErrorCode);
        if (retryable && nextRetryAttempt < _retryMaxAttempts)
        {
            sync.SyncStatus = ServiceAppointmentCalendarSyncStatus.Failed;
            sync.NextRetryAtUtc = DateTime.UtcNow.Add(CalculateBackoffWithJitter(nextRetryAttempt));
            sync.DeadLetterAtUtc = null;
            await _syncRepository.UpdateAsync(sync);

            GoogleCalendarSyncTelemetry.RecordFailure(operation, latencyMs, nextRetryAttempt, normalizedErrorCode);
            GoogleCalendarSyncTelemetry.RecordRetryScheduled(operation, nextRetryAttempt);
            _logger.LogWarning(
                "Google Calendar sync com falha retryable. Trigger={Trigger} AppointmentId={AppointmentId} ProviderId={ProviderId} Operation={Operation} RetryCount={RetryCount} NextRetryAtUtc={NextRetryAtUtc} ErrorCode={ErrorCode} GoogleEventId={GoogleEventId} TraceId={TraceId}",
                trigger,
                sync.AppointmentId,
                providerId,
                operation,
                sync.RetryCount,
                sync.NextRetryAtUtc,
                sync.LastErrorCode,
                sync.GoogleEventId,
                traceId);
            return;
        }

        await MarkAsDeadLetterAsync(
            sync,
            operation,
            normalizedErrorCode,
            errorMessage,
            latencyMs,
            traceId,
            trigger,
            providerId);
    }

    private async Task MarkAsDeadLetterAsync(
        ServiceAppointmentCalendarSync sync,
        ServiceAppointmentCalendarSyncOperation operation,
        string? errorCode,
        string? errorMessage,
        double latencyMs,
        string? traceId,
        string trigger,
        Guid? providerId)
    {
        sync.LastOperation = operation;
        sync.SyncStatus = ServiceAppointmentCalendarSyncStatus.DeadLetter;
        sync.NextRetryAtUtc = null;
        sync.DeadLetterAtUtc = DateTime.UtcNow;
        sync.LastSyncAtUtc = DateTime.UtcNow;
        sync.LastLatencyMs = Math.Round(Math.Max(0, latencyMs), 2);
        sync.LastErrorCode = string.IsNullOrWhiteSpace(errorCode) ? "google_calendar_unknown_error" : errorCode.Trim();
        sync.Error = TruncateError(ComposeSyncError(sync.LastErrorCode, errorMessage));
        await _syncRepository.UpdateAsync(sync);

        GoogleCalendarSyncTelemetry.RecordFailure(operation, latencyMs, sync.RetryCount, sync.LastErrorCode);
        _logger.LogError(
            "Google Calendar sync em dead-letter. Trigger={Trigger} AppointmentId={AppointmentId} ProviderId={ProviderId} Operation={Operation} RetryCount={RetryCount} ErrorCode={ErrorCode} GoogleEventId={GoogleEventId} TraceId={TraceId}",
            trigger,
            sync.AppointmentId,
            providerId,
            operation,
            sync.RetryCount,
            sync.LastErrorCode,
            sync.GoogleEventId,
            traceId);
    }

    private static ServiceAppointmentCalendarSyncOperation ResolveOperation(
        ServiceAppointment appointment,
        ServiceAppointmentCalendarSync sync)
    {
        if (DeleteEligibleStatuses.Contains(appointment.Status))
        {
            return ServiceAppointmentCalendarSyncOperation.Delete;
        }

        return string.IsNullOrWhiteSpace(sync.GoogleEventId)
            ? ServiceAppointmentCalendarSyncOperation.Create
            : ServiceAppointmentCalendarSyncOperation.Update;
    }

    private TimeSpan CalculateBackoffWithJitter(int retryAttempt)
    {
        var exponent = Math.Max(0, retryAttempt - 1);
        var baseDelay = _retryBaseDelaySeconds * Math.Pow(2, exponent);
        var cappedSeconds = Math.Min(_retryMaxDelaySeconds, baseDelay);
        var jitter = _retryJitterMaxSeconds <= 0 ? 0 : Random.Shared.Next(0, _retryJitterMaxSeconds + 1);
        return TimeSpan.FromSeconds(cappedSeconds + jitter);
    }

    private static bool IsRetryable(string errorCode)
    {
        return !NonRetryableErrorCodes.Contains(errorCode);
    }

    private static IReadOnlyList<ServiceAppointmentCalendarSyncStatus> NormalizeRequestedStatuses(
        IReadOnlyList<ServiceAppointmentCalendarSyncStatus>? statuses)
    {
        if (statuses == null || statuses.Count == 0)
        {
            return
            [
                ServiceAppointmentCalendarSyncStatus.Pending,
                ServiceAppointmentCalendarSyncStatus.Failed,
                ServiceAppointmentCalendarSyncStatus.DeadLetter
            ];
        }

        return statuses
            .Distinct()
            .ToArray();
    }

    private static double CalculateP95(IReadOnlyList<double> sortedValues)
    {
        if (sortedValues.Count == 0)
        {
            return 0;
        }

        var index = (int)Math.Ceiling(sortedValues.Count * 0.95) - 1;
        index = Math.Clamp(index, 0, sortedValues.Count - 1);
        return Math.Round(sortedValues[index], 2);
    }

    private static GoogleCalendarUpsertRequest BuildGoogleCalendarUpsertRequest(ServiceAppointment appointment)
    {
        var serviceRequest = appointment.ServiceRequest;
        var protocol = BuildCalendarProtocol(appointment.ServiceRequestId);
        var client = appointment.Client;
        var provider = appointment.Provider;

        return new GoogleCalendarUpsertRequest(
            Title: $"ConsertaPraMim - Visita #{protocol}",
            StartsAtUtc: NormalizeToUtc(appointment.WindowStartUtc),
            EndsAtUtc: NormalizeToUtc(appointment.WindowEndUtc),
            Description: BuildCalendarDescription(protocol, appointment, serviceRequest, client, provider),
            Location: BuildCalendarLocation(serviceRequest),
            Metadata: BuildCalendarMetadata(protocol, appointment),
            IdempotencyKey: BuildCalendarEventIdempotencyKey(appointment.Id));
    }

    private static IReadOnlyDictionary<string, string> BuildCalendarMetadata(
        string protocol,
        ServiceAppointment appointment)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["appointment_id"] = appointment.Id.ToString("N"),
            ["service_request_id"] = appointment.ServiceRequestId.ToString("N"),
            ["client_id"] = appointment.ClientId.ToString("N"),
            ["provider_id"] = appointment.ProviderId.ToString("N"),
            ["protocol"] = protocol,
            ["appointment_status"] = appointment.Status.ToString()
        };
    }

    private static string BuildCalendarDescription(
        string protocol,
        ServiceAppointment appointment,
        ServiceRequest? serviceRequest,
        User? client,
        User? provider)
    {
        var reason = string.IsNullOrWhiteSpace(appointment.Reason)
            ? "Agendamento atualizado automaticamente pelo sistema."
            : appointment.Reason.Trim();

        var clientDisplay = BuildCalendarPartyDisplay(client, appointment.ClientId);
        var providerDisplay = BuildCalendarPartyDisplay(provider, appointment.ProviderId);
        var categoryDisplay = BuildCalendarCategoryDisplay(serviceRequest);
        var addressDisplay = BuildCalendarLocation(serviceRequest) ?? "Nao informado";

        return string.Join(
            Environment.NewLine,
            [
                $"Protocolo: #{protocol}",
                $"Pedido: {appointment.ServiceRequestId:N}",
                $"Agendamento: {appointment.Id:N}",
                $"Cliente: {clientDisplay}",
                $"Prestador: {providerDisplay}",
                $"Categoria: {categoryDisplay}",
                $"Endereco: {addressDisplay}",
                $"Motivo: {reason}"
            ]);
    }

    private static string BuildCalendarPartyDisplay(User? user, Guid userId)
    {
        if (!string.IsNullOrWhiteSpace(user?.Name))
        {
            return $"{user!.Name.Trim()} ({userId:N})";
        }

        return userId.ToString("N");
    }

    private static string BuildCalendarCategoryDisplay(ServiceRequest? serviceRequest)
    {
        if (serviceRequest == null)
        {
            return "Nao informado";
        }

        if (!string.IsNullOrWhiteSpace(serviceRequest.CategoryDefinition?.Name))
        {
            return serviceRequest.CategoryDefinition.Name.Trim();
        }

        return serviceRequest.Category.ToString();
    }

    private static string? BuildCalendarLocation(ServiceRequest? serviceRequest)
    {
        if (serviceRequest == null)
        {
            return null;
        }

        var parts = new[]
        {
            serviceRequest.AddressStreet?.Trim(),
            serviceRequest.AddressNeighborhood?.Trim(),
            serviceRequest.AddressCity?.Trim(),
            serviceRequest.AddressZip?.Trim()
        }.Where(value => !string.IsNullOrWhiteSpace(value)).ToList();

        return parts.Count == 0 ? null : string.Join(" - ", parts);
    }

    private static string BuildCalendarEventIdempotencyKey(Guid appointmentId)
    {
        return $"cpm-apt-{appointmentId:N}";
    }

    private static string BuildCalendarProtocol(Guid serviceRequestId)
    {
        if (serviceRequestId == Guid.Empty)
        {
            return "00000000";
        }

        return serviceRequestId.ToString("N")[..8];
    }

    private static DateTime NormalizeToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static DateTime? NormalizeNullableUtc(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return NormalizeToUtc(value.Value);
    }

    private static string ComposeSyncError(string? errorCode, string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorCode) && string.IsNullOrWhiteSpace(errorMessage))
        {
            return "Erro nao identificado durante sincronizacao com Google Calendar.";
        }

        if (string.IsNullOrWhiteSpace(errorCode))
        {
            return errorMessage!.Trim();
        }

        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return errorCode.Trim();
        }

        return $"{errorCode.Trim()}: {errorMessage.Trim()}";
    }

    private static string TruncateError(string value)
    {
        const int maxLength = 1200;
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }

    private static int ParseInt(string? raw, int defaultValue, int min, int max)
    {
        if (!int.TryParse(raw, out var parsed))
        {
            return defaultValue;
        }

        return Math.Clamp(parsed, min, max);
    }

    private static bool ParseBoolean(string? raw, bool defaultValue)
    {
        if (!bool.TryParse(raw, out var parsed))
        {
            return defaultValue;
        }

        return parsed;
    }

    private sealed record GoogleExecutionResult(
        bool Success,
        string? EventId,
        string? ErrorCode,
        string? ErrorMessage,
        bool ClearGoogleEventId);
}
