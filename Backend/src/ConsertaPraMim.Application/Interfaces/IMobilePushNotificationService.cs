namespace ConsertaPraMim.Application.Interfaces;

public interface IMobilePushNotificationService
{
    Task SendToUserAsync(
        Guid userId,
        string title,
        string message,
        string? actionUrl = null,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default);

    Task<int> SendToAppKindAsync(
        string appKind,
        string title,
        string message,
        string? actionUrl = null,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default);

    Task SendToTokensAsync(
        IReadOnlyCollection<string> tokens,
        string title,
        string message,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default);
}
