using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;

namespace ConsertaPraMim.Application.Services;

public class AdminRequestProposalService : IAdminRequestProposalService
{
    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly IProposalRepository _proposalRepository;
    private readonly IAdminAuditLogRepository _adminAuditLogRepository;

    public AdminRequestProposalService(
        IServiceRequestRepository serviceRequestRepository,
        IProposalRepository proposalRepository,
        IAdminAuditLogRepository adminAuditLogRepository)
    {
        _serviceRequestRepository = serviceRequestRepository;
        _proposalRepository = proposalRepository;
        _adminAuditLogRepository = adminAuditLogRepository;
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
                    r.Category.ToString(),
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

        return new AdminServiceRequestDetailsDto(
            request.Id,
            request.Description,
            request.Status.ToString(),
            request.Category.ToString(),
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
            proposals);
    }

    public async Task<AdminOperationResultDto> UpdateServiceRequestStatusAsync(
        Guid requestId,
        AdminUpdateServiceRequestStatusRequestDto request,
        Guid actorUserId,
        string actorEmail)
    {
        if (!TryParseRequestStatus(request.Status, out var parsedStatus))
        {
            return new AdminOperationResultDto(false, "invalid_status", "Status de pedido invalido.");
        }

        var entity = await _serviceRequestRepository.GetByIdAsync(requestId);
        if (entity == null)
        {
            return new AdminOperationResultDto(false, "not_found", "Pedido nao encontrado.");
        }

        var previousStatus = entity.Status;
        entity.Status = parsedStatus;
        entity.UpdatedAt = DateTime.UtcNow;
        await _serviceRequestRepository.UpdateAsync(entity);

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "-" : request.Reason.Trim();
        await _adminAuditLogRepository.AddAsync(new AdminAuditLog
        {
            ActorUserId = actorUserId,
            ActorEmail = actorEmail,
            Action = "ServiceRequestStatusChanged",
            TargetType = "ServiceRequest",
            TargetId = entity.Id,
            Metadata = $"from={previousStatus};to={parsedStatus};reason={reason}"
        });

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
            return new AdminOperationResultDto(false, "not_found", "Proposta nao encontrada.");
        }

        if (proposal.IsInvalidated)
        {
            return new AdminOperationResultDto(true);
        }

        var wasAccepted = proposal.Accepted;
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

        await _adminAuditLogRepository.AddAsync(new AdminAuditLog
        {
            ActorUserId = actorUserId,
            ActorEmail = actorEmail,
            Action = "ProposalInvalidated",
            TargetType = "Proposal",
            TargetId = proposal.Id,
            Metadata = $"reason={reason};wasAccepted={wasAccepted}"
        });

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
        parsed = default;
        if (string.IsNullOrWhiteSpace(rawCategory)) return false;
        return Enum.TryParse(rawCategory, true, out parsed);
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
}
