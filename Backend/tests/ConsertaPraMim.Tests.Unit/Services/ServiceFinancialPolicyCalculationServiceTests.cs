using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class ServiceFinancialPolicyCalculationServiceTests
{
    private readonly Mock<IServiceFinancialPolicyRuleRepository> _policyRuleRepositoryMock;
    private readonly ServiceFinancialPolicyCalculationService _service;

    public ServiceFinancialPolicyCalculationServiceTests()
    {
        _policyRuleRepositoryMock = new Mock<IServiceFinancialPolicyRuleRepository>();
        _service = new ServiceFinancialPolicyCalculationService(_policyRuleRepositoryMock.Object);
    }

    [Fact]
    public async Task CalculateAsync_ShouldApplyRuleByAntecedenceWindow()
    {
        var nowUtc = DateTime.UtcNow;
        var rules = new List<ServiceFinancialPolicyRule>
        {
            BuildRule(
                "Client cancelamento > 24h",
                ServiceFinancialPolicyEventType.ClientCancellation,
                minHours: 24,
                maxHours: null,
                priority: 1,
                penaltyPercent: 0m,
                counterpartyPercent: 0m,
                platformPercent: 0m),
            BuildRule(
                "Client cancelamento 4h-24h",
                ServiceFinancialPolicyEventType.ClientCancellation,
                minHours: 4,
                maxHours: 24,
                priority: 2,
                penaltyPercent: 20m,
                counterpartyPercent: 15m,
                platformPercent: 5m),
            BuildRule(
                "Client cancelamento < 4h",
                ServiceFinancialPolicyEventType.ClientCancellation,
                minHours: 0,
                maxHours: 3,
                priority: 3,
                penaltyPercent: 40m,
                counterpartyPercent: 30m,
                platformPercent: 10m)
        };

        _policyRuleRepositoryMock
            .Setup(r => r.GetActiveByEventTypeAsync(ServiceFinancialPolicyEventType.ClientCancellation))
            .ReturnsAsync(rules);

        var result = await _service.CalculateAsync(new ServiceFinancialCalculationRequestDto(
            ServiceFinancialPolicyEventType.ClientCancellation,
            ServiceValue: 200m,
            WindowStartUtc: nowUtc.AddHours(10),
            EventOccurredAtUtc: nowUtc));

        Assert.True(result.Success);
        Assert.NotNull(result.Breakdown);
        Assert.Equal("Client cancelamento 4h-24h", result.Breakdown!.RuleName);
        Assert.Equal(40m, result.Breakdown.PenaltyAmount);
        Assert.Equal(30m, result.Breakdown.CounterpartyCompensationAmount);
        Assert.Equal(10m, result.Breakdown.PlatformRetainedAmount);
        Assert.Equal(160m, result.Breakdown.RemainingAmount);
    }

    [Fact]
    public async Task CalculateAsync_ShouldClampNegativeAntecedenceToZero_ForNoShow()
    {
        var nowUtc = DateTime.UtcNow;
        var noShowRule = BuildRule(
            "Client no-show",
            ServiceFinancialPolicyEventType.ClientNoShow,
            minHours: 0,
            maxHours: null,
            priority: 1,
            penaltyPercent: 60m,
            counterpartyPercent: 45m,
            platformPercent: 15m);

        _policyRuleRepositoryMock
            .Setup(r => r.GetActiveByEventTypeAsync(ServiceFinancialPolicyEventType.ClientNoShow))
            .ReturnsAsync(new List<ServiceFinancialPolicyRule> { noShowRule });

        var result = await _service.CalculateAsync(new ServiceFinancialCalculationRequestDto(
            ServiceFinancialPolicyEventType.ClientNoShow,
            ServiceValue: 150m,
            WindowStartUtc: nowUtc.AddHours(-2),
            EventOccurredAtUtc: nowUtc));

        Assert.True(result.Success);
        Assert.NotNull(result.Breakdown);
        Assert.Equal(0d, result.Breakdown!.HoursBeforeWindowStart);
        Assert.Equal(90m, result.Breakdown.PenaltyAmount);
        Assert.Equal(67.50m, result.Breakdown.CounterpartyCompensationAmount);
        Assert.Equal(22.50m, result.Breakdown.PlatformRetainedAmount);
        Assert.Equal(60m, result.Breakdown.RemainingAmount);
        Assert.Equal("Provider", result.Breakdown.CounterpartyActorLabel);
    }

    [Fact]
    public async Task CalculateAsync_ShouldReturnError_WhenNoRuleMatches()
    {
        _policyRuleRepositoryMock
            .Setup(r => r.GetActiveByEventTypeAsync(ServiceFinancialPolicyEventType.ProviderNoShow))
            .ReturnsAsync(Array.Empty<ServiceFinancialPolicyRule>());

        var result = await _service.CalculateAsync(new ServiceFinancialCalculationRequestDto(
            ServiceFinancialPolicyEventType.ProviderNoShow,
            ServiceValue: 300m,
            WindowStartUtc: DateTime.UtcNow.AddHours(1),
            EventOccurredAtUtc: DateTime.UtcNow));

        Assert.False(result.Success);
        Assert.Equal("policy_rule_not_found", result.ErrorCode);
        Assert.Null(result.Breakdown);
    }

    [Fact]
    public async Task CalculateAsync_ShouldReturnError_WhenServiceValueIsInvalid()
    {
        var result = await _service.CalculateAsync(new ServiceFinancialCalculationRequestDto(
            ServiceFinancialPolicyEventType.ClientCancellation,
            ServiceValue: 0m,
            WindowStartUtc: DateTime.UtcNow.AddHours(8),
            EventOccurredAtUtc: DateTime.UtcNow));

        Assert.False(result.Success);
        Assert.Equal("invalid_service_value", result.ErrorCode);
    }

    [Fact]
    public async Task CalculateAsync_ShouldAdjustAllocatedAmounts_WhenRoundingExceedsPenalty()
    {
        var rule = BuildRule(
            "Round consistency rule",
            ServiceFinancialPolicyEventType.ProviderCancellation,
            minHours: 0,
            maxHours: null,
            priority: 1,
            penaltyPercent: 33.33m,
            counterpartyPercent: 16.67m,
            platformPercent: 16.67m);

        _policyRuleRepositoryMock
            .Setup(r => r.GetActiveByEventTypeAsync(ServiceFinancialPolicyEventType.ProviderCancellation))
            .ReturnsAsync(new List<ServiceFinancialPolicyRule> { rule });

        var result = await _service.CalculateAsync(new ServiceFinancialCalculationRequestDto(
            ServiceFinancialPolicyEventType.ProviderCancellation,
            ServiceValue: 1m,
            WindowStartUtc: DateTime.UtcNow.AddHours(3),
            EventOccurredAtUtc: DateTime.UtcNow));

        Assert.True(result.Success);
        Assert.NotNull(result.Breakdown);
        Assert.Equal(0.33m, result.Breakdown!.PenaltyAmount);
        Assert.Equal(0.17m, result.Breakdown.CounterpartyCompensationAmount);
        Assert.Equal(0.16m, result.Breakdown.PlatformRetainedAmount);
        Assert.Equal(result.Breakdown.PenaltyAmount, result.Breakdown.CounterpartyCompensationAmount + result.Breakdown.PlatformRetainedAmount);
    }

    [Fact]
    public async Task CalculateAsync_ShouldRoundMonetaryFormulaValues_AwayFromZero()
    {
        var rule = BuildRule(
            "Rounding regression rule",
            ServiceFinancialPolicyEventType.ClientCancellation,
            minHours: 0,
            maxHours: null,
            priority: 1,
            penaltyPercent: 50m,
            counterpartyPercent: 30m,
            platformPercent: 20m);

        _policyRuleRepositoryMock
            .Setup(r => r.GetActiveByEventTypeAsync(ServiceFinancialPolicyEventType.ClientCancellation))
            .ReturnsAsync(new List<ServiceFinancialPolicyRule> { rule });

        var nowUtc = DateTime.UtcNow;
        var result = await _service.CalculateAsync(new ServiceFinancialCalculationRequestDto(
            ServiceFinancialPolicyEventType.ClientCancellation,
            ServiceValue: 10.005m,
            WindowStartUtc: nowUtc.AddHours(8),
            EventOccurredAtUtc: nowUtc));

        Assert.True(result.Success);
        Assert.NotNull(result.Breakdown);
        Assert.Equal(10.01m, result.Breakdown!.ServiceValue);
        Assert.Equal(5.01m, result.Breakdown.PenaltyAmount);
        Assert.Equal(3.00m, result.Breakdown.CounterpartyCompensationAmount);
        Assert.Equal(2.00m, result.Breakdown.PlatformRetainedAmount);
        Assert.Equal(5.00m, result.Breakdown.RemainingAmount);
    }

    [Fact]
    public async Task CalculateAsync_ShouldReduceCompensation_WhenOverflowIsGreaterThanPlatformShare()
    {
        var rule = BuildRule(
            "Overflow regression rule",
            ServiceFinancialPolicyEventType.ProviderCancellation,
            minHours: 0,
            maxHours: null,
            priority: 1,
            penaltyPercent: 33.33m,
            counterpartyPercent: 40m,
            platformPercent: 5m);

        _policyRuleRepositoryMock
            .Setup(r => r.GetActiveByEventTypeAsync(ServiceFinancialPolicyEventType.ProviderCancellation))
            .ReturnsAsync(new List<ServiceFinancialPolicyRule> { rule });

        var nowUtc = DateTime.UtcNow;
        var result = await _service.CalculateAsync(new ServiceFinancialCalculationRequestDto(
            ServiceFinancialPolicyEventType.ProviderCancellation,
            ServiceValue: 1m,
            WindowStartUtc: nowUtc.AddHours(2),
            EventOccurredAtUtc: nowUtc));

        Assert.True(result.Success);
        Assert.NotNull(result.Breakdown);
        Assert.Equal(0.33m, result.Breakdown!.PenaltyAmount);
        Assert.Equal(0.33m, result.Breakdown.CounterpartyCompensationAmount);
        Assert.Equal(0m, result.Breakdown.PlatformRetainedAmount);
        Assert.Equal(0.67m, result.Breakdown.RemainingAmount);
        Assert.Equal("Client", result.Breakdown.CounterpartyActorLabel);
        Assert.Equal(result.Breakdown.PenaltyAmount, result.Breakdown.CounterpartyCompensationAmount + result.Breakdown.PlatformRetainedAmount);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(24)]
    public async Task CalculateAsync_ShouldTreatAntecedenceWindowBoundariesAsInclusive(int hoursBeforeWindowStart)
    {
        var rule = BuildRule(
            "Boundary regression rule",
            ServiceFinancialPolicyEventType.ClientCancellation,
            minHours: 4,
            maxHours: 24,
            priority: 1,
            penaltyPercent: 20m,
            counterpartyPercent: 15m,
            platformPercent: 5m);

        _policyRuleRepositoryMock
            .Setup(r => r.GetActiveByEventTypeAsync(ServiceFinancialPolicyEventType.ClientCancellation))
            .ReturnsAsync(new List<ServiceFinancialPolicyRule> { rule });

        var nowUtc = DateTime.UtcNow;
        var result = await _service.CalculateAsync(new ServiceFinancialCalculationRequestDto(
            ServiceFinancialPolicyEventType.ClientCancellation,
            ServiceValue: 100m,
            WindowStartUtc: nowUtc.AddHours(hoursBeforeWindowStart),
            EventOccurredAtUtc: nowUtc));

        Assert.True(result.Success);
        Assert.NotNull(result.Breakdown);
        Assert.Equal("Boundary regression rule", result.Breakdown!.RuleName);
        Assert.Equal(20m, result.Breakdown.PenaltyAmount);
        Assert.Equal(15m, result.Breakdown.CounterpartyCompensationAmount);
        Assert.Equal(5m, result.Breakdown.PlatformRetainedAmount);
        Assert.Equal(80m, result.Breakdown.RemainingAmount);
    }

    private static ServiceFinancialPolicyRule BuildRule(
        string name,
        ServiceFinancialPolicyEventType eventType,
        int minHours,
        int? maxHours,
        int priority,
        decimal penaltyPercent,
        decimal counterpartyPercent,
        decimal platformPercent)
    {
        return new ServiceFinancialPolicyRule
        {
            Id = Guid.NewGuid(),
            Name = name,
            EventType = eventType,
            MinHoursBeforeWindowStart = minHours,
            MaxHoursBeforeWindowStart = maxHours,
            Priority = priority,
            PenaltyPercent = penaltyPercent,
            CounterpartyCompensationPercent = counterpartyPercent,
            PlatformRetainedPercent = platformPercent,
            IsActive = true
        };
    }
}
