using Moq;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Domain.Enums;
using Xunit;

namespace ConsertaPraMim.Tests.Unit.Services;

public class ProposalServiceTests
{
    private readonly Mock<IProposalRepository> _proposalRepoMock;
    private readonly Mock<IServiceRequestRepository> _requestRepoMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly ProposalService _service;

    public ProposalServiceTests()
    {
        _proposalRepoMock = new Mock<IProposalRepository>();
        _requestRepoMock = new Mock<IServiceRequestRepository>();
        _notificationServiceMock = new Mock<INotificationService>();
        _service = new ProposalService(_proposalRepoMock.Object, _requestRepoMock.Object, _notificationServiceMock.Object);
    }

    [Fact]
    public async Task CreateAsync_ShouldSaveProposal_WhenCalled()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var dto = new CreateProposalDto(Guid.NewGuid(), 150.0m, "I can fix it");

        // Act
        var result = await _service.CreateAsync(providerId, dto);

        // Assert
        Assert.NotEqual(Guid.Empty, result);
        _proposalRepoMock.Verify(r => r.AddAsync(It.Is<Proposal>(p => 
            p.ProviderId == providerId && p.EstimatedValue == 150.0m)), Times.Once);
    }

    [Fact]
    public async Task AcceptAsync_ShouldUpdateStatus_WhenClientMatches()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        
        var request = new ServiceRequest { Id = requestId, ClientId = clientId, Status = ServiceRequestStatus.Created };
        var proposal = new Proposal { Id = proposalId, RequestId = requestId, Request = request, Accepted = false };

        _proposalRepoMock.Setup(r => r.GetByIdAsync(proposalId)).ReturnsAsync(proposal);

        // Act
        var result = await _service.AcceptAsync(proposalId, clientId);

        // Assert
        Assert.True(result);
        Assert.True(proposal.Accepted);
        Assert.Equal(ServiceRequestStatus.Scheduled, request.Status);
        _proposalRepoMock.Verify(r => r.UpdateAsync(proposal), Times.Once);
        _requestRepoMock.Verify(r => r.UpdateAsync(request), Times.Once);
    }

    [Fact]
    public async Task AcceptAsync_ShouldReturnFalse_WhenClientDoesNotMatch()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        
        var request = new ServiceRequest { ClientId = ownerId };
        var proposal = new Proposal { Id = proposalId, Request = request };

        _proposalRepoMock.Setup(r => r.GetByIdAsync(proposalId)).ReturnsAsync(proposal);

        // Act
        var result = await _service.AcceptAsync(proposalId, otherUserId);

        // Assert
        Assert.False(result);
        Assert.False(proposal.Accepted);
    }
}
