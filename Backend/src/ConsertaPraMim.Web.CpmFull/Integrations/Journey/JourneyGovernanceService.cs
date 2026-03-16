using System.Security.Cryptography;
using System.Text;
using AppMobileCPM.Services;
using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyGovernanceService : IJourneyGovernanceService
{
    private readonly JourneyGovernanceOptions _options;
    private readonly HashSet<string> _allowedSourceChannels;

    public JourneyGovernanceService(IOptions<JourneyGovernanceOptions> options)
    {
        _options = options.Value;
        _allowedSourceChannels = (_options.AllowedSourceChannels ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(AdminKanbanJourneySourceChannels.Normalize)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public JourneyGovernanceDecision EvaluateIntake(string boardType, string sourceChannel, string stableKey)
    {
        if (!_options.Enabled)
        {
            return new JourneyGovernanceDecision
            {
                Step = JourneyGovernanceSteps.Intake,
                Reason = "Governanca da jornada desabilitada no ambiente atual."
            };
        }

        if (!_options.IntakeEnabled)
        {
            return new JourneyGovernanceDecision
            {
                Step = JourneyGovernanceSteps.Intake,
                Reason = "Intake automatico da jornada desabilitado no ambiente atual."
            };
        }

        var normalizedChannel = AdminKanbanJourneySourceChannels.Normalize(sourceChannel);
        if (_allowedSourceChannels.Count > 0 && !_allowedSourceChannels.Contains(normalizedChannel))
        {
            return new JourneyGovernanceDecision
            {
                Step = JourneyGovernanceSteps.Intake,
                Reason = $"Canal {AdminKanbanJourneySourceChannels.GetLabel(normalizedChannel)} fora do rollout atual da jornada."
            };
        }

        var normalizedBoardType = AdminKanbanBoardTypes.Normalize(boardType);
        var bucket = ResolveRolloutBucket($"{normalizedBoardType}:{normalizedChannel}:{stableKey}");
        if (bucket > Math.Clamp(_options.RolloutPercentage, 0, 100))
        {
            return new JourneyGovernanceDecision
            {
                Step = JourneyGovernanceSteps.Intake,
                RolloutBucket = bucket,
                Reason = "Jornada fora do percentual de rollout configurado."
            };
        }

        return new JourneyGovernanceDecision
        {
            Allowed = true,
            Step = JourneyGovernanceSteps.Intake,
            RolloutBucket = bucket,
            Reason = "Jornada liberada pela governanca."
        };
    }

    public JourneyGovernanceDecision EvaluateStep(string step, string sourceChannel)
    {
        if (!_options.Enabled)
        {
            return new JourneyGovernanceDecision
            {
                Step = step,
                Reason = "Governanca da jornada desabilitada no ambiente atual."
            };
        }

        var normalizedChannel = string.IsNullOrWhiteSpace(sourceChannel)
            ? AdminKanbanJourneySourceChannels.Landing
            : AdminKanbanJourneySourceChannels.Normalize(sourceChannel);

        if (_allowedSourceChannels.Count > 0 && !_allowedSourceChannels.Contains(normalizedChannel))
        {
            return new JourneyGovernanceDecision
            {
                Step = step,
                Reason = $"Canal {AdminKanbanJourneySourceChannels.GetLabel(normalizedChannel)} fora do rollout atual da jornada."
            };
        }

        var enabled = step switch
        {
            JourneyGovernanceSteps.Intake => _options.IntakeEnabled,
            JourneyGovernanceSteps.StageAutomation => _options.StageAutomationEnabled,
            JourneyGovernanceSteps.Matching => _options.MatchingEnabled,
            JourneyGovernanceSteps.Dispatch => _options.DispatchEnabled,
            JourneyGovernanceSteps.Connection => _options.ConnectionEnabled,
            JourneyGovernanceSteps.Closure => _options.ClosureEnabled,
            _ => true
        };

        return new JourneyGovernanceDecision
        {
            Allowed = enabled,
            Step = step,
            Reason = enabled
                ? "Etapa liberada pela governanca."
                : $"Etapa {step} desabilitada na governanca da jornada."
        };
    }

    public JourneyOperationalExceptionPolicy ResolveOperationalException(string reasonCode, string fallbackSummary)
    {
        var normalizedReasonCode = (reasonCode ?? string.Empty).Trim().ToLowerInvariant();
        return normalizedReasonCode switch
        {
            JourneyGovernanceReasonCodes.PendingDataTimeout => new JourneyOperationalExceptionPolicy
            {
                ReasonCode = normalizedReasonCode,
                Summary = "Cliente nao respondeu aos dados minimos dentro do SLA automatico; jornada encaminhada para excecao operacional.",
                HistoryEventType = "jornada_handoff_dados_pendentes_vencido",
                HandoffReason = _options.RouteOperationalExceptionsToHandoff
                    ? "Dados pendentes vencidos"
                    : "Excecao operacional"
            },
            JourneyGovernanceReasonCodes.ScheduleConfirmationTimeout => new JourneyOperationalExceptionPolicy
            {
                ReasonCode = normalizedReasonCode,
                Summary = "Cliente nao confirmou a agenda dentro do SLA automatico; jornada encaminhada para excecao operacional.",
                HistoryEventType = "jornada_handoff_agenda_pendente_vencida",
                HandoffReason = _options.RouteOperationalExceptionsToHandoff
                    ? "Confirmacao da agenda vencida"
                    : "Excecao operacional"
            },
            JourneyGovernanceReasonCodes.MatchingMissingData => new JourneyOperationalExceptionPolicy
            {
                ReasonCode = normalizedReasonCode,
                Summary = "Matching bloqueado por falta de dados minimos de categoria, localizacao ou janela; jornada encaminhada para excecao operacional.",
                HistoryEventType = "jornada_handoff_matching_dados_insuficientes",
                HandoffReason = _options.RouteOperationalExceptionsToHandoff
                    ? "Matching com dados insuficientes"
                    : "Excecao operacional"
            },
            JourneyGovernanceReasonCodes.ClientContestation => new JourneyOperationalExceptionPolicy
            {
                ReasonCode = normalizedReasonCode,
                Summary = "Cliente contestou a conclusao do atendimento; jornada encaminhada para excecao operacional.",
                HistoryEventType = "jornada_handoff_contestacao_cliente",
                HandoffReason = _options.RouteOperationalExceptionsToHandoff
                    ? "Contestacao do cliente"
                    : "Excecao operacional"
            },
            JourneyGovernanceReasonCodes.ProviderOutcomeException => new JourneyOperationalExceptionPolicy
            {
                ReasonCode = normalizedReasonCode,
                Summary = "Prestador registrou desfecho fora do fluxo padrao; jornada encaminhada para excecao operacional.",
                HistoryEventType = "jornada_handoff_desfecho_prestador_excecao",
                HandoffReason = _options.RouteOperationalExceptionsToHandoff
                    ? "Desfecho do prestador exige revisao"
                    : "Excecao operacional"
            },
            _ => new JourneyOperationalExceptionPolicy
            {
                ReasonCode = normalizedReasonCode,
                Summary = fallbackSummary,
                HistoryEventType = "jornada_handoff_excecao_operacional",
                HandoffReason = _options.RouteOperationalExceptionsToHandoff
                    ? "Excecao operacional"
                    : "Excecao operacional"
            }
        };
    }

    private static int ResolveRolloutBucket(string stableKey)
    {
        if (string.IsNullOrWhiteSpace(stableKey))
        {
            return 100;
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(stableKey.Trim()));
        var value = BitConverter.ToUInt32(bytes, 0);
        return (int)(value % 100) + 1;
    }
}
