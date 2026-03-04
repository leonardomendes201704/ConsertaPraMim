using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IGoogleCalendarSyncOperationsService
{
    Task<GoogleCalendarSyncOverviewDto> GetOverviewAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default);

    Task<GoogleCalendarSyncReprocessResultDto> ReprocessAsync(
        GoogleCalendarSyncReprocessRequestDto request,
        CancellationToken cancellationToken = default);

    Task<int> ProcessDueRetriesAsync(int batchSize, CancellationToken cancellationToken = default);

    Task<bool> SyncAppointmentAsync(
        Guid appointmentId,
        bool forceResetRetry = false,
        CancellationToken cancellationToken = default);
}
