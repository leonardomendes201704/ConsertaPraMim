using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Admin.Models;

namespace ConsertaPraMim.Web.Admin.Services;

public interface IAdminDashboardApiClient
{
    Task<AdminDashboardApiResult> GetDashboardAsync(
        AdminDashboardFilterModel filters,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminNoShowDashboardApiResult> GetNoShowDashboardAsync(
        AdminDashboardFilterModel filters,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminNoShowAlertThresholdApiResult> GetNoShowAlertThresholdsAsync(
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminNoShowAlertThresholdApiResult> UpdateNoShowAlertThresholdsAsync(
        AdminUpdateNoShowAlertThresholdRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default);
}
