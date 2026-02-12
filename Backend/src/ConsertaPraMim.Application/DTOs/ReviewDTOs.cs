namespace ConsertaPraMim.Application.DTOs;

public record CreateReviewDto(Guid RequestId, int Rating, string Comment);

public record ReviewDto(
    Guid Id,
    Guid RequestId,
    Guid ClientId,
    Guid ProviderId,
    int Rating,
    string Comment,
    DateTime CreatedAt);
