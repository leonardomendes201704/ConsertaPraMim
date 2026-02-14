namespace ConsertaPraMim.Web.Client.Models;

public sealed record ClientAppointmentListItemViewModel(
    Guid AppointmentId,
    Guid ServiceRequestId,
    Guid ProviderId,
    string ProviderName,
    string Category,
    string Description,
    string Street,
    string City,
    string Status,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
