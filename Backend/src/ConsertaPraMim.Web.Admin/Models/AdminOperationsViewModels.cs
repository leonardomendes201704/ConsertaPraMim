using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Web.Admin.Models;

public class AdminServiceRequestsFilterModel
{
    public string? SearchTerm { get; set; }
    public string Status { get; set; } = "all";
    public string Category { get; set; } = "all";
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class AdminServiceRequestsIndexViewModel
{
    public AdminServiceRequestsFilterModel Filters { get; set; } = new();
    public AdminServiceRequestsListResponseDto? Requests { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

    public int TotalPages
    {
        get
        {
            if (Requests == null || Requests.PageSize <= 0) return 0;
            return (int)Math.Ceiling((double)Requests.TotalCount / Requests.PageSize);
        }
    }
}

public class AdminServiceRequestDetailsViewModel
{
    public AdminServiceRequestDetailsDto? Request { get; set; }
    public string? ErrorMessage { get; set; }
}

public class AdminServiceRequestStatusUpdateWebRequest
{
    public Guid RequestId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public class AdminProposalsFilterModel
{
    public Guid? RequestId { get; set; }
    public Guid? ProviderId { get; set; }
    public string Status { get; set; } = "all";
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class AdminProposalsIndexViewModel
{
    public AdminProposalsFilterModel Filters { get; set; } = new();
    public AdminProposalsListResponseDto? Proposals { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

    public int TotalPages
    {
        get
        {
            if (Proposals == null || Proposals.PageSize <= 0) return 0;
            return (int)Math.Ceiling((double)Proposals.TotalCount / Proposals.PageSize);
        }
    }
}

public class AdminProposalInvalidateWebRequest
{
    public Guid ProposalId { get; set; }
    public string? Reason { get; set; }
}

public class AdminChatsFilterModel
{
    public Guid? RequestId { get; set; }
    public Guid? ProviderId { get; set; }
    public Guid? ClientId { get; set; }
    public string? SearchTerm { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class AdminChatsIndexViewModel
{
    public AdminChatsFilterModel Filters { get; set; } = new();
    public AdminChatsListResponseDto? Chats { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

    public int TotalPages
    {
        get
        {
            if (Chats == null || Chats.PageSize <= 0) return 0;
            return (int)Math.Ceiling((double)Chats.TotalCount / Chats.PageSize);
        }
    }
}

public class AdminDisputesQueueFilterModel
{
    public Guid? DisputeCaseId { get; set; }
    public int Take { get; set; } = 100;
}

public class AdminDisputesQueuePageViewModel
{
    public AdminDisputesQueueFilterModel Filters { get; set; } = new();
    public AdminDisputesQueueResponseDto? Queue { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}

public class AdminDisputeCaseDetailsPageViewModel
{
    public Guid DisputeCaseId { get; set; }
    public AdminDisputeCaseDetailsDto? Case { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}

public class AdminDisputeWorkflowUpdateWebRequest
{
    public Guid DisputeCaseId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? WaitingForRole { get; set; }
    public string? Note { get; set; }
    public bool ClaimOwnership { get; set; } = true;
}

public class AdminDisputeDecisionWebRequest
{
    public Guid DisputeCaseId { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public string Justification { get; set; } = string.Empty;
    public string? ResolutionSummary { get; set; }
    public string? FinancialAction { get; set; }
    public decimal? FinancialAmount { get; set; }
    public string? FinancialReason { get; set; }
}

public class AdminChatAttachmentsFilterModel
{
    public Guid? RequestId { get; set; }
    public Guid? UserId { get; set; }
    public string? MediaKind { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 100;
}

public class AdminChatDetailsViewModel
{
    public AdminChatDetailsDto? Chat { get; set; }
    public AdminChatAttachmentsListResponseDto? Attachments { get; set; }
    public string? ErrorMessage { get; set; }
}

public class AdminManualNotificationWebRequest
{
    public Guid? RecipientUserId { get; set; }
    public string? RecipientEmail { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public string? Reason { get; set; }
}

public class AdminServiceCategoriesIndexViewModel
{
    public bool IncludeInactive { get; set; } = true;
    public IReadOnlyList<AdminServiceCategoryDto> Categories { get; set; } = Array.Empty<AdminServiceCategoryDto>();
    public string? ErrorMessage { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}

public class AdminCreateServiceCategoryWebRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string LegacyCategory { get; set; } = string.Empty;
}

public class AdminUpdateServiceCategoryWebRequest
{
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string LegacyCategory { get; set; } = string.Empty;
}

public class AdminUpdateServiceCategoryStatusWebRequest
{
    public Guid CategoryId { get; set; }
    public bool IsActive { get; set; }
    public string? Reason { get; set; }
}

public class AdminChecklistTemplatesIndexViewModel
{
    public bool IncludeInactive { get; set; } = true;
    public IReadOnlyList<AdminChecklistTemplateDto> Templates { get; set; } = Array.Empty<AdminChecklistTemplateDto>();
    public IReadOnlyList<AdminServiceCategoryDto> Categories { get; set; } = Array.Empty<AdminServiceCategoryDto>();
    public string? ErrorMessage { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}

public class AdminChecklistTemplateItemWebRequest
{
    public Guid? Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? HelpText { get; set; }
    public bool IsRequired { get; set; } = true;
    public bool RequiresEvidence { get; set; }
    public bool AllowNote { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public class AdminCreateChecklistTemplateWebRequest
{
    public Guid CategoryDefinitionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<AdminChecklistTemplateItemWebRequest> Items { get; set; } = new();
}

public class AdminUpdateChecklistTemplateWebRequest
{
    public Guid TemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<AdminChecklistTemplateItemWebRequest> Items { get; set; } = new();
}

public class AdminUpdateChecklistTemplateStatusWebRequest
{
    public Guid TemplateId { get; set; }
    public bool IsActive { get; set; }
    public string? Reason { get; set; }
}

public class AdminPlanGovernanceIndexViewModel
{
    public bool IncludeInactivePromotions { get; set; } = true;
    public bool IncludeInactiveCoupons { get; set; } = true;
    public AdminPlanGovernanceSnapshotDto? Snapshot { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}

public class AdminUpdatePlanSettingWebRequest
{
    public string Plan { get; set; } = string.Empty;
    public decimal MonthlyPrice { get; set; }
    public double MaxRadiusKm { get; set; }
    public int MaxAllowedCategories { get; set; }
    public List<string> AllowedCategories { get; set; } = new();
}

public class AdminCreatePlanPromotionWebRequest
{
    public string Plan { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DiscountType { get; set; } = "Percentage";
    public decimal DiscountValue { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
}

public class AdminUpdatePlanPromotionWebRequest
{
    public Guid PromotionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DiscountType { get; set; } = "Percentage";
    public decimal DiscountValue { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
}

public class AdminUpdatePlanPromotionStatusWebRequest
{
    public Guid PromotionId { get; set; }
    public bool IsActive { get; set; }
    public string? Reason { get; set; }
}

public class AdminCreatePlanCouponWebRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Plan { get; set; }
    public string DiscountType { get; set; } = "Percentage";
    public decimal DiscountValue { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public int? MaxGlobalUses { get; set; }
    public int? MaxUsesPerProvider { get; set; }
}

public class AdminUpdatePlanCouponWebRequest
{
    public Guid CouponId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Plan { get; set; }
    public string DiscountType { get; set; } = "Percentage";
    public decimal DiscountValue { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public int? MaxGlobalUses { get; set; }
    public int? MaxUsesPerProvider { get; set; }
}

public class AdminUpdatePlanCouponStatusWebRequest
{
    public Guid CouponId { get; set; }
    public bool IsActive { get; set; }
    public string? Reason { get; set; }
}

public class AdminPlanSimulationWebRequest
{
    public string Plan { get; set; } = string.Empty;
    public string? CouponCode { get; set; }
    public DateTime? AtUtc { get; set; }
    public Guid? ProviderUserId { get; set; }
}
