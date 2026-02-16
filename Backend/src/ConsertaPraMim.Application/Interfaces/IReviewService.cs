using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.Interfaces;

public interface IReviewService
{
    Task<bool> SubmitReviewAsync(Guid clientId, CreateReviewDto dto);
    Task<bool> SubmitClientReviewAsync(Guid clientId, CreateReviewDto dto);
    Task<bool> SubmitProviderReviewAsync(Guid providerId, CreateReviewDto dto);
    Task<IEnumerable<ReviewDto>> GetByProviderAsync(Guid providerId);
    Task<bool> ReportReviewAsync(Guid reviewId, Guid actorUserId, UserRole actorRole, ReportReviewDto dto);
    Task<IEnumerable<ReviewDto>> GetReportedReviewsAsync();
    Task<bool> ModerateReviewAsync(Guid reviewId, Guid adminUserId, ModerateReviewDto dto);
    Task<ReviewScoreSummaryDto> GetProviderScoreSummaryAsync(Guid providerId);
    Task<ReviewScoreSummaryDto> GetClientScoreSummaryAsync(Guid clientId);
}
