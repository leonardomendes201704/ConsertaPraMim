using ConsertaPraMim.Web.Admin.Models;

namespace ConsertaPraMim.Web.Admin.Services;

public interface IAdminChangeLogsService
{
    Task<AdminChangeLogsViewModel> BuildViewModelAsync(
        string? searchTerm,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken = default);
}
