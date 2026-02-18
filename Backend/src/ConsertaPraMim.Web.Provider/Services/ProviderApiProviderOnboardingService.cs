using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Web.Provider.Services;

public class ProviderApiProviderOnboardingService : IProviderOnboardingService
{
    private readonly ProviderApiCaller _apiCaller;

    public ProviderApiProviderOnboardingService(ProviderApiCaller apiCaller)
    {
        _apiCaller = apiCaller;
    }

    public async Task<ProviderOnboardingStateDto?> GetStateAsync(Guid userId)
    {
        var response = await _apiCaller.SendAsync<ProviderOnboardingStateDto>(HttpMethod.Get, "/api/provider-onboarding");
        return response.Payload;
    }

    public async Task<bool> SaveBasicDataAsync(Guid userId, UpdateProviderOnboardingBasicDataDto dto)
    {
        var response = await _apiCaller.SendAsync<object>(HttpMethod.Put, "/api/provider-onboarding/basic-data", dto);
        return response.Success;
    }

    public async Task<bool> SavePlanAsync(Guid userId, ProviderPlan plan)
    {
        var response = await _apiCaller.SendAsync<object>(
            HttpMethod.Put,
            "/api/provider-onboarding/plan",
            new SaveProviderOnboardingPlanDto(plan));
        return response.Success;
    }

    public async Task<ProviderOnboardingDocumentDto?> AddDocumentAsync(Guid userId, AddProviderOnboardingDocumentDto dto)
    {
        var response = await _apiCaller.SendAsync<ProviderOnboardingDocumentDto>(
            HttpMethod.Post,
            "/api/provider-onboarding/documents/register",
            dto);

        return response.Payload;
    }

    public async Task<(bool Success, string? FileUrl)> RemoveDocumentAsync(Guid userId, Guid documentId)
    {
        var response = await _apiCaller.SendAsync<object>(HttpMethod.Delete, $"/api/provider-onboarding/documents/{documentId}");
        return (response.Success, null);
    }

    public async Task<CompleteProviderOnboardingResult> CompleteAsync(Guid userId)
    {
        var response = await _apiCaller.SendAsync<CompleteProviderOnboardingResult>(HttpMethod.Post, "/api/provider-onboarding/complete");
        return response.Payload ?? new CompleteProviderOnboardingResult(false, response.ErrorMessage ?? "Nao foi possivel concluir onboarding.");
    }

    public async Task<bool> IsOnboardingCompleteAsync(Guid userId)
    {
        var state = await GetStateAsync(userId);
        return state != null && (state.IsCompleted || state.Status == ProviderOnboardingStatus.Active);
    }
}
