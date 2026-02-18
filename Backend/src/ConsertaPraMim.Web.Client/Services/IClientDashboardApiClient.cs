using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Web.Client.Services;

public interface IClientDashboardApiClient
{
    Task<(IReadOnlyList<ServiceRequestDto> Requests, string? ErrorMessage)> GetMyRequestsAsync(CancellationToken cancellationToken = default);
}
