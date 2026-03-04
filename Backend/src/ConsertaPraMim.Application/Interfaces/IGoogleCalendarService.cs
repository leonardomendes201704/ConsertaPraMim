namespace ConsertaPraMim.Application.Interfaces;

public interface IGoogleCalendarService
{
    Task<GoogleCalendarUpsertResult> CreateEventAsync(
        GoogleCalendarUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<GoogleCalendarUpsertResult> UpdateEventAsync(
        string eventId,
        GoogleCalendarUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<GoogleCalendarDeleteResult> DeleteEventAsync(
        string eventId,
        CancellationToken cancellationToken = default);
}

public record GoogleCalendarUpsertRequest(
    string Title,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    string? Description = null,
    string? Location = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public record GoogleCalendarUpsertResult(
    bool Success,
    string? EventId = null,
    string? HtmlLink = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record GoogleCalendarDeleteResult(
    bool Success,
    string? ErrorCode = null,
    string? ErrorMessage = null);
