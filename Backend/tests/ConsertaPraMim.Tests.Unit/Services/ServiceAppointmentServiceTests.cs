using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class ServiceAppointmentServiceTests
{
    private readonly Mock<IServiceAppointmentRepository> _appointmentRepositoryMock;
    private readonly Mock<IServiceRequestRepository> _requestRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly ServiceAppointmentService _service;

    public ServiceAppointmentServiceTests()
    {
        _appointmentRepositoryMock = new Mock<IServiceAppointmentRepository>();
        _requestRepositoryMock = new Mock<IServiceRequestRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();

        _service = new ServiceAppointmentService(
            _appointmentRepositoryMock.Object,
            _requestRepositoryMock.Object,
            _userRepositoryMock.Object);
    }

    [Fact]
    public async Task GetAvailableSlotsAsync_ShouldReturnForbidden_WhenProviderQueriesAnotherProvider()
    {
        var actorProviderId = Guid.NewGuid();
        var otherProviderId = Guid.NewGuid();
        var query = new GetServiceAppointmentSlotsQueryDto(
            otherProviderId,
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(6));

        var result = await _service.GetAvailableSlotsAsync(actorProviderId, UserRole.Provider.ToString(), query);

        Assert.False(result.Success);
        Assert.Equal("forbidden", result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnProviderNotAssigned_WhenProposalIsNotAccepted()
    {
        var clientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var request = BuildRequest(clientId, providerId, acceptedProposal: false);
        request.Id = requestId;

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(providerId))
            .ReturnsAsync(new User { Id = providerId, Role = UserRole.Provider, IsActive = true });

        _requestRepositoryMock
            .Setup(r => r.GetByIdAsync(requestId))
            .ReturnsAsync(request);

        var dto = new CreateServiceAppointmentRequestDto(
            requestId,
            providerId,
            DateTime.UtcNow.AddHours(2),
            DateTime.UtcNow.AddHours(3));

        var result = await _service.CreateAsync(clientId, UserRole.Client.ToString(), dto);

        Assert.False(result.Success);
        Assert.Equal("provider_not_assigned", result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateAppointment_WhenRequestAndSlotAreValid()
    {
        var clientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var windowStartUtc = NextUtcDayOfWeek(DateTime.UtcNow, DayOfWeek.Monday).AddHours(10);
        var windowEndUtc = windowStartUtc.AddHours(1);
        var request = BuildRequest(clientId, providerId, acceptedProposal: true);
        request.Id = requestId;
        request.Status = ServiceRequestStatus.Created;

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(providerId))
            .ReturnsAsync(new User { Id = providerId, Role = UserRole.Provider, IsActive = true });

        _requestRepositoryMock
            .Setup(r => r.GetByIdAsync(requestId))
            .ReturnsAsync(request);

        _appointmentRepositoryMock
            .Setup(r => r.GetByRequestIdAsync(requestId))
            .ReturnsAsync((ServiceAppointment?)null);

        _appointmentRepositoryMock
            .Setup(r => r.GetAvailabilityRulesByProviderAsync(providerId))
            .ReturnsAsync(new List<ProviderAvailabilityRule>
            {
                new()
                {
                    ProviderId = providerId,
                    DayOfWeek = DayOfWeek.Monday,
                    StartTime = TimeSpan.FromHours(8),
                    EndTime = TimeSpan.FromHours(18),
                    SlotDurationMinutes = 30,
                    IsActive = true
                }
            });

        _appointmentRepositoryMock
            .Setup(r => r.GetAvailabilityExceptionsByProviderAsync(
                providerId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>()))
            .ReturnsAsync(Array.Empty<ProviderAvailabilityException>());

        _appointmentRepositoryMock
            .Setup(r => r.GetProviderAppointmentsByStatusesInRangeAsync(
                providerId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<IReadOnlyCollection<ServiceAppointmentStatus>>()))
            .ReturnsAsync(Array.Empty<ServiceAppointment>());

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((ServiceAppointment?)null);

        var dto = new CreateServiceAppointmentRequestDto(
            requestId,
            providerId,
            windowStartUtc,
            windowEndUtc,
            "Agendamento inicial");

        var result = await _service.CreateAsync(clientId, UserRole.Client.ToString(), dto);

        Assert.True(result.Success);
        Assert.NotNull(result.Appointment);
        Assert.Equal(requestId, result.Appointment!.ServiceRequestId);
        Assert.Equal(ServiceRequestStatus.Scheduled, request.Status);

        _appointmentRepositoryMock.Verify(r => r.AddAsync(It.IsAny<ServiceAppointment>()), Times.Once);
        _appointmentRepositoryMock.Verify(r => r.AddHistoryAsync(It.IsAny<ServiceAppointmentHistory>()), Times.Once);
        _requestRepositoryMock.Verify(r => r.UpdateAsync(request), Times.Once);
    }

    private static ServiceRequest BuildRequest(Guid clientId, Guid providerId, bool acceptedProposal)
    {
        return new ServiceRequest
        {
            ClientId = clientId,
            Category = ServiceCategory.Electrical,
            Description = "Troca de disjuntor",
            AddressStreet = "Rua A",
            AddressCity = "Praia Grande",
            AddressZip = "11704150",
            Latitude = -24.01,
            Longitude = -46.41,
            Status = ServiceRequestStatus.Scheduled,
            Proposals =
            {
                new Proposal
                {
                    ProviderId = providerId,
                    Accepted = acceptedProposal,
                    IsInvalidated = false
                }
            }
        };
    }

    private static DateTime NextUtcDayOfWeek(DateTime fromUtc, DayOfWeek dayOfWeek)
    {
        var date = fromUtc.Date;
        var offset = ((int)dayOfWeek - (int)date.DayOfWeek + 7) % 7;
        if (offset == 0)
        {
            offset = 7;
        }

        return DateTime.SpecifyKind(date.AddDays(offset), DateTimeKind.Utc);
    }
}
