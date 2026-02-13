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
