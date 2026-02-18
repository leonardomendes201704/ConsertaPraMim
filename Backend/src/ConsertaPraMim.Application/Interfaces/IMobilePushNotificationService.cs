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
}
