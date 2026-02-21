using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class MobileClientOrderServiceTests
{
    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Mobile cliente pedido servico | Obter my pedidos | Deve split pedidos e expose active proposal count.
    /// </summary>
    [Fact(DisplayName = "Mobile cliente pedido servico | Obter my pedidos | Deve split pedidos e expose active proposal count")]
    public async Task GetMyOrdersAsync_ShouldSplitOrdersAndExposeActiveProposalCount()
    {
        var clientId = Guid.NewGuid();

        var openRequest = BuildRequest(
            clientId,
            ServiceRequestStatus.Created,
            "Troca de tomada",
            proposalCount: 3,
            invalidatedProposalCount: 1);

        var finalizedRequest = BuildRequest(
            clientId,
            ServiceRequestStatus.Completed,
            "Pintura do quarto",
            proposalCount: 1,
            invalidatedProposalCount: 0);

        var repository = new Mock<IServiceRequestRepository>();
        repository
            .Setup(r => r.GetByClientIdAsync(clientId))
            .ReturnsAsync(new List<ServiceRequest> { finalizedRequest, openRequest });

        var service = CreateService(repository.Object);

        var result = await service.GetMyOrdersAsync(clientId);

        Assert.Single(result.OpenOrders);
        Assert.Single(result.FinalizedOrders);
        Assert.Equal(2, result.TotalOrdersCount);

        Assert.Equal(openRequest.Id, result.OpenOrders[0].Id);
        Assert.Equal(2, result.OpenOrders[0].ProposalCount);

        Assert.Equal(finalizedRequest.Id, result.FinalizedOrders[0].Id);
        Assert.Equal(1, result.FinalizedOrders[0].ProposalCount);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Mobile cliente pedido servico | Obter pedido details | Deve expose proposal count on pedido summary.
    /// </summary>
    [Fact(DisplayName = "Mobile cliente pedido servico | Obter pedido details | Deve expose proposal count on pedido summary")]
    public async Task GetOrderDetailsAsync_ShouldExposeProposalCountOnOrderSummary()
    {
        var clientId = Guid.NewGuid();
        var request = BuildRequest(
            clientId,
            ServiceRequestStatus.Matching,
            "Vazamento na cozinha",
            proposalCount: 4,
            invalidatedProposalCount: 2);

        var repository = new Mock<IServiceRequestRepository>();
        repository
            .Setup(r => r.GetByIdAsync(request.Id))
            .ReturnsAsync(request);

        var service = CreateService(repository.Object);

        var result = await service.GetOrderDetailsAsync(clientId, request.Id);

        Assert.NotNull(result);
        Assert.Equal(request.Id, result!.Order.Id);
        Assert.Equal(2, result.Order.ProposalCount);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Mobile cliente pedido servico | Obter pedido details | Deve include proposal reference em timeline.
    /// </summary>
    [Fact(DisplayName = "Mobile cliente pedido servico | Obter pedido details | Deve include proposal reference em timeline")]
    public async Task GetOrderDetailsAsync_ShouldIncludeProposalReferenceInTimeline()
    {
        var clientId = Guid.NewGuid();
        var request = BuildRequest(
            clientId,
            ServiceRequestStatus.Matching,
            "Troca de tomada",
            proposalCount: 1,
            invalidatedProposalCount: 0);

        var proposalId = request.Proposals.First().Id;

        var repository = new Mock<IServiceRequestRepository>();
        repository
            .Setup(r => r.GetByIdAsync(request.Id))
            .ReturnsAsync(request);

        var service = CreateService(repository.Object);
        var result = await service.GetOrderDetailsAsync(clientId, request.Id);

        var proposalEvent = result!.Timeline.FirstOrDefault(item => item.EventCode == "proposal_received");

        Assert.NotNull(proposalEvent);
        Assert.Equal("proposal", proposalEvent!.RelatedEntityType);
        Assert.Equal(proposalId, proposalEvent.RelatedEntityId);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Mobile cliente pedido servico | Obter pedido proposal details | Deve retornar proposal details quando requisicao belongs para cliente.
    /// </summary>
    [Fact(DisplayName = "Mobile cliente pedido servico | Obter pedido proposal details | Deve retornar proposal details quando requisicao belongs para cliente")]
    public async Task GetOrderProposalDetailsAsync_ShouldReturnProposalDetails_WhenRequestBelongsToClient()
    {
        var clientId = Guid.NewGuid();
        var request = BuildRequest(
            clientId,
            ServiceRequestStatus.Matching,
            "Conserto de vazamento",
            proposalCount: 1,
            invalidatedProposalCount: 0);

        var proposal = request.Proposals.First();
        proposal.Message = "Levo as pecas necessarias";
        proposal.EstimatedValue = 180.50m;
        proposal.Provider = new User
        {
            Id = proposal.ProviderId,
            Name = "Prestador 20"
        };

        var repository = new Mock<IServiceRequestRepository>();
        repository
            .Setup(r => r.GetByIdAsync(request.Id))
            .ReturnsAsync(request);

        var service = CreateService(repository.Object);
        var result = await service.GetOrderProposalDetailsAsync(clientId, request.Id, proposal.Id);

        Assert.NotNull(result);
        Assert.Equal(request.Id, result!.Order.Id);
        Assert.Equal(proposal.Id, result.Proposal.Id);
        Assert.Equal(proposal.ProviderId, result.Proposal.ProviderId);
        Assert.Equal("Prestador 20", result.Proposal.ProviderName);
        Assert.Equal(180.50m, result.Proposal.EstimatedValue);
        Assert.Equal("Recebida", result.Proposal.StatusLabel);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Mobile cliente pedido servico | Obter pedido proposal details | Deve expose current appointment quando existe for proposal prestador.
    /// </summary>
    [Fact(DisplayName = "Mobile cliente pedido servico | Obter pedido proposal details | Deve expose current appointment quando existe for proposal prestador")]
    public async Task GetOrderProposalDetailsAsync_ShouldExposeCurrentAppointment_WhenExistsForProposalProvider()
    {
        var clientId = Guid.NewGuid();
        var request = BuildRequest(
            clientId,
            ServiceRequestStatus.Scheduled,
            "Instalacao de luminaria",
            proposalCount: 1,
            invalidatedProposalCount: 0);

        var proposal = request.Proposals.First();
        proposal.Accepted = true;
        proposal.Provider = new User
        {
            Id = proposal.ProviderId,
            Name = "Prestador 03"
        };

        request.Appointments.Add(new ServiceAppointment
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = request.Id,
            ClientId = clientId,
            ProviderId = proposal.ProviderId,
            Provider = proposal.Provider,
            Status = ServiceAppointmentStatus.PendingProviderConfirmation,
            WindowStartUtc = DateTime.UtcNow.AddDays(1).Date.AddHours(13),
            WindowEndUtc = DateTime.UtcNow.AddDays(1).Date.AddHours(14),
            CreatedAt = DateTime.UtcNow
        });

        var repository = new Mock<IServiceRequestRepository>();
        repository
            .Setup(r => r.GetByIdAsync(request.Id))
            .ReturnsAsync(request);

        var service = CreateService(repository.Object);
        var result = await service.GetOrderProposalDetailsAsync(clientId, request.Id, proposal.Id);

        Assert.NotNull(result);
        Assert.NotNull(result!.CurrentAppointment);
        Assert.Equal(proposal.ProviderId, result.CurrentAppointment!.ProviderId);
        Assert.Equal("Aguardando confirmacao do prestador", result.CurrentAppointment.StatusLabel);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Mobile cliente pedido servico | Accept proposal | Deve accept e retornar updated proposal details.
    /// </summary>
    [Fact(DisplayName = "Mobile cliente pedido servico | Accept proposal | Deve accept e retornar updated proposal details")]
    public async Task AcceptProposalAsync_ShouldAcceptAndReturnUpdatedProposalDetails()
    {
        var clientId = Guid.NewGuid();
        var request = BuildRequest(
            clientId,
            ServiceRequestStatus.Matching,
            "Troca de chuveiro",
            proposalCount: 1,
            invalidatedProposalCount: 0);

        var proposal = request.Proposals.First();
        proposal.Provider = new User
        {
            Id = proposal.ProviderId,
            Name = "Prestador 01"
        };

        var acceptedRequest = BuildRequest(
            clientId,
            ServiceRequestStatus.Scheduled,
            request.Description,
            proposalCount: 1,
            invalidatedProposalCount: 0);
        acceptedRequest.Id = request.Id;
        var acceptedProposal = acceptedRequest.Proposals.First();
        acceptedProposal.Id = proposal.Id;
        acceptedProposal.ProviderId = proposal.ProviderId;
        acceptedProposal.Provider = proposal.Provider;
        acceptedProposal.Accepted = true;
        acceptedProposal.EstimatedValue = 250m;

        var repository = new Mock<IServiceRequestRepository>();
        repository
            .SetupSequence(r => r.GetByIdAsync(request.Id))
            .ReturnsAsync(request)
            .ReturnsAsync(acceptedRequest);

        var proposalService = new Mock<IProposalService>();
        proposalService
            .Setup(s => s.AcceptAsync(proposal.Id, clientId))
            .ReturnsAsync(true);

        var service = CreateService(repository.Object, proposalService.Object);

        var result = await service.AcceptProposalAsync(clientId, request.Id, proposal.Id);

        Assert.NotNull(result);
        Assert.Equal("Aceita", result!.Proposal.StatusLabel);
        Assert.True(result.Proposal.Accepted);
        proposalService.Verify(s => s.AcceptAsync(proposal.Id, clientId), Times.Once);
    }

    private static MobileClientOrderService CreateService(
        IServiceRequestRepository repository,
        IProposalService? proposalService = null)
    {
        var proposalServiceMock = proposalService ?? new Mock<IProposalService>().Object;
        return new MobileClientOrderService(repository, proposalServiceMock);
    }

    private static ServiceRequest BuildRequest(
        Guid clientId,
        ServiceRequestStatus status,
        string description,
        int proposalCount,
        int invalidatedProposalCount)
    {
        var request = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Status = status,
            Category = ServiceCategory.Plumbing,
            Description = description,
            AddressStreet = "Rua A",
            AddressCity = "Sao Paulo",
            AddressZip = "01001000",
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };

        for (var index = 0; index < proposalCount; index++)
        {
            request.Proposals.Add(new Proposal
            {
                Id = Guid.NewGuid(),
                RequestId = request.Id,
                ProviderId = Guid.NewGuid(),
                CreatedAt = request.CreatedAt.AddMinutes(index),
                IsInvalidated = index < invalidatedProposalCount
            });
        }

        return request;
    }
}
