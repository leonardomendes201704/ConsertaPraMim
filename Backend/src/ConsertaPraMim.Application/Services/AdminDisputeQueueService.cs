using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;

namespace ConsertaPraMim.Application.Services;

public class AdminDisputeQueueService : IAdminDisputeQueueService
{
    private readonly IServiceDisputeCaseRepository _serviceDisputeCaseRepository;

    public AdminDisputeQueueService(IServiceDisputeCaseRepository serviceDisputeCaseRepository)
    {
        _serviceDisputeCaseRepository = serviceDisputeCaseRepository;
    }

    public async Task<AdminDisputesQueueResponseDto> GetQueueAsync(Guid? highlightedDisputeCaseId, int take = 100)
    {
        var normalizedTake = Math.Clamp(take, 1, 200);
        var cases = (await _serviceDisputeCaseRepository.GetOpenCasesAsync(normalizedTake))
            .Where(c => IsOpenStatus(c.Status))
            .ToList();

        var items = cases
            .Select(MapToQueueItem)
            .ToList();

        return new AdminDisputesQueueResponseDto(
            highlightedDisputeCaseId,
            items.Count,
            items);
    }

    private static bool IsOpenStatus(DisputeCaseStatus status)
    {
        return status is DisputeCaseStatus.Open or DisputeCaseStatus.UnderReview or DisputeCaseStatus.WaitingParties;
    }

    private static AdminDisputeQueueItemDto MapToQueueItem(ServiceDisputeCase disputeCase)
    {
        var nowUtc = DateTime.UtcNow;
        var openedByName = !string.IsNullOrWhiteSpace(disputeCase.OpenedByUser?.Name)
            ? disputeCase.OpenedByUser.Name
            : $"Usuario {disputeCase.OpenedByUserId.ToString()[..8]}";

        var counterpartyName = !string.IsNullOrWhiteSpace(disputeCase.CounterpartyUser?.Name)
            ? disputeCase.CounterpartyUser.Name
            : $"Usuario {disputeCase.CounterpartyUserId.ToString()[..8]}";

        return new AdminDisputeQueueItemDto(
            disputeCase.Id,
            disputeCase.ServiceRequestId,
            disputeCase.ServiceAppointmentId,
            disputeCase.Type.ToString(),
            disputeCase.Priority.ToString(),
            disputeCase.Status.ToString(),
            disputeCase.ReasonCode,
            disputeCase.Description,
            openedByName,
            disputeCase.OpenedByRole.ToString(),
            counterpartyName,
            disputeCase.CounterpartyRole.ToString(),
            disputeCase.OpenedAtUtc,
            disputeCase.SlaDueAtUtc,
            disputeCase.LastInteractionAtUtc,
            disputeCase.SlaDueAtUtc < nowUtc,
            string.IsNullOrWhiteSpace(disputeCase.ServiceRequest?.AddressCity) ? null : disputeCase.ServiceRequest.AddressCity,
            disputeCase.ServiceRequest?.Category.ToString(),
            disputeCase.Attachments?.Count ?? 0,
            disputeCase.Messages?.Count ?? 0,
            $"/AdminServiceRequests/Details/{disputeCase.ServiceRequestId:D}?disputeCaseId={disputeCase.Id:D}");
    }
}
