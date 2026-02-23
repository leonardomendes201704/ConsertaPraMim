using ConsertaPraMim.Web.Admin.Models;

namespace ConsertaPraMim.Web.Admin.Services;

public interface IAdminWikiService
{
    Task<AdminWikiViewModel> BuildViewModelAsync(string? selectedDocumentPath, CancellationToken cancellationToken = default);
}
