using AppMobileCPM.Integrations.Journey;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Tests.Unit.Integrations.Journey;

public sealed class JourneyGovernanceServiceTests
{
    [Fact(DisplayName = "Journey Governance | Deve bloquear intake quando canal estiver fora do rollout")]
    public void EvaluateIntake_DeveBloquearCanalForaDoRollout()
    {
        var sut = new JourneyGovernanceService(Options.Create(new JourneyGovernanceOptions
        {
            Enabled = true,
            RolloutPercentage = 100,
            AllowedSourceChannels = "telegram",
            IntakeEnabled = true
        }));

        var result = sut.EvaluateIntake(
            AppMobileCPM.Services.AdminKanbanBoardTypes.Clients,
            AppMobileCPM.Services.AdminKanbanJourneySourceChannels.Landing,
            "lead:001");

        Assert.False(result.Allowed);
        Assert.Contains("fora do rollout", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Journey Governance | Deve devolver politica padrao de contestacao do cliente")]
    public void ResolveOperationalException_DeveMapearContestacao()
    {
        var sut = new JourneyGovernanceService(Options.Create(new JourneyGovernanceOptions
        {
            Enabled = true,
            RouteOperationalExceptionsToHandoff = true
        }));

        var result = sut.ResolveOperationalException(
            JourneyGovernanceReasonCodes.ClientContestation,
            "fallback");

        Assert.Equal("jornada_handoff_contestacao_cliente", result.HistoryEventType);
        Assert.Contains("contestou", result.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Contestacao", result.HandoffReason, StringComparison.OrdinalIgnoreCase);
    }
}
