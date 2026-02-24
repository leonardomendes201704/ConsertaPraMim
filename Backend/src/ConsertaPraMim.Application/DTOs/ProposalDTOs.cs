using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.DTOs;

public record CreateProposalDto(
    Guid RequestId,
    decimal? EstimatedValue,
    string? Message,
    int? EstimatedLeadTimeHours = null,
    int? WarrantyDays = null);

public record ProposalDto(
    Guid Id, 
    Guid RequestId, 
    Guid ProviderId, 
    string ProviderName, 
    decimal? EstimatedValue, 
    bool Accepted, 
    string? Message, 
    DateTime CreatedAt,
    int? EstimatedLeadTimeHours = null,
    int? WarrantyDays = null,
    bool Invalidated = false,
    decimal? QualityScore = null,
    decimal? QualityCompletenessScore = null,
    decimal? QualityClarityScore = null,
    decimal? QualityHistoryScore = null,
    decimal? QualityCommercialScore = null,
    DateTime? QualityCalculatedAtUtc = null,
    ProviderTrustStatus ProviderTrustStatus = ProviderTrustStatus.Pending,
    ProviderRiskLevel ProviderRiskLevel = ProviderRiskLevel.Low,
    DateTime? ProviderTrustStatusUpdatedAtUtc = null,
    string? ProviderTrustStatusReason = null);
