using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IServiceFinancialPolicyCalculationService
{
    Task<ServiceFinancialCalculationResultDto> CalculateAsync(
        ServiceFinancialCalculationRequestDto request,
        CancellationToken cancellationToken = default);
}
