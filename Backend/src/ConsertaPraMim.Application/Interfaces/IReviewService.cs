using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IReviewService
{
    Task<bool> SubmitReviewAsync(Guid clientId, CreateReviewDto dto);
    Task<bool> SubmitClientReviewAsync(Guid clientId, CreateReviewDto dto);
    Task<bool> SubmitProviderReviewAsync(Guid providerId, CreateReviewDto dto);
    Task<IEnumerable<ReviewDto>> GetByProviderAsync(Guid providerId);
    Task<ReviewScoreSummaryDto> GetProviderScoreSummaryAsync(Guid providerId);
    Task<ReviewScoreSummaryDto> GetClientScoreSummaryAsync(Guid clientId);
}
