namespace AppMobileCPM.Services;

public sealed class AdminUser
{
    public int Id { get; init; }
    public required string Username { get; init; }
    public required string DisplayName { get; init; }
}

public sealed class AdminDashboardStats
{
    public int ProfessionalsCount { get; init; }
    public int ServiceRequestsCount { get; init; }
    public int ProfessionalRegistrationsCount { get; init; }
    public int SupportRequestsCount { get; init; }
}
