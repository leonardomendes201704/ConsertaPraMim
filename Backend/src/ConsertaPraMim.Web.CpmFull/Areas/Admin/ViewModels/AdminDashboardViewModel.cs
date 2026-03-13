namespace AppMobileCPM.Areas.Admin.ViewModels;

public sealed class AdminDashboardViewModel
{
    public required string AdminDisplayName { get; init; }
    public int ProfessionalsCount { get; init; }
    public int ServiceRequestsCount { get; init; }
    public int ProfessionalRegistrationsCount { get; init; }
    public int SupportRequestsCount { get; init; }
}
