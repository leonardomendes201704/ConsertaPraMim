using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IServiceJourneyAutomationGateway
{
    Task<ServiceJourneyAutomationResultDto> UpsertJourneyAsync(
        ServiceJourneyAutomationRequestDto request,
        CancellationToken cancellationToken = default);
}
