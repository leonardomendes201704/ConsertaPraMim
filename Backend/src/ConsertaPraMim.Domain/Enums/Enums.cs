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

public enum ProviderOnboardingStatus
{
    PendingDocumentation = 0,
    PendingApproval = 1,
    Active = 2
}

public enum ProviderDocumentType
{
    IdentityDocument = 1,
    SelfieWithDocument = 2,
    AddressProof = 3
}

public enum ProviderDocumentStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

public enum ProviderOperationalStatus
{
    Ausente = 0,
    Online = 1,
    EmAtendimento = 2
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
