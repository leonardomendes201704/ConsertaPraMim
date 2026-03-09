using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Entities;

namespace ConsertaPraMim.Application.Interfaces;

public interface ILandingAdminNotificationService
{
    Task NotifyLandingAccessAsync(
        NotifyLandingAccessRequestDto request,
        CancellationToken cancellationToken = default);

    Task NotifyLandingLeadCapturedAsync(
        LandingLead lead,
        CancellationToken cancellationToken = default);
}
