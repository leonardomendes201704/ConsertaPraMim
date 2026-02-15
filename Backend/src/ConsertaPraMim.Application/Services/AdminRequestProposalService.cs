using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace ConsertaPraMim.Application.Services;

public class AdminRequestProposalService : IAdminRequestProposalService
{
    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly IProposalRepository _proposalRepository;
    private readonly IServiceScopeChangeRequestRepository _scopeChangeRequestRepository;
    private readonly IAdminAuditLogRepository _adminAuditLogRepository;
    private readonly IProviderGalleryService _providerGalleryService;
    private readonly ILogger<AdminRequestProposalService> _logger;

    public AdminRequestProposalService(
        IServiceRequestRepository serviceRequestRepository,
        IProposalRepository proposalRepository,
        IAdminAuditLogRepository adminAuditLogRepository,
        IProviderGalleryService providerGalleryService,
        IServiceScopeChangeRequestRepository? scopeChangeRequestRepository = null,
        ILogger<AdminRequestProposalService>? logger = null)
    {
        _serviceRequestRepository = serviceRequestRepository;
        _proposalRepository = proposalRepository;
        _adminAuditLogRepository = adminAuditLogRepository;
        _providerGalleryService = providerGalleryService;
        _scopeChangeRequestRepository = scopeChangeRequestRepository ?? NullServiceScopeChangeRequestRepository.Instance;
        _logger = logger ?? NullLogger<AdminRequestProposalService>.Instance;
    }

    public async Task<AdminServiceRequestsListResponseDto> GetServiceRequestsAsync(AdminServiceRequestsQueryDto query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 100);
        var (fromUtc, toUtc) = NormalizeRange(query.FromUtc, query.ToUtc);

        var requests = (await _serviceRequestRepository.GetAllAsync()).AsQueryable();
        var proposals = (await _proposalRepository.GetAllAsync()).ToList();

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var search = query.SearchTerm.Trim();
            requests = requests.Where(r =>
                r.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                r.Client.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                r.Client.Email.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                r.AddressZip.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (TryParseRequestStatus(query.Status, out var parsedStatus))
        {
            requests = requests.Where(r => r.Status == parsedStatus);
        }

        if (TryParseCategory(query.Category, out var parsedCategory))
        {
            requests = requests.Where(r => r.Category == parsedCategory);
        }

        requests = requests.Where(r => r.CreatedAt >= fromUtc && r.CreatedAt <= toUtc);

        var ordered = requests.OrderByDescending(r => r.CreatedAt).ToList();
        var totalCount = ordered.Count;

        var items = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r =>
            {
                var requestProposals = proposals.Where(p => p.RequestId == r.Id).ToList();
                return new AdminServiceRequestListItemDto(
                    r.Id,
                    r.Description,
                    r.Status.ToString(),
                    ResolveCategoryName(r),
                    r.Client.Name,
                    r.Client.Email,
                    r.AddressZip,
                    r.CreatedAt,
                    requestProposals.Count,
                    requestProposals.Count(p => p.Accepted && !p.IsInvalidated),
                    requestProposals.Count(p => p.IsInvalidated));
            })
            .ToList();

