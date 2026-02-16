using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Web.Client.Models;

public sealed record ProviderPublicProfileViewModel(
    Guid ProviderId,
    UserProfileDto Profile,
    ReviewScoreSummaryDto Reputation,
    IReadOnlyList<ReviewDto> RecentReviews);
