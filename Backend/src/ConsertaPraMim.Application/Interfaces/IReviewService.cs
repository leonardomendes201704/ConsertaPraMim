using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IReviewService
{
    Task<bool> SubmitReviewAsync(Guid clientId, CreateReviewDto dto);
    Task<IEnumerable<ReviewDto>> GetByProviderAsync(Guid providerId);
}
