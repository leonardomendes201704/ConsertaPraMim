using ConsertaPraMim.Web.Admin.Models;

namespace ConsertaPraMim.Web.Admin.Services;

public interface IAdminDatabaseSchemaService
{
    Task<AdminDatabaseSchemaViewModel> BuildViewModelAsync(CancellationToken cancellationToken = default);
}