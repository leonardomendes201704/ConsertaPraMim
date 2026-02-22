namespace ConsertaPraMim.Application.Interfaces;

public interface INotificationService
{
    Task SendNotificationAsync(
        string recipient,
        string subject,
        string message,
        string? actionUrl = null);

    Task SendNotificationAsync(
        string recipient,
        string subject,
        string message,
        string? actionUrl,
        IReadOnlyDictionary<string, string>? data);
}
