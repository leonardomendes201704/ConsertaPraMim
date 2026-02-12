namespace ConsertaPraMim.Domain.Enums;

public enum UserRole
{
    Client = 1,
    Provider = 2,
    Admin = 99
}

public enum ProviderPlan
{
    Trial = 0,
    Bronze = 1,
    Silver = 2,
    Gold = 3
}

public enum ServiceRequestStatus
{
    Created = 1,
    Matching = 2,
    Scheduled = 3,
    InProgress = 4,
    Completed = 5,
    Validated = 6,
    Canceled = 99
}

public enum ServiceCategory
{
    Electrical = 1,
    Plumbing = 2,
    Electronics = 3,
    Appliances = 4,
    Masonry = 5,
    Cleaning = 6,
    Other = 99
}
