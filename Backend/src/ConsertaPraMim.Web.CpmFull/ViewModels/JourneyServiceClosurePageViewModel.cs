namespace AppMobileCPM.ViewModels;

public sealed record class JourneyServiceClosurePageViewModel
{
    public string Title { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string LeadName { get; init; } = string.Empty;
    public string ProviderName { get; init; } = string.Empty;
    public string CounterpartyName { get; init; } = string.Empty;
    public string RequestedCategory { get; init; } = string.Empty;
    public string AddressSummary { get; init; } = string.Empty;
    public string ScheduledWindowLabel { get; init; } = string.Empty;
    public string CompletionStatusLabel { get; init; } = string.Empty;
    public string ResponseHeadline { get; init; } = string.Empty;
    public string ResponseDescription { get; init; } = string.Empty;
    public string FeedbackMessage { get; init; } = string.Empty;
    public bool CanRespond { get; init; }
    public bool AlreadyResponded { get; init; }
    public bool TokenExpired { get; init; }
    public bool NotFound { get; init; }
    public bool ActionCompleted { get; init; }
    public bool ActionSucceeded { get; init; }
    public string NextReviewUrl { get; init; } = string.Empty;
}
