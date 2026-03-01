using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Web.Provider.Services;

public class ProviderApiReviewService : IReviewService
{
    private readonly ProviderApiCaller _apiCaller;

    public ProviderApiReviewService(ProviderApiCaller apiCaller)
    {
        _apiCaller = apiCaller;
    }

    public Task<bool> SubmitReviewAsync(Guid clientId, CreateReviewDto dto) =>
        SubmitClientReviewAsync(clientId, dto);

    public async Task<bool> SubmitClientReviewAsync(Guid clientId, CreateReviewDto dto)
    {
        var result = await SubmitClientReviewDetailedAsync(clientId, dto);
        return result.Success;
    }

    public async Task<bool> SubmitProviderReviewAsync(Guid providerId, CreateReviewDto dto)
    {
        var result = await SubmitProviderReviewDetailedAsync(providerId, dto);
        return result.Success;
    }

    public async Task<ReviewSubmissionResultDto> SubmitClientReviewDetailedAsync(Guid clientId, CreateReviewDto dto)
    {
        var response = await _apiCaller.SendAsync<object>(HttpMethod.Post, "/api/reviews/client", dto);
        return response.Success
            ? new ReviewSubmissionResultDto(true)
            : new ReviewSubmissionResultDto(false, $"http_{(int)response.StatusCode}", response.ErrorMessage);
    }

    public async Task<ReviewSubmissionResultDto> SubmitProviderReviewDetailedAsync(Guid providerId, CreateReviewDto dto)
    {
        var response = await _apiCaller.SendAsync<object>(HttpMethod.Post, "/api/reviews/provider", dto);
        return response.Success
            ? new ReviewSubmissionResultDto(true)
            : new ReviewSubmissionResultDto(false, $"http_{(int)response.StatusCode}", response.ErrorMessage);
    }

    public async Task<IReadOnlyList<ReviewPendingRequestDto>> GetPendingClientReviewsAsync(Guid clientId, int take = 20)
    {
        var response = await _apiCaller.SendAsync<List<ReviewPendingRequestDto>>(
            HttpMethod.Get,
            $"/api/reviews/client/pending?take={Math.Clamp(take, 1, 100)}");
        return response.Payload ?? [];
    }

    public async Task<IReadOnlyList<ReviewPendingRequestDto>> GetPendingProviderReviewsAsync(Guid providerId, int take = 20)
    {
        var response = await _apiCaller.SendAsync<List<ReviewPendingRequestDto>>(
            HttpMethod.Get,
            $"/api/reviews/provider/pending?take={Math.Clamp(take, 1, 100)}");
        return response.Payload ?? [];
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

