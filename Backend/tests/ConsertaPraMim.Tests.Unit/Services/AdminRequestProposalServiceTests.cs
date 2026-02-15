using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminRequestProposalServiceTests
{
    private readonly Mock<IServiceRequestRepository> _serviceRequestRepositoryMock;
    private readonly Mock<IProposalRepository> _proposalRepositoryMock;
    private readonly Mock<IAdminAuditLogRepository> _auditLogRepositoryMock;
    private readonly Mock<IProviderGalleryService> _providerGalleryServiceMock;
    private readonly AdminRequestProposalService _service;

    public AdminRequestProposalServiceTests()
    {
        _serviceRequestRepositoryMock = new Mock<IServiceRequestRepository>();
        _proposalRepositoryMock = new Mock<IProposalRepository>();
        _auditLogRepositoryMock = new Mock<IAdminAuditLogRepository>();
        _providerGalleryServiceMock = new Mock<IProviderGalleryService>();

        _service = new AdminRequestProposalService(
            _serviceRequestRepositoryMock.Object,
            _proposalRepositoryMock.Object,
            _auditLogRepositoryMock.Object,
            _providerGalleryServiceMock.Object);
    }

    [Fact]
    public async Task GetServiceRequestsAsync_ShouldReturnPagedResultWithProposalCounters()
    {
        var now = DateTime.UtcNow;
        var requestId = Guid.NewGuid();

        _serviceRequestRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ServiceRequest>
        {
            new()
            {
                Id = requestId,
                Description = "Troca de tomada",
                Status = ServiceRequestStatus.Created,
                Category = ServiceCategory.Electrical,
                AddressZip = "11704150",
                CreatedAt = now.AddHours(-1),
                Client = new User { Name = "Cliente 1", Email = "cliente1@teste.com" }
            }
        });

        _proposalRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Proposal>
        {
            new() { Id = Guid.NewGuid(), RequestId = requestId, Accepted = false, IsInvalidated = false },
            new() { Id = Guid.NewGuid(), RequestId = requestId, Accepted = true, IsInvalidated = false },
            new() { Id = Guid.NewGuid(), RequestId = requestId, Accepted = false, IsInvalidated = true }
        });

        var result = await _service.GetServiceRequestsAsync(new AdminServiceRequestsQueryDto(null, "Created", null, now.AddDays(-1), now, 1, 10));

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(3, result.Items[0].TotalProposals);
        Assert.Equal(1, result.Items[0].AcceptedProposals);
        Assert.Equal(1, result.Items[0].InvalidatedProposals);
    }

    [Fact]
    public async Task UpdateServiceRequestStatusAsync_ShouldReturnInvalidStatus_WhenStatusIsUnknown()
    {
        var result = await _service.UpdateServiceRequestStatusAsync(
            Guid.NewGuid(),
            new AdminUpdateServiceRequestStatusRequestDto("Unknown", "x"),
            Guid.NewGuid(),
            "admin@teste.com");

        Assert.False(result.Success);
        Assert.Equal("invalid_status", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateServiceRequestStatusAsync_ShouldUpdateAndAuditWithBeforeAfter_WhenStatusIsValid()
    {
        var requestId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            Status = ServiceRequestStatus.Created
        };

        _serviceRequestRepositoryMock.Setup(r => r.GetByIdAsync(requestId)).ReturnsAsync(request);
        _serviceRequestRepositoryMock.Setup(r => r.UpdateAsync(request)).Returns(Task.CompletedTask);

        var result = await _service.UpdateServiceRequestStatusAsync(
            requestId,
            new AdminUpdateServiceRequestStatusRequestDto("Matching", "Moderacao"),
            actorId,
            "admin@teste.com");

        Assert.True(result.Success);
        Assert.Equal(ServiceRequestStatus.Matching, request.Status);
        _auditLogRepositoryMock.Verify(r => r.AddAsync(It.Is<AdminAuditLog>(a =>
            a.Action == "ServiceRequestStatusChanged" &&
            a.TargetId == requestId &&
            !string.IsNullOrWhiteSpace(a.Metadata) &&
            a.Metadata!.Contains("\"before\"") &&
            a.Metadata.Contains("\"after\""))), Times.Once);
    }

    [Fact]
    public async Task InvalidateProposalAsync_ShouldInvalidateAndRollbackScheduledRequest_WhenAcceptedProposal()
    {
        var requestId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            Status = ServiceRequestStatus.Scheduled
        };
        var proposal = new Proposal
        {
            Id = proposalId,
            RequestId = requestId,
            Accepted = true,
            IsInvalidated = false,
            Request = request
        };

        _proposalRepositoryMock.Setup(r => r.GetByIdAsync(proposalId)).ReturnsAsync(proposal);
        _proposalRepositoryMock.Setup(r => r.GetByRequestIdAsync(requestId)).ReturnsAsync(new List<Proposal> { proposal });

        var result = await _service.InvalidateProposalAsync(
            proposalId,
            new AdminInvalidateProposalRequestDto("Fraude"),
            actorUserId,
            "admin@teste.com");

        Assert.True(result.Success);
        Assert.True(proposal.IsInvalidated);
        Assert.False(proposal.Accepted);
        Assert.Equal(ServiceRequestStatus.Created, request.Status);
        _proposalRepositoryMock.Verify(r => r.UpdateAsync(proposal), Times.Once);
        _serviceRequestRepositoryMock.Verify(r => r.UpdateAsync(request), Times.Once);
        _auditLogRepositoryMock.Verify(r => r.AddAsync(It.Is<AdminAuditLog>(a =>
            a.Action == "ProposalInvalidated" &&
            a.TargetId == proposalId &&
            !string.IsNullOrWhiteSpace(a.Metadata) &&
            a.Metadata!.Contains("\"before\"") &&
            a.Metadata.Contains("\"after\""))), Times.Once);
    }

    [Fact]
    public async Task GetServiceRequestByIdAsync_ShouldReturnOperationalEvidencesOrderedByCreatedAtDesc()
    {
        var requestId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = requestId,
            ClientId = clientId,
            Client = new User
            {
                Id = clientId,
                Name = "Cliente",
                Email = "cliente@teste.com",
                Phone = "11999999999"
            },
            Description = "Troca de disjuntor",
            Status = ServiceRequestStatus.Scheduled,
            Category = ServiceCategory.Electrical,
            AddressStreet = "Rua 1",
            AddressCity = "Praia Grande",
            AddressZip = "11704150",
            Latitude = -24.0,
            Longitude = -46.4,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _serviceRequestRepositoryMock.Setup(r => r.GetByIdAsync(requestId)).ReturnsAsync(request);
        _proposalRepositoryMock.Setup(r => r.GetByRequestIdAsync(requestId)).ReturnsAsync(Array.Empty<Proposal>());
        _providerGalleryServiceMock.Setup(s => s.GetEvidenceTimelineByServiceRequestAsync(
                requestId,
                null,
                UserRole.Admin.ToString()))
            .ReturnsAsync(new List<ServiceRequestEvidenceTimelineItemDto>
        {
            new(
                Guid.NewGuid(),
                requestId,
                providerId,
                "Prestador 01",
                null,
                "Before",
                "/uploads/a-before.jpg",
                "/uploads/a-before-thumb.jpg",
                "/uploads/a-before.jpg",
                "a-before.jpg",
                "image/jpeg",
                "image",
                "Eletrica",
                "Antes",
                DateTime.UtcNow.AddHours(-2)),
            new(
                Guid.NewGuid(),
                requestId,
                providerId,
                "Prestador 01",
                null,
                "After",
                "/uploads/a-after.jpg",
                "/uploads/a-after-thumb.jpg",
                "/uploads/a-after.jpg",
                "a-after.jpg",
                "image/jpeg",
                "image",
                "Eletrica",
                "Depois",
                DateTime.UtcNow.AddHours(-1))
        });

        var result = await _service.GetServiceRequestByIdAsync(requestId);

        Assert.NotNull(result);
        Assert.NotNull(result!.Evidences);
        Assert.Equal(2, result.Evidences!.Count);
        Assert.Equal("After", result.Evidences[0].EvidencePhase);
        Assert.Equal("Before", result.Evidences[1].EvidencePhase);
    }
}
