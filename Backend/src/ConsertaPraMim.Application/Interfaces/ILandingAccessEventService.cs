using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface ILandingAccessEventService
{
    Task RecordAccessAsync(NotifyLandingAccessRequestDto request, CancellationToken cancellationToken = default);
}
