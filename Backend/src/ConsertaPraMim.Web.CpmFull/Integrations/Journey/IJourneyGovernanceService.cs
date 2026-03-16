namespace AppMobileCPM.Integrations.Journey;

public interface IJourneyGovernanceService
{
    JourneyGovernanceDecision EvaluateIntake(string boardType, string sourceChannel, string stableKey);
    JourneyGovernanceDecision EvaluateStep(string step, string sourceChannel);
    JourneyOperationalExceptionPolicy ResolveOperationalException(string reasonCode, string fallbackSummary);
}
