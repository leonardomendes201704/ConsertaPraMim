using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace ConsertaPraMim.Application.Services;

public class ReviewService : IReviewService, IReviewRetentionService
{
    private readonly IReviewRepository _reviewRepository;
    private readonly IServiceRequestRepository _requestRepository;
    private readonly IUserRepository _userRepository;
    private readonly INotificationService? _notificationService;
    private readonly IAdminAuditLogRepository? _adminAuditLogRepository;
    private readonly int _evaluationWindowDays;
    private readonly int _defaultRepurchaseMinDays;
    private readonly int _defaultRepurchaseMaxDays;
    private readonly int _defaultRepurchaseMaxDispatch;
    private readonly int _defaultRepurchaseMinPositiveRating;
    private readonly decimal _defaultRepurchaseMinCompositeScore;
    private readonly bool _defaultRepurchaseRequirePositiveReview;
    private readonly string _repurchaseActionUrl;

    public ReviewService(
        IReviewRepository reviewRepository, 
        IServiceRequestRepository requestRepository,
        IUserRepository userRepository,
        IConfiguration configuration,
        INotificationService? notificationService = null,
        IAdminAuditLogRepository? adminAuditLogRepository = null)
    {
        _reviewRepository = reviewRepository;
        _requestRepository = requestRepository;
        _userRepository = userRepository;
        _notificationService = notificationService;
        _adminAuditLogRepository = adminAuditLogRepository;
        _evaluationWindowDays = ParseInt(configuration["Reviews:EvaluationWindowDays"], 30, 1, 365);
        _defaultRepurchaseMinDays = ParseInt(configuration["Reviews:Repurchase:MinDaysAfterCompletion"], 14, 1, 365);
        _defaultRepurchaseMaxDays = ParseInt(configuration["Reviews:Repurchase:MaxDaysAfterCompletion"], 90, 1, 730);
        _defaultRepurchaseMaxDispatch = ParseInt(configuration["Reviews:Repurchase:MaxDispatch"], 100, 1, 1000);
        _defaultRepurchaseMinPositiveRating = ParseInt(configuration["Reviews:Repurchase:MinPositiveRating"], 4, 1, 5);
        _defaultRepurchaseMinCompositeScore = ParseDecimal(configuration["Reviews:Repurchase:MinCompositeScore"], 70m, 0m, 100m);
        _defaultRepurchaseRequirePositiveReview = ParseBool(configuration["Reviews:Repurchase:RequirePositiveReview"], defaultValue: true);
        _repurchaseActionUrl = NormalizeRepurchaseActionUrl(configuration["Reviews:Repurchase:ActionUrl"]);
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
            Comment = dto.Comment,
            ServiceQualityRating = ResolveQuestionnaireScore(dto.ServiceQualityRating, dto.Rating),
            PunctualityRating = ResolveQuestionnaireScore(dto.PunctualityRating, dto.Rating),
            CommunicationRating = ResolveQuestionnaireScore(dto.CommunicationRating, dto.Rating),
            CostBenefitRating = ResolveQuestionnaireScore(dto.CostBenefitRating, dto.Rating),
            NpsScore = dto.NpsScore,
            WouldHireAgain = dto.WouldHireAgain
        };
        review.CompositeScore = CalculateCompositeScore(review);

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
            Comment = dto.Comment,
            ServiceQualityRating = ResolveQuestionnaireScore(dto.ServiceQualityRating, dto.Rating),
            PunctualityRating = ResolveQuestionnaireScore(dto.PunctualityRating, dto.Rating),
            CommunicationRating = ResolveQuestionnaireScore(dto.CommunicationRating, dto.Rating),
            CostBenefitRating = ResolveQuestionnaireScore(dto.CostBenefitRating, dto.Rating),
            NpsScore = dto.NpsScore,
            WouldHireAgain = dto.WouldHireAgain
        };
        review.CompositeScore = CalculateCompositeScore(review);

        await _reviewRepository.AddAsync(review);
        return true;
    }

    public async Task<IReadOnlyList<ReviewPendingRequestDto>> GetPendingClientReviewsAsync(Guid clientId, int take = 20)
    {
        var normalizedTake = Math.Clamp(take, 1, 100);
        var requests = await _requestRepository.GetAllAsync();
        var eligibleRequests = requests
            .Where(request => request.ClientId == clientId)
            .Where(IsEligibleForReview)
            .OrderByDescending(GetCompletionReferenceUtc)
            .Take(normalizedTake * 3)
            .ToList();

        var result = new List<ReviewPendingRequestDto>(normalizedTake);
        foreach (var request in eligibleRequests)
        {
            if (result.Count >= normalizedTake)
            {
                break;
            }

            var acceptedProposal = request.Proposals.FirstOrDefault(proposal => proposal.Accepted);
            if (acceptedProposal == null)
            {
                continue;
            }

            var existingReview = await _reviewRepository.GetByRequestAndReviewerAsync(request.Id, clientId);
            if (existingReview != null)
            {
                continue;
            }

            var completionReferenceUtc = GetCompletionReferenceUtc(request);
            var reviewDeadlineUtc = completionReferenceUtc.AddDays(_evaluationWindowDays);
            var daysRemaining = Math.Max(0, (int)Math.Ceiling((reviewDeadlineUtc - DateTime.UtcNow).TotalDays));

            result.Add(new ReviewPendingRequestDto(
                RequestId: request.Id,
                CounterpartyName: ResolveCounterpartyName(
                    acceptedProposal.Provider?.Name,
                    fallback: "Prestador"),
                CounterpartyRole: "Provider",
                Category: ResolveCategoryLabel(request),
                CompletedAtUtc: completionReferenceUtc,
                ReviewDeadlineUtc: reviewDeadlineUtc,
                DaysRemaining: daysRemaining));
        }

        return result;
    }

    public async Task<IReadOnlyList<ReviewPendingRequestDto>> GetPendingProviderReviewsAsync(Guid providerId, int take = 20)
    {
        var normalizedTake = Math.Clamp(take, 1, 100);
        var requests = await _requestRepository.GetAllAsync();
        var eligibleRequests = requests
            .Where(request => request.Proposals.Any(proposal => proposal.Accepted && proposal.ProviderId == providerId))
            .Where(IsEligibleForReview)
            .OrderByDescending(GetCompletionReferenceUtc)
            .Take(normalizedTake * 3)
            .ToList();

        var result = new List<ReviewPendingRequestDto>(normalizedTake);
        foreach (var request in eligibleRequests)
        {
            if (result.Count >= normalizedTake)
            {
                break;
            }

            var existingReview = await _reviewRepository.GetByRequestAndReviewerAsync(request.Id, providerId);
            if (existingReview != null)
            {
                continue;
            }

            var completionReferenceUtc = GetCompletionReferenceUtc(request);
            var reviewDeadlineUtc = completionReferenceUtc.AddDays(_evaluationWindowDays);
            var daysRemaining = Math.Max(0, (int)Math.Ceiling((reviewDeadlineUtc - DateTime.UtcNow).TotalDays));

            result.Add(new ReviewPendingRequestDto(
                RequestId: request.Id,
                CounterpartyName: ResolveCounterpartyName(
                    request.Client?.Name,
                    fallback: "Cliente"),
                CounterpartyRole: "Client",
                Category: ResolveCategoryLabel(request),
                CompletedAtUtc: completionReferenceUtc,
                ReviewDeadlineUtc: reviewDeadlineUtc,
                DaysRemaining: daysRemaining));
        }

        return result;
    }

    public async Task<ReviewRepurchaseTriggerResultDto> RunRepurchaseTriggerAsync(
        ReviewRepurchaseTriggerRequestDto request,
        Guid actorUserId,
        string? actorEmail,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedRequest = NormalizeRepurchaseTriggerRequest(request);
        var nowUtc = DateTime.UtcNow;
        var requests = (await _requestRepository.GetAllAsync()).ToList();
        var candidateWindowStartUtc = nowUtc.AddDays(-normalizedRequest.MaxDaysAfterCompletion);
        var candidateWindowEndUtc = nowUtc.AddDays(-normalizedRequest.MinDaysAfterCompletion);

        var alreadyTriggeredRequestIds = await GetAlreadyTriggeredRepurchaseRequestIdsAsync(
            candidateWindowStartUtc.AddDays(-30),
            nowUtc);
        var requestsByClient = requests
            .GroupBy(r => r.ClientId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var evaluated = 0;
        var skippedAlreadyRepurchased = 0;
        var skippedWithoutPositiveReview = 0;
        var skippedAlreadyTriggered = 0;
        var eligibleCandidates = new List<ReviewRepurchaseTriggerCandidateDto>();

        foreach (var serviceRequest in requests
                     .Where(IsEligibleForReview)
                     .OrderByDescending(GetCompletionReferenceUtc))
        {
            evaluated++;
            var completionReferenceUtc = GetCompletionReferenceUtc(serviceRequest);
            if (completionReferenceUtc < candidateWindowStartUtc || completionReferenceUtc > candidateWindowEndUtc)
            {
                continue;
            }

            if (requestsByClient.TryGetValue(serviceRequest.ClientId, out var clientRequests) &&
                HasRepurchaseAfterCompletion(clientRequests, serviceRequest.Id, completionReferenceUtc))
            {
                skippedAlreadyRepurchased++;
                continue;
            }

            if (alreadyTriggeredRequestIds.Contains(serviceRequest.Id))
            {
                skippedAlreadyTriggered++;
                continue;
            }

            var clientReview = serviceRequest.Reviews
                .OrderByDescending(review => review.CreatedAt)
                .FirstOrDefault(review =>
                    review.ReviewerRole == UserRole.Client &&
                    review.ReviewerUserId == serviceRequest.ClientId);

            if (normalizedRequest.RequirePositiveReview &&
                !IsPositiveReview(clientReview, normalizedRequest.MinPositiveRating, normalizedRequest.MinCompositeScore))
            {
                skippedWithoutPositiveReview++;
                continue;
            }

            var daysSinceCompletion = Math.Max(0, (int)Math.Floor((nowUtc - completionReferenceUtc).TotalDays));
            var clientName = ResolveClientName(serviceRequest.Client?.Name);

            eligibleCandidates.Add(new ReviewRepurchaseTriggerCandidateDto(
                RequestId: serviceRequest.Id,
                ClientId: serviceRequest.ClientId,
                ClientName: clientName,
                Category: ResolveCategoryLabel(serviceRequest),
                CompletedAtUtc: completionReferenceUtc,
                DaysSinceCompletion: daysSinceCompletion,
                ClientRating: clientReview?.Rating,
                ClientCompositeScore: clientReview?.CompositeScore,
                WouldHireAgain: clientReview?.WouldHireAgain));
        }

        var selectedCandidates = eligibleCandidates
            .Take(normalizedRequest.MaxDispatch)
            .ToList();

        var triggeredCount = 0;
        if (!normalizedRequest.DryRun && _notificationService != null)
        {
            foreach (var candidate in selectedCandidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await _notificationService.SendNotificationAsync(
                        candidate.ClientId.ToString("N"),
                        "Pronto para o proximo atendimento?",
                        BuildRepurchaseMessage(candidate),
                        _repurchaseActionUrl,
                        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["type"] = "client_repurchase_nudge",
                            ["requestId"] = candidate.RequestId.ToString("N"),
                            ["clientId"] = candidate.ClientId.ToString("N"),
                            ["category"] = candidate.Category,
                            ["daysSinceCompletion"] = candidate.DaysSinceCompletion.ToString()
                        });
                    triggeredCount++;

                    await RegisterRepurchaseAuditAsync(
                        actorUserId,
                        actorEmail,
                        action: "repurchase_nudge_sent",
                        candidate,
                        errorMessage: null);
                }
                catch (Exception ex)
                {
                    await RegisterRepurchaseAuditAsync(
                        actorUserId,
                        actorEmail,
                        action: "repurchase_nudge_failed",
                        candidate,
                        errorMessage: ex.Message);
                }
            }
        }

        return new ReviewRepurchaseTriggerResultDto(
            ExecutedAtUtc: nowUtc,
            EvaluatedRequests: evaluated,
            EligibleCandidates: eligibleCandidates.Count,
            TriggeredCount: triggeredCount,
            SkippedAlreadyRepurchasedCount: skippedAlreadyRepurchased,
            SkippedWithoutPositiveReviewCount: skippedWithoutPositiveReview,
            SkippedAlreadyTriggeredCount: skippedAlreadyTriggered,
            DryRun: normalizedRequest.DryRun,
            Candidates: selectedCandidates);
    }

    public async Task<IEnumerable<ReviewDto>> GetByProviderAsync(Guid providerId)
    {
        var reviews = await _reviewRepository.GetByRevieweeAsync(providerId, UserRole.Provider);
        return reviews.Select(MapToDto);
    }

    public async Task<IEnumerable<ReviewDto>> GetByClientAsync(Guid clientId)
    {
        var reviews = await _reviewRepository.GetByRevieweeAsync(clientId, UserRole.Client);
        return reviews.Select(MapToDto);
    }

    public async Task<bool> ReportReviewAsync(Guid reviewId, Guid actorUserId, UserRole actorRole, ReportReviewDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
        {
            return false;
        }

        var review = await _reviewRepository.GetByIdAsync(reviewId);
        if (review == null)
        {
            return false;
        }

        if (review.ReviewerUserId == actorUserId)
        {
            return false;
        }

        if (review.ModerationStatus == ReviewModerationStatus.Reported)
        {
            return false;
        }

        var canReport = actorRole == UserRole.Admin ||
                        actorUserId == review.ClientId ||
                        actorUserId == review.ProviderId;
        if (!canReport)
        {
            return false;
        }

        review.ModerationStatus = ReviewModerationStatus.Reported;
        review.ReportReason = dto.Reason.Trim();
        review.ReportedByUserId = actorUserId;
        review.ReportedAtUtc = DateTime.UtcNow;
        review.UpdatedAt = DateTime.UtcNow;

        await _reviewRepository.UpdateAsync(review);
        return true;
    }

    public async Task<IEnumerable<ReviewDto>> GetReportedReviewsAsync()
    {
        var reviews = await _reviewRepository.GetReportedAsync();
        return reviews.Select(MapToDto);
    }

    public async Task<bool> ModerateReviewAsync(Guid reviewId, Guid adminUserId, ModerateReviewDto dto)
    {
        var review = await _reviewRepository.GetByIdAsync(reviewId);
        if (review == null)
        {
            return false;
        }

        if (review.ModerationStatus != ReviewModerationStatus.Reported)
        {
            return false;
        }

        var decision = (dto.Decision ?? string.Empty).Trim().ToLowerInvariant();
        if (decision != "keepvisible" && decision != "hidecomment")
        {
            return false;
        }

        review.ModerationStatus = decision == "hidecomment"
            ? ReviewModerationStatus.Hidden
            : ReviewModerationStatus.ApprovedVisible;
        review.ModeratedByAdminId = adminUserId;
        review.ModerationReason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim();
        review.ModeratedAtUtc = DateTime.UtcNow;
        review.UpdatedAt = DateTime.UtcNow;

        await _reviewRepository.UpdateAsync(review);
        return true;
    }

    public Task<ReviewScoreSummaryDto> GetProviderScoreSummaryAsync(Guid providerId)
    {
        return BuildScoreSummaryAsync(providerId, UserRole.Provider);
    }

    public Task<ReviewScoreSummaryDto> GetClientScoreSummaryAsync(Guid clientId)
    {
        return BuildScoreSummaryAsync(clientId, UserRole.Client);
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

    private static decimal ParseDecimal(string? value, decimal fallback, decimal min, decimal max)
    {
        if (!decimal.TryParse(value, out var parsed))
        {
            return fallback;
        }

        return decimal.Clamp(parsed, min, max);
    }

    private static bool ParseBool(string? value, bool defaultValue)
    {
        return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private RepurchaseTriggerExecutionOptions NormalizeRepurchaseTriggerRequest(ReviewRepurchaseTriggerRequestDto request)
    {
        var minDays = request.MinDaysAfterCompletion > 0
            ? request.MinDaysAfterCompletion
            : _defaultRepurchaseMinDays;
        minDays = Math.Clamp(minDays, 1, 365);

        var maxDays = request.MaxDaysAfterCompletion > 0
            ? request.MaxDaysAfterCompletion
            : _defaultRepurchaseMaxDays;
        maxDays = Math.Clamp(maxDays, minDays, 730);

        var maxDispatch = request.MaxDispatch > 0
            ? request.MaxDispatch
            : _defaultRepurchaseMaxDispatch;
        maxDispatch = Math.Clamp(maxDispatch, 1, 1000);

        var minPositiveRating = request.MinPositiveRating > 0
            ? request.MinPositiveRating
            : _defaultRepurchaseMinPositiveRating;
        minPositiveRating = Math.Clamp(minPositiveRating, 1, 5);

        var minCompositeScore = request.MinCompositeScore > 0m
            ? request.MinCompositeScore
            : _defaultRepurchaseMinCompositeScore;
        minCompositeScore = decimal.Clamp(minCompositeScore, 0m, 100m);

        return new RepurchaseTriggerExecutionOptions(
            MinDaysAfterCompletion: minDays,
            MaxDaysAfterCompletion: maxDays,
            MaxDispatch: maxDispatch,
            RequirePositiveReview: request.RequirePositiveReview ?? _defaultRepurchaseRequirePositiveReview,
            MinPositiveRating: minPositiveRating,
            MinCompositeScore: minCompositeScore,
            DryRun: request.DryRun);
    }

    private async Task<HashSet<Guid>> GetAlreadyTriggeredRepurchaseRequestIdsAsync(DateTime fromUtc, DateTime toUtc)
    {
        if (_adminAuditLogRepository == null)
        {
            return new HashSet<Guid>();
        }

        var logs = await _adminAuditLogRepository.GetByTargetAndPeriodAsync(
            targetType: "ClientRepurchaseTrigger",
            fromUtc: fromUtc,
            toUtc: toUtc,
            action: "repurchase_nudge_sent",
            take: 10000);

        return logs
            .Where(log => log.TargetId.HasValue && log.TargetId.Value != Guid.Empty)
            .Select(log => log.TargetId!.Value)
            .ToHashSet();
    }

    private static bool HasRepurchaseAfterCompletion(
        IReadOnlyCollection<ServiceRequest> requestsByClient,
        Guid sourceRequestId,
        DateTime completionReferenceUtc)
    {
        return requestsByClient.Any(request =>
            request.Id != sourceRequestId &&
            request.CreatedAt > completionReferenceUtc &&
            request.Status != ServiceRequestStatus.Canceled);
    }

    private static bool IsPositiveReview(Review? review, int minPositiveRating, decimal minCompositeScore)
    {
        if (review == null)
        {
            return false;
        }

        if (review.WouldHireAgain == true)
        {
            return true;
        }

        if (review.CompositeScore.HasValue && review.CompositeScore.Value >= minCompositeScore)
        {
            return true;
        }

        return review.Rating >= minPositiveRating;
    }

    private static string ResolveCategoryLabel(ServiceRequest request)
    {
        if (request.CategoryDefinition != null && !string.IsNullOrWhiteSpace(request.CategoryDefinition.Name))
        {
            return request.CategoryDefinition.Name.Trim();
        }

        return request.Category.ToString();
    }

    private static string ResolveClientName(string? name)
    {
        return string.IsNullOrWhiteSpace(name)
            ? "Cliente"
            : name.Trim();
    }

    private static string BuildRepurchaseMessage(ReviewRepurchaseTriggerCandidateDto candidate)
    {
        var firstName = ResolveFirstName(candidate.ClientName);
        return $"Oi {firstName}, seu ultimo atendimento de {candidate.Category} foi concluido ha {candidate.DaysSinceCompletion} dia(s). Se precisar novamente, abra um novo pedido no ConsertaPraMim.";
    }

    private static string ResolveFirstName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return "cliente";
        }

        var first = fullName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(first) ? "cliente" : first;
    }

    private async Task RegisterRepurchaseAuditAsync(
        Guid actorUserId,
        string? actorEmail,
        string action,
        ReviewRepurchaseTriggerCandidateDto candidate,
        string? errorMessage)
    {
        if (_adminAuditLogRepository == null)
        {
            return;
        }

        var metadata = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["clientId"] = candidate.ClientId.ToString("N"),
            ["clientName"] = candidate.ClientName,
            ["category"] = candidate.Category,
            ["completedAtUtc"] = candidate.CompletedAtUtc,
            ["daysSinceCompletion"] = candidate.DaysSinceCompletion,
            ["clientRating"] = candidate.ClientRating,
            ["clientCompositeScore"] = candidate.ClientCompositeScore,
            ["wouldHireAgain"] = candidate.WouldHireAgain
        };
        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            metadata["errorMessage"] = errorMessage.Trim();
        }

        await _adminAuditLogRepository.AddAsync(new AdminAuditLog
        {
            ActorUserId = actorUserId == Guid.Empty ? Guid.Empty : actorUserId,
            ActorEmail = string.IsNullOrWhiteSpace(actorEmail) ? "system@consertapramim.local" : actorEmail.Trim(),
            Action = action,
            TargetType = "ClientRepurchaseTrigger",
            TargetId = candidate.RequestId,
            Metadata = JsonSerializer.Serialize(metadata)
        });
    }

    private static string NormalizeRepurchaseActionUrl(string? actionUrl)
    {
        if (string.IsNullOrWhiteSpace(actionUrl))
        {
            return "/ServiceRequests/Create";
        }

        var normalized = actionUrl.Trim();
        return normalized.StartsWith('/') ? normalized : "/ServiceRequests/Create";
    }

    private async Task<ReviewScoreSummaryDto> BuildScoreSummaryAsync(Guid userId, UserRole userRole)
    {
        var reviews = (await _reviewRepository.GetByRevieweeAsync(userId, userRole)).ToList();
        if (reviews.Count == 0)
        {
            return new ReviewScoreSummaryDto(
                userId,
                userRole,
                0,
                0,
                0,
                0,
                0,
                0,
                0);
        }

        var five = reviews.Count(r => r.Rating == 5);
        var four = reviews.Count(r => r.Rating == 4);
        var three = reviews.Count(r => r.Rating == 3);
        var two = reviews.Count(r => r.Rating == 2);
        var one = reviews.Count(r => r.Rating == 1);
        var average = Math.Round(reviews.Average(r => r.Rating), 2, MidpointRounding.AwayFromZero);

        return new ReviewScoreSummaryDto(
            userId,
            userRole,
            average,
            reviews.Count,
            five,
            four,
            three,
            two,
            one);
    }

    private static ReviewDto MapToDto(Review review)
    {
        return new ReviewDto(
            review.Id,
            review.RequestId,
            review.ClientId,
            review.ProviderId,
            review.ReviewerUserId,
            review.ReviewerRole,
            review.RevieweeUserId,
            review.RevieweeRole,
            review.Rating,
            GetPublicComment(review),
            review.ServiceQualityRating,
            review.PunctualityRating,
            review.CommunicationRating,
            review.CostBenefitRating,
            review.NpsScore,
            review.WouldHireAgain,
            review.CompositeScore,
            review.CreatedAt,
            review.ModerationStatus == ReviewModerationStatus.Reported,
            review.ModerationStatus.ToString(),
            review.ReportReason,
            review.ReportedByUserId,
            review.ReportedAtUtc,
            review.ModeratedByAdminId,
            review.ModerationReason,
            review.ModeratedAtUtc);
    }

    private static string GetPublicComment(Review review)
    {
        return review.ModerationStatus == ReviewModerationStatus.Hidden
            ? "Comentario removido pela moderacao."
            : review.Comment;
    }

    private static string ResolveCounterpartyName(string? name, string fallback)
    {
        return string.IsNullOrWhiteSpace(name)
            ? fallback
            : name.Trim();
    }

    private static int ResolveQuestionnaireScore(int? score, int fallbackRating)
    {
        if (!score.HasValue)
        {
            return fallbackRating;
        }

        return Math.Clamp(score.Value, 1, 5);
    }

    private static decimal CalculateCompositeScore(Review review)
    {
        var overallNormalized = NormalizeFivePointScore(review.Rating);
        var qualityNormalized = NormalizeFivePointScore(review.ServiceQualityRating ?? review.Rating);
        var punctualityNormalized = NormalizeFivePointScore(review.PunctualityRating ?? review.Rating);
        var communicationNormalized = NormalizeFivePointScore(review.CommunicationRating ?? review.Rating);
        var costBenefitNormalized = NormalizeFivePointScore(review.CostBenefitRating ?? review.Rating);

        var weightedBase =
            (overallNormalized * 0.25m) +
            (qualityNormalized * 0.25m) +
            (punctualityNormalized * 0.20m) +
            (communicationNormalized * 0.15m) +
            (costBenefitNormalized * 0.15m);

        if (review.NpsScore.HasValue)
        {
            var normalizedNps = Math.Clamp(review.NpsScore.Value, 0, 10) * 10m;
            weightedBase = (weightedBase * 0.80m) + (normalizedNps * 0.20m);
        }

        if (review.WouldHireAgain.HasValue)
        {
            weightedBase += review.WouldHireAgain.Value ? 3m : -3m;
        }

        return decimal.Round(Math.Clamp(weightedBase, 0m, 100m), 2, MidpointRounding.AwayFromZero);
    }

    private static decimal NormalizeFivePointScore(int score)
    {
        var normalized = Math.Clamp(score, 1, 5);
        return normalized * 20m;
    }

    private sealed record RepurchaseTriggerExecutionOptions(
        int MinDaysAfterCompletion,
        int MaxDaysAfterCompletion,
        int MaxDispatch,
        bool RequirePositiveReview,
        int MinPositiveRating,
        decimal MinCompositeScore,
        bool DryRun);
}
