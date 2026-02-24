namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminOperationalEventNotifier
{
    Task NotifyClientOpenedRequestAsync(
        Guid requestId,
        string? requestDescription,
        string? categoryName,
        CancellationToken cancellationToken = default);

    Task NotifyProviderOpenedSupportTicketAsync(
        Guid ticketId,
        Guid providerUserId,
        string? ticketSubject,
        string? categoryName,
        CancellationToken cancellationToken = default);

    Task NotifyProviderSentProposalAsync(
        Guid proposalId,
        Guid requestId,
        decimal? estimatedValue,
        CancellationToken cancellationToken = default);

    Task NotifyClientAcceptedProposalAsync(
        Guid proposalId,
        Guid requestId,
        CancellationToken cancellationToken = default);

    Task NotifyClientScheduledAsync(
        Guid appointmentId,
        Guid requestId,
        DateTime windowStartUtc,
        DateTime windowEndUtc,
        CancellationToken cancellationToken = default);

    Task NotifyUserRegisteredAsync(
        Guid userId,
        string userName,
        string role,
        CancellationToken cancellationToken = default);

    Task NotifyUserLoggedInAsync(
        Guid userId,
        string userName,
        string role,
        CancellationToken cancellationToken = default);

    Task NotifyNoShowPolicyAppliedAsync(
        Guid appointmentId,
        Guid requestId,
        string financialEventType,
        string outcome,
        decimal serviceValue,
        decimal counterpartyCompensationAmount,
        decimal penaltyAmount,
        CancellationToken cancellationToken = default);
}
