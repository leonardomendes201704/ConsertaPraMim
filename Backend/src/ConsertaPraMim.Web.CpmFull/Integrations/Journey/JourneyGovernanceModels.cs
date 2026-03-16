using AppMobileCPM.Services;

namespace AppMobileCPM.Integrations.Journey;

public static class JourneyGovernanceSteps
{
    public const string Intake = "intake";
    public const string StageAutomation = "stage_automation";
    public const string Matching = "matching";
    public const string Dispatch = "dispatch";
    public const string Connection = "connection";
    public const string Closure = "closure";
}

public static class JourneyGovernanceReasonCodes
{
    public const string PendingDataTimeout = "pending_data_timeout";
    public const string ScheduleConfirmationTimeout = "schedule_confirmation_timeout";
    public const string MatchingMissingData = "matching_missing_data";
    public const string ClientContestation = "client_contestation";
    public const string ProviderOutcomeException = "provider_outcome_exception";
}

public sealed record class JourneyGovernanceDecision
{
    public bool Allowed { get; init; }
    public string Step { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public int RolloutBucket { get; init; }
}

public sealed record class JourneyOperationalExceptionPolicy
{
    public string ReasonCode { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string HistoryEventType { get; init; } = string.Empty;
    public string HandoffReason { get; init; } = string.Empty;
    public string TargetState { get; init; } = AdminKanbanJourneyStates.OperationalException;
    public string TargetStageName { get; init; } = AdminKanbanJourneyClientStageNames.OperationalException;
}
