namespace ConsertaPraMim.Application.DTOs;

public record ServiceRequestProblemAnalysisRequestDto(
    Guid CategoryId,
    string Description);

public record ServiceRequestProblemAnalysisResultDto(
    bool Success,
    string CategoryName,
    string UnderstandingSummary,
    IReadOnlyList<string> Highlights,
    bool UsedFallback,
    string? Model = null,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    DateTime? GeneratedAtUtc = null);
