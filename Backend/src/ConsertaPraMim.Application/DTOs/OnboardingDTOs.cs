using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.DTOs;

public record ProviderOnboardingStateDto(
    string Name,
    string Email,
    string Phone,
    ProviderPlan SelectedPlan,
    ProviderOnboardingStatus Status,
    bool BasicDataCompleted,
    bool PlanCompleted,
    bool DocumentsCompleted,
    bool IsCompleted,
    DateTime? StartedAt,
    DateTime? PlanSelectedAt,
    DateTime? DocumentsSubmittedAt,
    DateTime? CompletedAt,
    IReadOnlyList<ProviderOnboardingDocumentDto> Documents,
    IReadOnlyList<ProviderPlanOfferDto> PlanOffers,
    bool HasOperationalCompliancePending,
    string? OperationalComplianceNotes);

public record ProviderOnboardingDocumentDto(
    Guid Id,
    ProviderDocumentType DocumentType,
    ProviderDocumentStatus Status,
    string FileName,
    string MimeType,
    long SizeBytes,
    string FileUrl,
    DateTime CreatedAt,
    string? RejectionReason);

public record UpdateProviderOnboardingBasicDataDto(string Name, string Phone);

public record SaveProviderOnboardingPlanDto(ProviderPlan Plan);

public record AddProviderOnboardingDocumentDto(
    ProviderDocumentType DocumentType,
    string FileName,
    string MimeType,
    long SizeBytes,
    string FileUrl,
    string? FileHashSha256);

public record CompleteProviderOnboardingResult(bool Success, string? ErrorMessage);
