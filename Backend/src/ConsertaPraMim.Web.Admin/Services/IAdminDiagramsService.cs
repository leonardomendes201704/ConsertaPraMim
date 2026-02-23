using ConsertaPraMim.Web.Admin.Models;

namespace ConsertaPraMim.Web.Admin.Services;

public interface IAdminDiagramsService
{
    Task<AdminDiagramsViewModel> BuildViewModelAsync(string? selectedDiagramPath, CancellationToken cancellationToken = default);
}
