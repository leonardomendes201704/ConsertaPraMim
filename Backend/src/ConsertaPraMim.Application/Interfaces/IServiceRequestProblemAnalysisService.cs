using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IServiceRequestProblemAnalysisService
{
    Task<ServiceRequestProblemAnalysisResultDto> AnalyzeAsync(
        ServiceRequestProblemAnalysisRequestDto request,
        CancellationToken cancellationToken = default);
}
