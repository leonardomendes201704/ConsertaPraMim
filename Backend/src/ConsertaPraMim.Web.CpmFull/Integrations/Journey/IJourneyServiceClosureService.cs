namespace AppMobileCPM.Integrations.Journey;

public interface IJourneyServiceClosureService
{
    Task<JourneyServiceClosureStartResult> StartServiceAsync(int leadId, DateTime nowUtc, CancellationToken cancellationToken = default);
    JourneyServiceClosureCompletionContext GetProviderCompletionContext(string token, DateTime nowUtc);
    Task<JourneyServiceClosureCompletionActionResult> SubmitProviderOutcomeAsync(string token, string outcome, string notes, DateTime nowUtc, CancellationToken cancellationToken = default);
    JourneyServiceClosureCompletionContext GetClientCompletionContext(string token, string action, DateTime nowUtc);
    Task<JourneyServiceClosureCompletionActionResult> SubmitClientDecisionAsync(string token, string action, string reason, DateTime nowUtc, CancellationToken cancellationToken = default);
    JourneyServiceClosureReviewContext GetReviewContext(string token, string audience, DateTime nowUtc);
    Task<JourneyServiceClosureReviewActionResult> SubmitReviewAsync(string token, string audience, JourneyServiceClosureReviewSubmissionRequest request, DateTime nowUtc, CancellationToken cancellationToken = default);
}
