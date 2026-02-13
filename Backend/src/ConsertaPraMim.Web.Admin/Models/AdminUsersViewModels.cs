using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Web.Admin.Models;

public class AdminUsersFilterModel
{
    public string? SearchTerm { get; set; }
    public string Role { get; set; } = "all";
    public bool? IsActive { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class AdminUsersIndexViewModel
{
    public AdminUsersFilterModel Filters { get; set; } = new();
    public AdminUsersListResponseDto? Users { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

    public int TotalPages
    {
        get
        {
            if (Users == null || Users.PageSize <= 0) return 0;
            return (int)Math.Ceiling((double)Users.TotalCount / Users.PageSize);
        }
    }
}

public class AdminUserDetailsViewModel
{
    public AdminUserDetailsDto? User { get; set; }
    public string? ErrorMessage { get; set; }
}

public class AdminUpdateUserStatusWebRequest
{
    public Guid UserId { get; set; }
    public bool IsActive { get; set; }
    public string? Reason { get; set; }
}

public class AdminApiResult<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorCode { get; init; }
    public int? StatusCode { get; init; }

    public static AdminApiResult<T> Ok(T data)
        => new() { Success = true, Data = data };

    public static AdminApiResult<T> Fail(string message, string? errorCode = null, int? statusCode = null)
        => new() { Success = false, ErrorMessage = message, ErrorCode = errorCode, StatusCode = statusCode };
}
