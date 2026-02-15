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
    PendingClientCompletionAcceptance = 7,
    Canceled = 99
}

public enum ServiceRequestCommercialState
{
    NotInitialized = 0,
    Stable = 1,
    PendingClientApproval = 2
}

public enum ServiceAppointmentStatus
{
    PendingProviderConfirmation = 1,
    Confirmed = 2,
    RejectedByProvider = 3,
    ExpiredWithoutProviderAction = 4,
    RescheduleRequestedByClient = 5,
    RescheduleRequestedByProvider = 6,
    RescheduleConfirmed = 7,
    CancelledByClient = 8,
    CancelledByProvider = 9,
    Completed = 10,
    Arrived = 11,
    InProgress = 12
}

public enum ServiceCompletionTermStatus
{
    PendingClientAcceptance = 1,
    AcceptedByClient = 2,
    ContestedByClient = 3,
    Expired = 4,
    EscalatedToAdmin = 5
}

public enum ServiceCompletionAcceptanceMethod
{
    Pin = 1,
    SignatureName = 2
}

public enum ServiceAppointmentOperationalStatus
{
    OnTheWay = 1,
    OnSite = 2,
    InService = 3,
    WaitingParts = 4,
    Completed = 5
}

public enum ServiceAppointmentNoShowRiskLevel
{
    Low = 1,
    Medium = 2,
    High = 3
}

public enum ServiceAppointmentNoShowQueueStatus
{
    Open = 1,
    InProgress = 2,
    Resolved = 3,
    Dismissed = 4
}

public enum ServiceScopeChangeRequestStatus
{
    PendingClientApproval = 1,
    ApprovedByClient = 2,
    RejectedByClient = 3,
    CancelledByProvider = 4,
    Expired = 5
}

public enum ServiceAppointmentActorRole
{
    System = 0,
    Client = 1,
    Provider = 2,
    Admin = 3
}

public enum AppointmentReminderChannel
{
    InApp = 1,
    Email = 2
}

public enum AppointmentReminderDispatchStatus
{
    Pending = 1,
    Sent = 2,
    FailedRetryable = 3,
    FailedPermanent = 4,
    Cancelled = 5
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

public enum PricingDiscountType
{
    Percentage = 1,
    FixedAmount = 2
}

public enum PaymentTransactionProvider
{
    Mock = 1
}

public enum PaymentTransactionMethod
{
    Pix = 1,
    Card = 2
}

public enum PaymentTransactionStatus
{
    Pending = 1,
    Paid = 2,
    Failed = 3,
    Refunded = 4
}

public enum ServiceFinancialPolicyEventType
{
    ClientCancellation = 1,
    ProviderCancellation = 2,
    ClientNoShow = 3,
    ProviderNoShow = 4
}
