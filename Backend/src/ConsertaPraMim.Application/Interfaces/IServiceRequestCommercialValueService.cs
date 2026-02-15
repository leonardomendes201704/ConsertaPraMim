using ConsertaPraMim.Domain.Entities;

namespace ConsertaPraMim.Application.Interfaces;

public interface IServiceRequestCommercialValueService
{
    Task<ServiceRequestCommercialTotalsDto> RecalculateAsync(ServiceRequest serviceRequest);
}

public sealed record ServiceRequestCommercialTotalsDto(
    decimal BaseValue,
    decimal ApprovedIncrementalValue,
    decimal CurrentValue);
