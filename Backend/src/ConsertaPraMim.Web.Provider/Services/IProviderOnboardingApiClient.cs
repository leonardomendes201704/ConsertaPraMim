using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Web.Provider.Services;

public interface IProviderOnboardingApiClient
{
    Task<(ProviderOnboardingStateDto? State, string? ErrorMessage)> GetStateAsync(CancellationToken cancellationToken = default);
    Task<(ProviderOnboardingStateDto? State, string? ErrorMessage)> GetStateAsync(string bearerToken, CancellationToken cancellationToken = default);
}
