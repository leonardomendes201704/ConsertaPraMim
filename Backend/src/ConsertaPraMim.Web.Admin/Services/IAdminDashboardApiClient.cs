using ConsertaPraMim.Web.Admin.Models;

namespace ConsertaPraMim.Web.Admin.Services;

public interface IAdminDashboardApiClient
{
    Task<AdminDashboardApiResult> GetDashboardAsync(
        AdminDashboardFilterModel filters,
        string accessToken,
        CancellationToken cancellationToken = default);
}
