using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.Interfaces;

public interface IProviderOnboardingService
{
    Task<ProviderOnboardingStateDto?> GetStateAsync(Guid userId);
    Task<bool> SaveBasicDataAsync(Guid userId, UpdateProviderOnboardingBasicDataDto dto);
    Task<bool> SavePlanAsync(Guid userId, ProviderPlan plan);
    Task<ProviderOnboardingDocumentDto?> AddDocumentAsync(Guid userId, AddProviderOnboardingDocumentDto dto);
    Task<(bool Success, string? FileUrl)> RemoveDocumentAsync(Guid userId, Guid documentId);
    Task<CompleteProviderOnboardingResult> CompleteAsync(Guid userId);
    Task<bool> IsOnboardingCompleteAsync(Guid userId);
}
