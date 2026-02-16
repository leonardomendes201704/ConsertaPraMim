using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.Services;

public class ReviewService : IReviewService
{
    private readonly IReviewRepository _reviewRepository;
    private readonly IServiceRequestRepository _requestRepository;
    private readonly IUserRepository _userRepository;

    public ReviewService(
        IReviewRepository reviewRepository, 
        IServiceRequestRepository requestRepository,
        IUserRepository userRepository)
    {
        _reviewRepository = reviewRepository;
        _requestRepository = requestRepository;
        _userRepository = userRepository;
    }

    public async Task<bool> SubmitReviewAsync(Guid clientId, CreateReviewDto dto)
    {
        return await SubmitClientReviewAsync(clientId, dto);
    }

    public async Task<bool> SubmitClientReviewAsync(Guid clientId, CreateReviewDto dto)
    {
        var request = await _requestRepository.GetByIdAsync(dto.RequestId);
        if (request == null || !CanReviewStatus(request.Status)) return false;

        // Security and Logic checks
        if (request.ClientId != clientId) return false;

        // Check if already reviewed
        var existingReview = await _reviewRepository.GetByRequestAndReviewerAsync(dto.RequestId, clientId);
        if (existingReview != null) return false;

        // Extract provider ID from accepted proposal
        var acceptedProposal = request.Proposals.FirstOrDefault(p => p.Accepted);
        if (acceptedProposal == null) return false;

        var review = new Review
        {
            RequestId = dto.RequestId,
            ClientId = clientId,
            ProviderId = acceptedProposal.ProviderId,
            ReviewerUserId = clientId,
            ReviewerRole = UserRole.Client,
            RevieweeUserId = acceptedProposal.ProviderId,
            RevieweeRole = UserRole.Provider,
            Rating = dto.Rating,
            Comment = dto.Comment
        };

        await _reviewRepository.AddAsync(review);

        // Update Provider Rating
        await UpdateProviderRating(acceptedProposal.ProviderId, dto.Rating);

        return true;
    }

    public async Task<bool> SubmitProviderReviewAsync(Guid providerId, CreateReviewDto dto)
    {
        var request = await _requestRepository.GetByIdAsync(dto.RequestId);
        if (request == null || !CanReviewStatus(request.Status)) return false;

        var acceptedProposal = request.Proposals.FirstOrDefault(p => p.Accepted && p.ProviderId == providerId);
        if (acceptedProposal == null) return false;

        var existingReview = await _reviewRepository.GetByRequestAndReviewerAsync(dto.RequestId, providerId);
        if (existingReview != null) return false;

        var review = new Review
        {
            RequestId = dto.RequestId,
            ClientId = request.ClientId,
            ProviderId = providerId,
            ReviewerUserId = providerId,
            ReviewerRole = UserRole.Provider,
            RevieweeUserId = request.ClientId,
            RevieweeRole = UserRole.Client,
            Rating = dto.Rating,
            Comment = dto.Comment
        };

        await _reviewRepository.AddAsync(review);
        return true;
    }

    public async Task<IEnumerable<ReviewDto>> GetByProviderAsync(Guid providerId)
    {
        var reviews = await _reviewRepository.GetByRevieweeAsync(providerId, UserRole.Provider);
        return reviews.Select(r => new ReviewDto(
            r.Id,
            r.RequestId,
            r.ClientId,
            r.ProviderId,
            r.ReviewerUserId,
            r.ReviewerRole,
            r.RevieweeUserId,
            r.RevieweeRole,
            r.Rating,
            r.Comment,
            r.CreatedAt));
    }

    private async Task UpdateProviderRating(Guid providerId, int newRating)
    {
        var provider = await _userRepository.GetByIdAsync(providerId);
        if (provider != null && provider.ProviderProfile != null)
        {
            var profile = provider.ProviderProfile;
            
            // Incremental average calculation
            double totalPoints = (profile.Rating * profile.ReviewCount) + newRating;
            profile.ReviewCount++;
            profile.Rating = totalPoints / profile.ReviewCount;

            await _userRepository.UpdateAsync(provider);
        }
    }

    private static bool CanReviewStatus(ServiceRequestStatus status)
    {
        return status == ServiceRequestStatus.Completed ||
               status == ServiceRequestStatus.Validated;
    }
}
