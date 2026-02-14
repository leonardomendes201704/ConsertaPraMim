namespace ConsertaPraMim.Web.Provider.Models;

public record PendingAppointmentConfirmationViewModel(
    Guid AppointmentId,
    Guid ServiceRequestId,
    string Category,
    string Description,
    string? ClientName,
    string Street,
    string City,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc);
