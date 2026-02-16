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

    public async Task<AdminDisputeCaseDetailsDto?> GetCaseDetailsAsync(Guid disputeCaseId)
    {
        if (disputeCaseId == Guid.Empty)
        {
            return null;
        }

        var disputeCase = await _serviceDisputeCaseRepository.GetByIdWithDetailsAsync(disputeCaseId);
        if (disputeCase == null)
        {
            return null;
        }

        var nowUtc = DateTime.UtcNow;
        var messages = (disputeCase.Messages ?? Array.Empty<ServiceDisputeCaseMessage>())
            .OrderBy(m => m.CreatedAt)
            .Select(m => new AdminDisputeCaseMessageDto(
                m.Id,
                m.MessageType,
                m.MessageText,
                m.IsInternal,
                m.AuthorUserId,
                m.AuthorRole.ToString(),
                ResolveUserDisplayName(m.AuthorUserId, m.AuthorUser?.Name),
                m.CreatedAt))
            .ToList();

        var attachments = (disputeCase.Attachments ?? Array.Empty<ServiceDisputeCaseAttachment>())
            .OrderBy(a => a.CreatedAt)
            .Select(a => new AdminDisputeCaseAttachmentDto(
                a.Id,
                a.ServiceDisputeCaseMessageId,
                a.FileUrl,
                a.FileName,
                a.ContentType,
                a.MediaKind,
                a.SizeBytes,
                a.UploadedByUserId,
                ResolveUserDisplayName(a.UploadedByUserId, a.UploadedByUser?.Name),
                a.CreatedAt))
            .ToList();

        var audits = (disputeCase.AuditEntries ?? Array.Empty<ServiceDisputeCaseAuditEntry>())
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AdminDisputeCaseAuditEntryDto(
                a.Id,
                a.EventType,
                a.Message,
                a.ActorUserId,
                a.ActorRole.ToString(),
                ResolveUserDisplayName(a.ActorUserId, a.ActorUser?.Name),
                a.MetadataJson,
                a.CreatedAt))
            .ToList();

        return new AdminDisputeCaseDetailsDto(
            disputeCase.Id,
            disputeCase.ServiceRequestId,
            disputeCase.ServiceAppointmentId,
            disputeCase.Type.ToString(),
            disputeCase.Priority.ToString(),
            disputeCase.Status.ToString(),
            disputeCase.ReasonCode,
            disputeCase.Description,
            ResolveUserDisplayName(disputeCase.OpenedByUserId, disputeCase.OpenedByUser?.Name),
            disputeCase.OpenedByRole.ToString(),
            ResolveUserDisplayName(disputeCase.CounterpartyUserId, disputeCase.CounterpartyUser?.Name),
            disputeCase.CounterpartyRole.ToString(),
            disputeCase.OwnedByAdminUserId,
            ResolveUserDisplayName(disputeCase.OwnedByAdminUserId, disputeCase.OwnedByAdminUser?.Name),
            disputeCase.OwnedAtUtc,
            disputeCase.WaitingForRole?.ToString(),
            disputeCase.OpenedAtUtc,
            disputeCase.SlaDueAtUtc,
            disputeCase.LastInteractionAtUtc,
            disputeCase.ClosedAtUtc,
            disputeCase.SlaDueAtUtc < nowUtc,
            disputeCase.ResolutionSummary,
            string.IsNullOrWhiteSpace(disputeCase.ServiceRequest?.AddressCity) ? null : disputeCase.ServiceRequest.AddressCity,
            disputeCase.ServiceRequest?.Category.ToString(),
            messages,
            attachments,
            audits);
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

    private static string ResolveUserDisplayName(Guid? userId, string? userName)
    {
        if (!string.IsNullOrWhiteSpace(userName))
        {
            return userName;
        }

        return userId.HasValue
            ? $"Usuario {userId.Value.ToString("N")[..8]}"
            : "Sistema";
    }
}
