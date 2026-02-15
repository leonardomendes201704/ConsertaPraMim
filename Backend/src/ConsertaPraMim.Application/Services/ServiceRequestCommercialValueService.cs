using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;

namespace ConsertaPraMim.Application.Services;

public class ServiceRequestCommercialValueService : IServiceRequestCommercialValueService
{
    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly IServiceScopeChangeRequestRepository _scopeChangeRequestRepository;

    public ServiceRequestCommercialValueService(
        IServiceRequestRepository serviceRequestRepository,
        IServiceScopeChangeRequestRepository scopeChangeRequestRepository)
    {
        _serviceRequestRepository = serviceRequestRepository;
        _scopeChangeRequestRepository = scopeChangeRequestRepository;
    }

    public async Task<ServiceRequestCommercialTotalsDto> RecalculateAsync(ServiceRequest serviceRequest)
    {
        if (serviceRequest == null)
        {
            throw new ArgumentNullException(nameof(serviceRequest));
        }

        var sourceRequest = serviceRequest;
        if (sourceRequest.Id != Guid.Empty &&
            (sourceRequest.Proposals == null || sourceRequest.Proposals.Count == 0))
        {
            var persistedRequest = await _serviceRequestRepository.GetByIdAsync(sourceRequest.Id);
            if (persistedRequest != null)
            {
                sourceRequest = persistedRequest;
            }
        }

        var acceptedProposalValue = decimal.Round(
            (sourceRequest.Proposals ?? Array.Empty<Proposal>())
                .Where(p => p.Accepted && !p.IsInvalidated)
                .Select(p => p.EstimatedValue)
                .Where(v => v.HasValue && v.Value > 0m)
                .Select(v => v!.Value)
                .DefaultIfEmpty(0m)
                .Max(),
            2,
            MidpointRounding.AwayFromZero);

        var baseValue = decimal.Round(
            Math.Max(0m, sourceRequest.CommercialBaseValue ?? acceptedProposalValue),
            2,
            MidpointRounding.AwayFromZero);

        IReadOnlyList<ServiceScopeChangeRequest> scopeChanges = Array.Empty<ServiceScopeChangeRequest>();
        if (sourceRequest.Id != Guid.Empty)
        {
            scopeChanges = await _scopeChangeRequestRepository.GetByServiceRequestIdAsync(sourceRequest.Id);
        }

        var approvedIncrementalValue = decimal.Round(
            Math.Max(
                0m,
                scopeChanges
                    .Where(sc => sc.Status == ServiceScopeChangeRequestStatus.ApprovedByClient)
                    .Select(sc => sc.IncrementalValue)
                    .Where(v => v > 0m)
                    .Sum()),
            2,
            MidpointRounding.AwayFromZero);

        var currentValue = decimal.Round(
            baseValue + approvedIncrementalValue,
            2,
            MidpointRounding.AwayFromZero);

        return new ServiceRequestCommercialTotalsDto(
            baseValue,
            approvedIncrementalValue,
            currentValue);
    }
}
