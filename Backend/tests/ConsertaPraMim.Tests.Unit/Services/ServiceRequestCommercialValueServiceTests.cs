using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Moq;
using Xunit;

namespace ConsertaPraMim.Tests.Unit.Services;

public class ServiceRequestCommercialValueServiceTests
{
    /// <summary>
    /// Cenario: pedido possui proposta aceita e multiplas alteracoes de escopo com status distintos.
    /// Passos: recalcula valores somando apenas incrementos aprovados pelo cliente.
    /// Resultado esperado: base permanece da proposta aceita e valor atual inclui somente incrementos elegiveis.
    /// </summary>
    [Fact(DisplayName = "Servico requisicao commercial value servico | Recalculate | Deve retornar base e current quando tem approved scope changes")]
    public async Task RecalculateAsync_ShouldReturnBaseAndCurrent_WhenHasApprovedScopeChanges()
    {
        var requestRepositoryMock = new Mock<IServiceRequestRepository>();
        var scopeChangeRepositoryMock = new Mock<IServiceScopeChangeRequestRepository>();
        var service = new ServiceRequestCommercialValueService(
            requestRepositoryMock.Object,
            scopeChangeRepositoryMock.Object);

        var requestId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            Proposals =
            {
                new Proposal
                {
                    Accepted = true,
                    IsInvalidated = false,
                    EstimatedValue = 120m
                }
            }
        };

        scopeChangeRepositoryMock
            .Setup(r => r.GetByServiceRequestIdAsync(requestId))
            .ReturnsAsync(new List<ServiceScopeChangeRequest>
            {
                new()
                {
                    ServiceRequestId = requestId,
                    Status = ServiceScopeChangeRequestStatus.ApprovedByClient,
                    IncrementalValue = 35m
                },
                new()
                {
                    ServiceRequestId = requestId,
                    Status = ServiceScopeChangeRequestStatus.PendingClientApproval,
                    IncrementalValue = 50m
                },
                new()
                {
                    ServiceRequestId = requestId,
                    Status = ServiceScopeChangeRequestStatus.ApprovedByClient,
                    IncrementalValue = 15m
                }
            });

        var result = await service.RecalculateAsync(request);

        Assert.Equal(120m, result.BaseValue);
        Assert.Equal(50m, result.ApprovedIncrementalValue);
        Assert.Equal(170m, result.CurrentValue);
    }

    /// <summary>
    /// Cenario: servico recebe entidade parcial sem propostas carregadas em memoria.
    /// Passos: hidrata request no repositorio, recalcula sem incrementos aprovados e consolida valores.
    /// Resultado esperado: usa base da proposta persistida e mantem current igual ao base na ausencia de adicionais.
    /// </summary>
    [Fact(DisplayName = "Servico requisicao commercial value servico | Recalculate | Deve hydrate requisicao de repository quando proposals missing")]
    public async Task RecalculateAsync_ShouldHydrateRequestFromRepository_WhenProposalsAreMissing()
    {
        var requestRepositoryMock = new Mock<IServiceRequestRepository>();
        var scopeChangeRepositoryMock = new Mock<IServiceScopeChangeRequestRepository>();
        var service = new ServiceRequestCommercialValueService(
            requestRepositoryMock.Object,
            scopeChangeRepositoryMock.Object);

        var requestId = Guid.NewGuid();
        var input = new ServiceRequest
        {
            Id = requestId
        };

        requestRepositoryMock
            .Setup(r => r.GetByIdAsync(requestId))
            .ReturnsAsync(new ServiceRequest
            {
                Id = requestId,
                Proposals =
                {
                    new Proposal
                    {
                        Accepted = true,
                        IsInvalidated = false,
                        EstimatedValue = 89.90m
                    }
                }
            });

        scopeChangeRepositoryMock
            .Setup(r => r.GetByServiceRequestIdAsync(requestId))
            .ReturnsAsync(Array.Empty<ServiceScopeChangeRequest>());

        var result = await service.RecalculateAsync(input);

        Assert.Equal(89.90m, result.BaseValue);
        Assert.Equal(0m, result.ApprovedIncrementalValue);
        Assert.Equal(89.90m, result.CurrentValue);
        requestRepositoryMock.Verify(r => r.GetByIdAsync(requestId), Times.Once);
    }
}
