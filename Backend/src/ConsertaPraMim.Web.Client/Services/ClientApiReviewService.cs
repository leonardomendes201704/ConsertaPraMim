using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Web.Client.Services;

public class ClientApiReviewService : IReviewService
{
    private readonly ClientApiCaller _apiCaller;

    public ClientApiReviewService(ClientApiCaller apiCaller)
    {
        _apiCaller = apiCaller;
    }

    public Task<bool> SubmitReviewAsync(Guid clientId, CreateReviewDto dto) =>
        SubmitClientReviewAsync(clientId, dto);

    public async Task<bool> SubmitClientReviewAsync(Guid clientId, CreateReviewDto dto)
    {
        var response = await _apiCaller.SendAsync<object>(HttpMethod.Post, "/api/reviews/client", dto);
        return response.Success;
    }

    public async Task<bool> SubmitProviderReviewAsync(Guid providerId, CreateReviewDto dto)
    {
        var response = await _apiCaller.SendAsync<object>(HttpMethod.Post, "/api/reviews/provider", dto);
        return response.Success;
    }

    public async Task<IEnumerable<ReviewDto>> GetByProviderAsync(Guid providerId)
    {
        var response = await _apiCaller.SendAsync<List<ReviewDto>>(HttpMethod.Get, $"/api/reviews/provider/{providerId}");
        return response.Payload ?? [];
    }

    public async Task<IEnumerable<ReviewDto>> GetByClientAsync(Guid clientId)
    {
        var response = await _apiCaller.SendAsync<List<ReviewDto>>(HttpMethod.Get, $"/api/reviews/client/{clientId}");
        return response.Payload ?? [];
    }

    public async Task<bool> ReportReviewAsync(Guid reviewId, Guid actorUserId, UserRole actorRole, ReportReviewDto dto)
    {
        var response = await _apiCaller.SendAsync<object>(HttpMethod.Post, $"/api/reviews/{reviewId}/report", dto);
        return response.Success;
    }

    public Task<IEnumerable<ReviewDto>> GetReportedReviewsAsync() =>
        Task.FromResult<IEnumerable<ReviewDto>>([]);

    public Task<bool> ModerateReviewAsync(Guid reviewId, Guid adminUserId, ModerateReviewDto dto) =>
        Task.FromResult(false);

    public async Task<ReviewScoreSummaryDto> GetProviderScoreSummaryAsync(Guid providerId)
    {
        var response = await _apiCaller.SendAsync<ReviewScoreSummaryDto>(HttpMethod.Get, $"/api/reviews/summary/provider/{providerId}");
        return response.Payload ?? new ReviewScoreSummaryDto(providerId, UserRole.Provider, 0, 0, 0, 0, 0, 0, 0);
    }

    public async Task<ReviewScoreSummaryDto> GetClientScoreSummaryAsync(Guid clientId)
    {
        var response = await _apiCaller.SendAsync<ReviewScoreSummaryDto>(HttpMethod.Get, $"/api/reviews/summary/client/{clientId}");
        return response.Payload ?? new ReviewScoreSummaryDto(clientId, UserRole.Client, 0, 0, 0, 0, 0, 0, 0);
    }
}
