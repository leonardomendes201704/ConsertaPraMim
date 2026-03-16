namespace AppMobileCPM.Integrations.Journey;

public interface IJourneyCalendarGateway
{
    Task<IReadOnlyList<JourneyCalendarBusySlot>> ListBusySlotsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    Task<JourneyCalendarEventUpsertResult> CreateEventAsync(
        JourneyCalendarEventUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<JourneyCalendarEventUpsertResult> UpdateEventAsync(
        string eventId,
        JourneyCalendarEventUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<JourneyCalendarEventDeleteResult> DeleteEventAsync(
        string eventId,
        CancellationToken cancellationToken = default);
}
