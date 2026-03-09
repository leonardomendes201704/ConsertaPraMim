using ConsertaPraMim.Web.Landing.Models;

namespace ConsertaPraMim.Web.Landing.Services;

public interface ILandingAdminNotificationsClient
{
    Task NotifyLandingAccessAsync(
        LandingAccessNotificationRequest request,
        CancellationToken cancellationToken = default);
}
