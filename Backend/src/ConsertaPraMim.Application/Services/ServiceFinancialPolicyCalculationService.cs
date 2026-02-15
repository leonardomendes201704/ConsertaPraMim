using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using System.Globalization;

namespace ConsertaPraMim.Application.Services;

public class ServiceFinancialPolicyCalculationService : IServiceFinancialPolicyCalculationService
{
    private static readonly CultureInfo PtBrCulture = CultureInfo.GetCultureInfo("pt-BR");
    private readonly IServiceFinancialPolicyRuleRepository _policyRuleRepository;

    public ServiceFinancialPolicyCalculationService(IServiceFinancialPolicyRuleRepository policyRuleRepository)
    {
        _policyRuleRepository = policyRuleRepository;
    }

    public async Task<ServiceFinancialCalculationResultDto> CalculateAsync(
        ServiceFinancialCalculationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request.ServiceValue <= 0m)
        {
            return new ServiceFinancialCalculationResultDto(
                false,
                ErrorCode: "invalid_service_value",
                ErrorMessage: "Valor do servico deve ser maior que zero.");
        }

        var normalizedServiceValue = RoundMoney(request.ServiceValue);
        var windowStartUtc = NormalizeToUtc(request.WindowStartUtc);
        var eventOccurredAtUtc = NormalizeToUtc(request.EventOccurredAtUtc);

        var rawHoursBeforeWindowStart = (windowStartUtc - eventOccurredAtUtc).TotalHours;
        var effectiveHoursBeforeWindowStart = Math.Max(0d, rawHoursBeforeWindowStart);

        var activeRules = await _policyRuleRepository.GetActiveByEventTypeAsync(request.EventType);
        var matchedRule = ResolveRule(activeRules, effectiveHoursBeforeWindowStart);
        if (matchedRule == null)
        {
            return new ServiceFinancialCalculationResultDto(
                false,
                ErrorCode: "policy_rule_not_found",
                ErrorMessage: "Nenhuma regra financeira ativa encontrada para o evento informado.");
        }

        var penaltyAmount = RoundMoney(normalizedServiceValue * (matchedRule.PenaltyPercent / 100m));
        var counterpartyCompensationAmount = RoundMoney(normalizedServiceValue * (matchedRule.CounterpartyCompensationPercent / 100m));
        var platformRetainedAmount = RoundMoney(normalizedServiceValue * (matchedRule.PlatformRetainedPercent / 100m));

        EnsurePenaltyConsistency(
            penaltyAmount,
            ref counterpartyCompensationAmount,
            ref platformRetainedAmount);

        var remainingAmount = RoundMoney(Math.Max(0m, normalizedServiceValue - penaltyAmount));
        var counterpartyActor = ResolveCounterpartyActorLabel(request.EventType);

        var memo =
            $"Evento={request.EventType}; Regra='{matchedRule.Name}'; " +
            $"AntecedenciaHoras={FormatNumberPtBr(effectiveHoursBeforeWindowStart)}; " +
            $"ValorBase={FormatCurrencyPtBr(normalizedServiceValue)}; " +
            $"Multa={FormatPercentPtBr(matchedRule.PenaltyPercent)}({FormatCurrencyPtBr(penaltyAmount)}); " +
            $"Compensacao={FormatPercentPtBr(matchedRule.CounterpartyCompensationPercent)}({FormatCurrencyPtBr(counterpartyCompensationAmount)}); " +
            $"RetencaoPlataforma={FormatPercentPtBr(matchedRule.PlatformRetainedPercent)}({FormatCurrencyPtBr(platformRetainedAmount)}); " +
            $"SaldoRemanescente={FormatCurrencyPtBr(remainingAmount)}.";

        var breakdown = new ServiceFinancialCalculationBreakdownDto(
            matchedRule.Id,
            matchedRule.Name,
            request.EventType,
            normalizedServiceValue,
            Math.Round(effectiveHoursBeforeWindowStart, 2, MidpointRounding.AwayFromZero),
            matchedRule.MinHoursBeforeWindowStart,
            matchedRule.MaxHoursBeforeWindowStart,
            matchedRule.Priority,
            matchedRule.PenaltyPercent,
            penaltyAmount,
            matchedRule.CounterpartyCompensationPercent,
            counterpartyCompensationAmount,
            matchedRule.PlatformRetainedPercent,
            platformRetainedAmount,
            remainingAmount,
            counterpartyActor,
            memo);

        return new ServiceFinancialCalculationResultDto(true, breakdown);
    }

    private static ServiceFinancialPolicyRule? ResolveRule(
        IReadOnlyList<ServiceFinancialPolicyRule> rules,
        double hoursBeforeWindowStart)
    {
        return rules.FirstOrDefault(rule =>
            hoursBeforeWindowStart >= rule.MinHoursBeforeWindowStart &&
            (!rule.MaxHoursBeforeWindowStart.HasValue || hoursBeforeWindowStart <= rule.MaxHoursBeforeWindowStart.Value));
    }

    private static void EnsurePenaltyConsistency(
        decimal penaltyAmount,
        ref decimal counterpartyCompensationAmount,
        ref decimal platformRetainedAmount)
    {
        var allocatedAmount = counterpartyCompensationAmount + platformRetainedAmount;
        if (allocatedAmount <= penaltyAmount)
        {
            return;
        }

        var overflow = allocatedAmount - penaltyAmount;
        if (platformRetainedAmount >= overflow)
        {
            platformRetainedAmount = RoundMoney(platformRetainedAmount - overflow);
            return;
        }

        overflow -= platformRetainedAmount;
        platformRetainedAmount = 0m;
        counterpartyCompensationAmount = RoundMoney(Math.Max(0m, counterpartyCompensationAmount - overflow));
    }

    private static string ResolveCounterpartyActorLabel(ServiceFinancialPolicyEventType eventType)
    {
        return eventType switch
        {
            ServiceFinancialPolicyEventType.ClientCancellation => "Provider",
            ServiceFinancialPolicyEventType.ClientNoShow => "Provider",
            ServiceFinancialPolicyEventType.ProviderCancellation => "Client",
            ServiceFinancialPolicyEventType.ProviderNoShow => "Client",
            _ => "Counterparty"
        };
    }

    private static decimal RoundMoney(decimal value)
    {
        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static string FormatCurrencyPtBr(decimal value)
    {
        return value.ToString("C2", PtBrCulture);
    }

    private static string FormatPercentPtBr(decimal value)
    {
        return $"{value.ToString("0.##", PtBrCulture)}%";
    }

    private static string FormatNumberPtBr(double value)
    {
        return value.ToString("0.##", PtBrCulture);
    }

    private static DateTime NormalizeToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
