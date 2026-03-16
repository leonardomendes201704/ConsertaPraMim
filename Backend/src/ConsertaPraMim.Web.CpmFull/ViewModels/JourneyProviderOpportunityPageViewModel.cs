namespace AppMobileCPM.ViewModels;

public sealed record class JourneyProviderOpportunityPageViewModel
{
    public string Title { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string ActionLabel { get; init; } = string.Empty;
    public string ConfirmationLabel { get; init; } = string.Empty;
    public string LeadName { get; init; } = string.Empty;
    public string ProviderName { get; init; } = string.Empty;
    public string RequestedCategory { get; init; } = string.Empty;
    public string RequestedSubcategory { get; init; } = string.Empty;
    public string QualificationSummary { get; init; } = string.Empty;
    public string AddressSummary { get; init; } = string.Empty;
    public string ScheduledWindowLabel { get; init; } = string.Empty;
    public string DispatchStatusLabel { get; init; } = string.Empty;
    public string TargetStatusLabel { get; init; } = string.Empty;
    public string ResponseHeadline { get; init; } = string.Empty;
    public string ResponseDescription { get; init; } = string.Empty;
    public string FeedbackMessage { get; init; } = string.Empty;
    public string PortalUrl { get; init; } = string.Empty;
    public bool CanRespond { get; init; }
    public bool AlreadyReserved { get; init; }
    public bool AlreadyResponded { get; init; }
    public bool TokenExpired { get; init; }
    public bool NotFound { get; init; }
    public bool ActionCompleted { get; init; }
    public bool ActionSucceeded { get; init; }
}
