namespace ConsertaPraMim.Application.DTOs;

public record AdminChecklistTemplateItemDto(
    Guid Id,
    string Title,
    string? HelpText,
    bool IsRequired,
    bool RequiresEvidence,
    bool AllowNote,
    bool IsActive,
    int SortOrder,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record AdminChecklistTemplateDto(
    Guid Id,
    Guid CategoryDefinitionId,
    string CategoryName,
    string LegacyCategory,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<AdminChecklistTemplateItemDto> Items);

public record AdminChecklistTemplateItemUpsertDto(
    Guid? Id,
    string Title,
    string? HelpText,
    bool IsRequired,
    bool RequiresEvidence,
    bool AllowNote,
    bool IsActive,
    int SortOrder);

public record AdminCreateChecklistTemplateRequestDto(
    Guid CategoryDefinitionId,
    string Name,
    string? Description,
    IReadOnlyList<AdminChecklistTemplateItemUpsertDto> Items);

public record AdminUpdateChecklistTemplateRequestDto(
    string Name,
    string? Description,
    IReadOnlyList<AdminChecklistTemplateItemUpsertDto> Items);

public record AdminUpdateChecklistTemplateStatusRequestDto(
    bool IsActive,
    string? Reason);

public record AdminChecklistTemplateUpsertResultDto(
    bool Success,
    AdminChecklistTemplateDto? Template = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record ServiceChecklistItemDto(
    Guid TemplateItemId,
    string Title,
    string? HelpText,
    bool IsRequired,
    bool RequiresEvidence,
    bool AllowNote,
    int SortOrder,
    bool IsChecked,
    string? Note,
    string? EvidenceUrl,
    string? EvidenceFileName,
    string? EvidenceContentType,
    long? EvidenceSizeBytes,
    Guid? CheckedByUserId,
    DateTime? CheckedAtUtc);

public record ServiceChecklistHistoryDto(
    Guid Id,
    Guid TemplateItemId,
    string ItemTitle,
    bool? PreviousIsChecked,
    bool NewIsChecked,
    string? PreviousNote,
    string? NewNote,
    string? PreviousEvidenceUrl,
    string? NewEvidenceUrl,
    Guid ActorUserId,
    string ActorRole,
    DateTime OccurredAtUtc);

public record ServiceAppointmentChecklistDto(
    Guid AppointmentId,
    Guid? TemplateId,
    string? TemplateName,
    string CategoryName,
    bool IsRequiredChecklist,
    int RequiredItemsCount,
    int RequiredCompletedCount,
    IReadOnlyList<ServiceChecklistItemDto> Items,
    IReadOnlyList<ServiceChecklistHistoryDto> History);

public record UpsertServiceChecklistItemResponseRequestDto(
    Guid TemplateItemId,
    bool IsChecked,
    string? Note,
    string? EvidenceUrl,
    string? EvidenceFileName,
    string? EvidenceContentType,
    long? EvidenceSizeBytes,
    bool ClearEvidence = false);

public record ServiceAppointmentChecklistResultDto(
    bool Success,
    ServiceAppointmentChecklistDto? Checklist = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record ServiceAppointmentChecklistValidationResultDto(
    bool Success,
    bool CanComplete,
    int PendingRequiredCount,
    IReadOnlyList<string> PendingRequiredItems,
    string? ErrorCode = null,
    string? ErrorMessage = null);
