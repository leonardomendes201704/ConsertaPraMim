using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.DTOs;

public record TelegramChatbotEligibleProviderDto(
    Guid ProviderId,
    string ProviderName,
    double DistanceKm,
    double Rating,
    int ReviewCount,
    double CoverageRadiusKm,
    string? BaseZipCode,
    IReadOnlyList<ServiceCategory> Categories);

public record TelegramChatbotEligibleProvidersResultDto(
    bool Success,
    Guid ServiceRequestId,
    IReadOnlyList<TelegramChatbotEligibleProviderDto> Providers,
    string? ErrorCode = null,
    string? ErrorMessage = null);
