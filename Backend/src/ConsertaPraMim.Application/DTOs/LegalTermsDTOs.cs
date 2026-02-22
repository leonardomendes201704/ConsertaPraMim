namespace ConsertaPraMim.Application.DTOs;

public record LegalTermsDocumentDto(
    Guid Id,
    string Audience,
    int Version,
    string Title,
    string HtmlContent,
    string? ChangeSummary,
    bool IsPublished,
    DateTime? PublishedAtUtc,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record LegalTermsPublishPayloadDto(
    string Title,
    string HtmlContent,
    string? ChangeSummary);

public record LegalTermsPublishResultDto(
    bool Success,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    LegalTermsDocumentDto? Document = null);
