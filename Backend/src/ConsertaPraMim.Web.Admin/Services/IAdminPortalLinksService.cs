namespace ConsertaPraMim.Web.Admin.Services;

public interface IAdminPortalLinksService
{
    Task<AdminPortalLinksDto> GetPortalLinksAsync(CancellationToken cancellationToken = default);
}
