using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Web.Provider.Services;

public class ProviderLegacyAdminApiClient : IProviderLegacyAdminApiClient
{
    private readonly ProviderApiCaller _apiCaller;

    public ProviderLegacyAdminApiClient(ProviderApiCaller apiCaller)
    {
        _apiCaller = apiCaller;
    }

    public async Task<(AdminDashboardDto? Dashboard, string? ErrorMessage)> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var response = await _apiCaller.SendAsync<AdminDashboardDto>(
            HttpMethod.Get,
            "/api/admin/dashboard?page=1&pageSize=1",
            cancellationToken: cancellationToken);

        return (response.Payload, response.ErrorMessage);
    }

    public async Task<(IReadOnlyList<AdminUserListItemDto> Users, int TotalCount, string? ErrorMessage)> GetUsersAsync(
        string? searchTerm = null,
        string? role = null,
        bool? isActive = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"page={Math.Max(1, page)}",
            $"pageSize={Math.Max(1, pageSize)}"
        };

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query.Add($"searchTerm={Uri.EscapeDataString(searchTerm.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            query.Add($"role={Uri.EscapeDataString(role.Trim())}");
        }

        if (isActive.HasValue)
        {
            query.Add($"isActive={isActive.Value.ToString().ToLowerInvariant()}");
        }

        var path = "/api/admin/users?" + string.Join("&", query);
        var response = await _apiCaller.SendAsync<AdminUsersListResponseDto>(HttpMethod.Get, path, cancellationToken: cancellationToken);
        if (response.Payload == null)
        {
            return ([], 0, response.ErrorMessage);
        }

        return (response.Payload.Items, response.Payload.TotalCount, response.ErrorMessage);
    }

    public async Task<(AdminUserDetailsDto? User, string? ErrorMessage)> GetUserByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiCaller.SendAsync<AdminUserDetailsDto>(
            HttpMethod.Get,
            $"/api/admin/users/{userId}",
            cancellationToken: cancellationToken);

        return (response.Payload, response.ErrorMessage);
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateUserStatusAsync(
        Guid userId,
        bool isActive,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiCaller.SendAsync<AdminUpdateUserStatusResultDto>(
            HttpMethod.Put,
            $"/api/admin/users/{userId}/status",
            new AdminUpdateUserStatusRequestDto(isActive, reason),
            cancellationToken);

        if (!response.Success)
        {
            return (false, response.ErrorMessage);
        }

        if (response.Payload is { Success: false } payload)
        {
            return (false, payload.ErrorMessage ?? response.ErrorMessage);
        }

        return (true, null);
    }
}