        return new AdminServiceRequestsListResponseDto(page, pageSize, totalCount, items);
    }

    public async Task<AdminServiceRequestDetailsDto?> GetServiceRequestByIdAsync(Guid requestId)
    {
        var request = await _serviceRequestRepository.GetByIdAsync(requestId);
        if (request == null) return null;

        var proposals = (await _proposalRepository.GetByRequestIdAsync(requestId))
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new AdminServiceRequestDetailProposalDto(
                p.Id,
                p.ProviderId,
                p.Provider?.Name ?? string.Empty,
                p.Provider?.Email ?? string.Empty,
                p.EstimatedValue,
                p.Accepted,
                p.IsInvalidated,
                p.InvalidationReason,
                p.CreatedAt))
            .ToList();

        var appointments = request.Appointments
            .OrderByDescending(a => a.UpdatedAt ?? a.CreatedAt)
            .Select(a => new AdminServiceRequestAppointmentDto(
                a.Id,
                a.ProviderId,
                a.Provider?.Name ?? string.Empty,
                a.Status.ToString(),
                a.OperationalStatus?.ToString(),
                a.OperationalStatusReason,
                a.OperationalStatusUpdatedAtUtc,
                a.WindowStartUtc,
                a.WindowEndUtc,
                a.ArrivedAtUtc,
                a.StartedAtUtc,
                a.CompletedAtUtc,
                a.History
                    .OrderBy(h => h.OccurredAtUtc)
                    .Select(h => new AdminServiceRequestAppointmentHistoryDto(
                        h.OccurredAtUtc,
                        h.ActorRole.ToString(),
                        h.NewStatus.ToString(),
                        h.NewOperationalStatus?.ToString(),
                        h.Reason))
                    .ToList()))
            .ToList();

        var evidences = (await _providerGalleryService.GetEvidenceTimelineByServiceRequestAsync(
                requestId,
                null,
                UserRole.Admin.ToString()))
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new AdminServiceRequestEvidenceDto(
                e.Id,
                e.ProviderId,
                e.ProviderName,
                e.ServiceAppointmentId,
                e.EvidencePhase,
                e.FileUrl,
                e.ThumbnailUrl,
                e.PreviewUrl,
                e.FileName,
                e.ContentType,
                e.MediaKind,
                e.Category,
                e.Caption,
                e.CreatedAt))
            .ToList();

        var providerNamesById = proposals
            .GroupBy(p => p.ProviderId)
            .ToDictionary(group => group.Key, group => group.First().ProviderName);

        var scopeChanges = await BuildScopeChangeHistoryAsync(
            request,
            proposals,
            providerNamesById);

        return new AdminServiceRequestDetailsDto(
            request.Id,
            request.Description,
            request.Status.ToString(),
            ResolveCategoryName(request),
            request.AddressStreet,
            request.AddressCity,
            request.AddressZip,
            request.Latitude,
            request.Longitude,
            request.Client.Name,
            request.Client.Email,
            request.Client.Phone,
            request.CreatedAt,
            request.UpdatedAt,
            proposals,
            appointments,
            evidences,
            scopeChanges,
            request.CommercialVersion,
            request.CommercialState.ToString(),
            request.CommercialBaseValue,
            request.CommercialCurrentValue,
            request.CommercialUpdatedAtUtc);
    }

    public async Task<AdminOperationResultDto> UpdateServiceRequestStatusAsync(
        Guid requestId,
        AdminUpdateServiceRequestStatusRequestDto request,
        Guid actorUserId,
        string actorEmail)
    {
        if (!TryParseRequestStatus(request.Status, out var parsedStatus))
        {
            _logger.LogWarning(
                "Admin request status update failed: invalid status. ActorUserId={ActorUserId}, RequestId={RequestId}, RequestedStatus={RequestedStatus}",
                actorUserId,
                requestId,
                request.Status);
            return new AdminOperationResultDto(false, "invalid_status", "Status de pedido invalido.");
        }

        var entity = await _serviceRequestRepository.GetByIdAsync(requestId);
        if (entity == null)
        {
            _logger.LogWarning(
                "Admin request status update failed: request not found. ActorUserId={ActorUserId}, RequestId={RequestId}",
                actorUserId,
                requestId);
            return new AdminOperationResultDto(false, "not_found", "Pedido nao encontrado.");
        }

        var previousStatus = entity.Status;
        entity.Status = parsedStatus;
        entity.UpdatedAt = DateTime.UtcNow;
        await _serviceRequestRepository.UpdateAsync(entity);

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "-" : request.Reason.Trim();
        var metadata = JsonSerializer.Serialize(new
        {
            before = new
            {
                status = previousStatus.ToString()
            },
            after = new
            {
                status = parsedStatus.ToString()
            },
            reason
        });

        await _adminAuditLogRepository.AddAsync(new AdminAuditLog
        {
            ActorUserId = actorUserId,
            ActorEmail = actorEmail,
            Action = "ServiceRequestStatusChanged",
            TargetType = "ServiceRequest",
            TargetId = entity.Id,
            Metadata = metadata
        });

        _logger.LogInformation(
            "Admin request status updated. ActorUserId={ActorUserId}, RequestId={RequestId}, PreviousStatus={PreviousStatus}, NewStatus={NewStatus}",
            actorUserId,
            entity.Id,
            previousStatus,
            parsedStatus);

        return new AdminOperationResultDto(true);
    }

    public async Task<AdminProposalsListResponseDto> GetProposalsAsync(AdminProposalsQueryDto query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 100);
        var (fromUtc, toUtc) = NormalizeRange(query.FromUtc, query.ToUtc);

        var proposals = (await _proposalRepository.GetAllAsync()).AsQueryable();
        proposals = proposals.Where(p => p.CreatedAt >= fromUtc && p.CreatedAt <= toUtc);

        if (query.RequestId.HasValue)
        {
            proposals = proposals.Where(p => p.RequestId == query.RequestId.Value);
        }

        if (query.ProviderId.HasValue)
        {
            proposals = proposals.Where(p => p.ProviderId == query.ProviderId.Value);
        }

        var statusFilter = NormalizeProposalStatusFilter(query.Status);
        proposals = statusFilter switch
        {
            "accepted" => proposals.Where(p => p.Accepted && !p.IsInvalidated),
            "pending" => proposals.Where(p => !p.Accepted && !p.IsInvalidated),
            "invalidated" => proposals.Where(p => p.IsInvalidated),
            _ => proposals
        };

        var ordered = proposals.OrderByDescending(p => p.CreatedAt).ToList();
        var totalCount = ordered.Count;
        var items = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new AdminProposalListItemDto(
                p.Id,
                p.RequestId,
                p.ProviderId,
                p.Provider?.Name ?? string.Empty,
                p.Provider?.Email ?? string.Empty,
                p.EstimatedValue,
                p.Accepted,
                p.IsInvalidated,
                p.InvalidationReason,
                p.CreatedAt))
            .ToList();

        return new AdminProposalsListResponseDto(page, pageSize, totalCount, items);
    }

    public async Task<AdminOperationResultDto> InvalidateProposalAsync(
        Guid proposalId,
        AdminInvalidateProposalRequestDto request,
        Guid actorUserId,
        string actorEmail)
    {
        var proposal = await _proposalRepository.GetByIdAsync(proposalId);
        if (proposal == null)
        {
            _logger.LogWarning(
                "Admin proposal invalidation failed: proposal not found. ActorUserId={ActorUserId}, ProposalId={ProposalId}",
                actorUserId,
                proposalId);
            return new AdminOperationResultDto(false, "not_found", "Proposta nao encontrada.");
        }

        if (proposal.IsInvalidated)
        {
            _logger.LogInformation(
                "Admin proposal invalidation skipped: already invalidated. ActorUserId={ActorUserId}, ProposalId={ProposalId}",
                actorUserId,
                proposalId);
            return new AdminOperationResultDto(true);
        }

        var wasAccepted = proposal.Accepted;
        var previousRequestStatus = proposal.Request.Status;
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "Invalidada pelo administrador." : request.Reason.Trim();

        proposal.IsInvalidated = true;
        proposal.InvalidationReason = reason;
        proposal.InvalidatedAt = DateTime.UtcNow;
        proposal.InvalidatedByAdminId = actorUserId;
        proposal.Accepted = false;
        proposal.UpdatedAt = DateTime.UtcNow;
        await _proposalRepository.UpdateAsync(proposal);

        if (wasAccepted && proposal.Request.Status == ServiceRequestStatus.Scheduled)
        {
            var activeProposals = (await _proposalRepository.GetByRequestIdAsync(proposal.RequestId))
                .Where(p => !p.IsInvalidated);

            proposal.Request.Status = activeProposals.Any()
                ? ServiceRequestStatus.Matching
                : ServiceRequestStatus.Created;
            proposal.Request.UpdatedAt = DateTime.UtcNow;
            await _serviceRequestRepository.UpdateAsync(proposal.Request);
        }

        var metadata = JsonSerializer.Serialize(new
        {
            before = new
            {
                isInvalidated = false,
                accepted = wasAccepted,
                invalidationReason = (string?)null,
                requestStatus = previousRequestStatus.ToString()
            },
            after = new
            {
                isInvalidated = proposal.IsInvalidated,
                accepted = proposal.Accepted,
                invalidationReason = proposal.InvalidationReason,
                requestStatus = proposal.Request.Status.ToString()
            },
            reason
        });

        await _adminAuditLogRepository.AddAsync(new AdminAuditLog
        {
            ActorUserId = actorUserId,
            ActorEmail = actorEmail,
            Action = "ProposalInvalidated",
            TargetType = "Proposal",
            TargetId = proposal.Id,
            Metadata = metadata
        });

        _logger.LogInformation(
            "Admin proposal invalidated. ActorUserId={ActorUserId}, ProposalId={ProposalId}, WasAccepted={WasAccepted}, RequestStatusBefore={RequestStatusBefore}, RequestStatusAfter={RequestStatusAfter}",
            actorUserId,
            proposal.Id,
            wasAccepted,
            previousRequestStatus,
            proposal.Request.Status);

        return new AdminOperationResultDto(true);
    }

    private static (DateTime FromUtc, DateTime ToUtc) NormalizeRange(DateTime? fromUtc, DateTime? toUtc)
    {
        var end = toUtc ?? DateTime.UtcNow;
        var start = fromUtc ?? end.AddDays(-30);
        if (start > end)
        {
            (start, end) = (end, start);
        }

        return (start, end);
    }

    private static bool TryParseRequestStatus(string? rawStatus, out ServiceRequestStatus parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(rawStatus)) return false;
        return Enum.TryParse(rawStatus, true, out parsed);
    }

    private static bool TryParseCategory(string? rawCategory, out ServiceCategory parsed)
    {
        return ServiceCategoryExtensions.TryParseFlexible(rawCategory, out parsed);
    }

    private static string NormalizeProposalStatusFilter(string? rawStatus)
    {
        if (string.IsNullOrWhiteSpace(rawStatus)) return "all";

        var normalized = rawStatus.Trim().ToLowerInvariant();
        return normalized switch
        {
            "accepted" => "accepted",
            "pending" => "pending",
            "invalidated" => "invalidated",
            _ => "all"
        };
    }

    private static string ResolveCategoryName(ServiceRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.CategoryDefinition?.Name))
        {
            return request.CategoryDefinition.Name;
        }

        return request.Category.ToPtBr();
    }

    private async Task<IReadOnlyList<AdminServiceRequestScopeChangeDto>> BuildScopeChangeHistoryAsync(
        ServiceRequest request,
        IReadOnlyList<AdminServiceRequestDetailProposalDto> proposals,
        IReadOnlyDictionary<Guid, string> providerNamesById)
    {
        var persistedScopeChanges = await _scopeChangeRequestRepository.GetByServiceRequestIdAsync(request.Id);
        if (persistedScopeChanges.Count == 0)
        {
            return Array.Empty<AdminServiceRequestScopeChangeDto>();
        }

        var orderedScopeChanges = persistedScopeChanges
            .OrderBy(scopeChange => scopeChange.Version)
            .ThenBy(scopeChange => scopeChange.RequestedAtUtc)
            .ThenBy(scopeChange => scopeChange.CreatedAt)
            .ToList();

        var baseValue = ResolveCommercialBaseValue(request, proposals);
        var approvedIncrementalValue = 0m;
        var timeline = new List<AdminServiceRequestScopeChangeDto>(orderedScopeChanges.Count);

        foreach (var scopeChange in orderedScopeChanges)
        {
            var previousValue = decimal.Round(
                baseValue + approvedIncrementalValue,
                2,
                MidpointRounding.AwayFromZero);

            var incrementalValue = decimal.Round(
                Math.Max(0m, scopeChange.IncrementalValue),
                2,
                MidpointRounding.AwayFromZero);

            var newValue = decimal.Round(
                previousValue + incrementalValue,
                2,
                MidpointRounding.AwayFromZero);

            var providerName = providerNamesById.TryGetValue(scopeChange.ProviderId, out var mappedProviderName) &&
                               !string.IsNullOrWhiteSpace(mappedProviderName)
                ? mappedProviderName
                : "Prestador";

            var attachments = (scopeChange.Attachments ?? Array.Empty<ServiceScopeChangeRequestAttachment>())
                .OrderBy(attachment => attachment.CreatedAt)
                .Select(attachment => new AdminServiceRequestScopeChangeAttachmentDto(
                    attachment.Id,
                    attachment.FileUrl,
                    attachment.FileName,
                    attachment.ContentType,
                    attachment.MediaKind,
                    attachment.SizeBytes,
                    attachment.CreatedAt))
                .ToList();

            timeline.Add(new AdminServiceRequestScopeChangeDto(
                scopeChange.Id,
                scopeChange.ServiceAppointmentId,
                scopeChange.ProviderId,
                providerName,
                scopeChange.Version,
                scopeChange.Status.ToString(),
                scopeChange.Reason,
                scopeChange.AdditionalScopeDescription,
                incrementalValue,
                previousValue,
                newValue,
                scopeChange.RequestedAtUtc,
                scopeChange.ClientRespondedAtUtc,
                scopeChange.ClientResponseReason,
                attachments));

            if (scopeChange.Status == ServiceScopeChangeRequestStatus.ApprovedByClient)
            {
                approvedIncrementalValue = decimal.Round(
                    approvedIncrementalValue + incrementalValue,
                    2,
                    MidpointRounding.AwayFromZero);
            }
        }

        return timeline
            .OrderByDescending(scopeChange => scopeChange.RequestedAtUtc)
            .ThenByDescending(scopeChange => scopeChange.Version)
            .ToList();
    }

    private static decimal ResolveCommercialBaseValue(
        ServiceRequest request,
        IReadOnlyList<AdminServiceRequestDetailProposalDto> proposals)
    {
        var acceptedProposalBaseValue = proposals
            .Where(proposal => proposal.Accepted && !proposal.IsInvalidated)
            .Select(proposal => proposal.EstimatedValue)
            .Where(value => value.HasValue && value.Value > 0m)
            .Select(value => value!.Value)
            .DefaultIfEmpty(0m)
            .Max();

        var baseValue = request.CommercialBaseValue ?? acceptedProposalBaseValue;
        return decimal.Round(Math.Max(0m, baseValue), 2, MidpointRounding.AwayFromZero);
    }

    private sealed class NullServiceScopeChangeRequestRepository : IServiceScopeChangeRequestRepository
    {
        public static readonly NullServiceScopeChangeRequestRepository Instance = new();

        public Task<IReadOnlyList<ServiceScopeChangeRequest>> GetByAppointmentIdAsync(Guid appointmentId)
        {
            return Task.FromResult<IReadOnlyList<ServiceScopeChangeRequest>>(Array.Empty<ServiceScopeChangeRequest>());
        }

        public Task<IReadOnlyList<ServiceScopeChangeRequest>> GetByServiceRequestIdAsync(Guid serviceRequestId)
        {
            return Task.FromResult<IReadOnlyList<ServiceScopeChangeRequest>>(Array.Empty<ServiceScopeChangeRequest>());
        }

        public Task<IReadOnlyList<ServiceScopeChangeRequest>> GetExpiredPendingByRequestedAtAsync(DateTime requestedAtUtcThreshold, int take = 200)
        {
            return Task.FromResult<IReadOnlyList<ServiceScopeChangeRequest>>(Array.Empty<ServiceScopeChangeRequest>());
        }

        public Task<ServiceScopeChangeRequest?> GetLatestByAppointmentIdAsync(Guid appointmentId)
        {
            return Task.FromResult<ServiceScopeChangeRequest?>(null);
        }

        public Task<ServiceScopeChangeRequest?> GetByIdAsync(Guid scopeChangeRequestId)
        {
            return Task.FromResult<ServiceScopeChangeRequest?>(null);
        }

        public Task<ServiceScopeChangeRequest?> GetByIdWithAttachmentsAsync(Guid scopeChangeRequestId)
        {
            return Task.FromResult<ServiceScopeChangeRequest?>(null);
        }

        public Task<ServiceScopeChangeRequest?> GetLatestByAppointmentIdAndStatusAsync(
            Guid appointmentId,
            ServiceScopeChangeRequestStatus status)
        {
            return Task.FromResult<ServiceScopeChangeRequest?>(null);
        }

        public Task AddAsync(ServiceScopeChangeRequest scopeChangeRequest)
        {
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ServiceScopeChangeRequest scopeChangeRequest)
        {
            return Task.CompletedTask;
        }

        public Task AddAttachmentAsync(ServiceScopeChangeRequestAttachment attachment)
        {
            return Task.CompletedTask;
        }
    }
}
