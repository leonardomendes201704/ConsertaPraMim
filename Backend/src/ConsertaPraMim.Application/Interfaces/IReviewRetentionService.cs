using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IReviewRetentionService
{
    Task<ReviewRepurchaseTriggerResultDto> RunRepurchaseTriggerAsync(
        ReviewRepurchaseTriggerRequestDto request,
        Guid actorUserId,
        string? actorEmail,
        CancellationToken cancellationToken = default);
}
