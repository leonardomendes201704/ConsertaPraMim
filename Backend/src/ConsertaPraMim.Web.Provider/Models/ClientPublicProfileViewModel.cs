using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Web.Provider.Models;

public sealed record ClientPublicProfileViewModel(
    Guid ClientId,
    UserProfileDto Profile,
    ReviewScoreSummaryDto Reputation,
    IReadOnlyList<ReviewDto> RecentReviews);
