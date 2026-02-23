using ConsertaPraMim.Web.Admin.Models;

namespace ConsertaPraMim.Web.Admin.Services;

public interface IAdminRoadmapService
{
    Task<AdminRoadmapViewModel> BuildViewModelAsync(
        string? searchTerm,
        string? epicFilter,
        string? trackFilter,
        string? statusFilter,
        CancellationToken cancellationToken = default);
}
