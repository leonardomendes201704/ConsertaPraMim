using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace ConsertaPraMim.Application.Services;

public class ReviewService : IReviewService
{
    private readonly IReviewRepository _reviewRepository;
    private readonly IServiceRequestRepository _requestRepository;
    private readonly IUserRepository _userRepository;
    private readonly int _evaluationWindowDays;

    public ReviewService(
        IReviewRepository reviewRepository, 
        IServiceRequestRepository requestRepository,
        IUserRepository userRepository,
        IConfiguration configuration)
    {
        _reviewRepository = reviewRepository;
        _requestRepository = requestRepository;
        _userRepository = userRepository;
        _evaluationWindowDays = ParseInt(configuration["Reviews:EvaluationWindowDays"], 30, 1, 365);
    }

    public async Task<bool> SubmitReviewAsync(Guid clientId, CreateReviewDto dto)
    {
        return await SubmitClientReviewAsync(clientId, dto);
    }

    public async Task<bool> SubmitClientReviewAsync(Guid clientId, CreateReviewDto dto)
    {
        var request = await _requestRepository.GetByIdAsync(dto.RequestId);
        if (request == null || !IsEligibleForReview(request)) return false;

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
        if (request == null || !IsEligibleForReview(request)) return false;

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

    private bool IsEligibleForReview(ServiceRequest request)
    {
        return CanReviewStatus(request.Status)
            && HasSuccessfulPayment(request)
            && IsWithinReviewWindow(request);
    }

    private static bool HasSuccessfulPayment(ServiceRequest request)
    {
        return request.PaymentTransactions.Any(t => t.Status == PaymentTransactionStatus.Paid);
    }

    private bool IsWithinReviewWindow(ServiceRequest request)
    {
        var completionReferenceUtc = GetCompletionReferenceUtc(request);
        return DateTime.UtcNow <= completionReferenceUtc.AddDays(_evaluationWindowDays);
    }

    private static DateTime GetCompletionReferenceUtc(ServiceRequest request)
    {
        var completedAtUtc = request.Appointments
            .Where(a => a.CompletedAtUtc.HasValue)
            .Select(a => a.CompletedAtUtc!.Value)
            .OrderByDescending(a => a)
            .FirstOrDefault();

        if (completedAtUtc != default)
        {
            return completedAtUtc.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(completedAtUtc, DateTimeKind.Utc)
                : completedAtUtc.ToUniversalTime();
        }

        var fallback = request.UpdatedAt ?? request.CreatedAt;
        return fallback.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(fallback, DateTimeKind.Utc)
            : fallback.ToUniversalTime();
    }

    private static int ParseInt(string? value, int fallback, int min, int max)
    {
        if (!int.TryParse(value, out var parsed))
        {
            return fallback;
        }

        if (parsed < min)
        {
            return min;
        }

        if (parsed > max)
        {
            return max;
        }

        return parsed;
    }
}
