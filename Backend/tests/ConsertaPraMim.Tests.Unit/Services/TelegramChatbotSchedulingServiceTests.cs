using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class TelegramChatbotSchedulingServiceTests
{
    [Fact(DisplayName = "Telegram scheduling | Query | Deve listar pedidos do cliente com paginacao e proxima visita")]
    public async Task GetClientOrdersAsync_ShouldReturnPagedOrdersWithUpcomingAppointment()
    {
        var clientId = Guid.NewGuid();
        var requestA = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Status = ServiceRequestStatus.Scheduled,
            Category = ServiceCategory.Plumbing,
            Description = "Torneira pingando",
            AddressCity = "Praia Grande",
            AddressStreet = "Rua A",
            AddressZip = "11704150",
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            Proposals =
            [
                new Proposal
                {
                    Id = Guid.NewGuid(),
                    ProviderId = Guid.NewGuid(),
                    Accepted = true
                }
            ]
        };

        var requestB = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Status = ServiceRequestStatus.Matching,
            Category = ServiceCategory.Appliances,
            Description = "Ar com erro CH26",
            AddressCity = "Santos",
            AddressStreet = "Rua B",
            AddressZip = "11000000",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        var requestRepositoryMock = new Mock<IServiceRequestRepository>();
        requestRepositoryMock
            .Setup(repository => repository.GetByClientIdAsync(clientId))
            .ReturnsAsync([requestA, requestB]);

        var appointmentRepositoryMock = new Mock<IServiceAppointmentRepository>();
        appointmentRepositoryMock
            .Setup(repository => repository.GetByClientAsync(clientId, null, null))
            .ReturnsAsync(
            [
                new ServiceAppointment
                {
                    Id = Guid.NewGuid(),
                    ServiceRequestId = requestA.Id,
                    ClientId = clientId,
                    ProviderId = Guid.NewGuid(),
                    Status = ServiceAppointmentStatus.Confirmed,
                    WindowStartUtc = DateTime.UtcNow.AddDays(1),
                    WindowEndUtc = DateTime.UtcNow.AddDays(1).AddHours(2)
                }
            ]);

        var service = BuildService(
            requestRepositoryMock,
            new Mock<IUserRepository>(),
            out _,
            out _,
            out _,
            appointmentRepositoryMock);

        var result = await service.GetClientOrdersAsync(clientId, skip: 0, take: 2);

        Assert.True(result.Success);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Orders.Count);
        Assert.False(result.HasMore);
        var orderWithVisit = result.Orders.First(item => item.ServiceRequestId == requestA.Id);
        Assert.Equal("Scheduled", orderWithVisit.Status);
        Assert.Equal("Confirmed", orderWithVisit.NextAppointmentStatus);
    }

    [Fact(DisplayName = "Telegram scheduling | Query | Deve bloquear detalhe de pedido quando cliente nao e dono")]
    public async Task GetOrderDetailsAsync_ShouldReturnForbidden_WhenClientDoesNotOwnRequest()
    {
        var requestId = Guid.NewGuid();

        var requestRepositoryMock = new Mock<IServiceRequestRepository>();
        requestRepositoryMock
            .Setup(repository => repository.GetByIdAsync(requestId))
            .ReturnsAsync(new ServiceRequest
            {
                Id = requestId,
                ClientId = Guid.NewGuid(),
                Status = ServiceRequestStatus.Created,
                Category = ServiceCategory.Plumbing,
                Description = "Teste",
                AddressStreet = "Rua X",
                AddressCity = "Cidade X",
                AddressZip = "11000000"
            });

        var service = BuildService(
            requestRepositoryMock,
            new Mock<IUserRepository>(),
            out _,
            out _);

        var result = await service.GetOrderDetailsAsync(Guid.NewGuid(), requestId);

        Assert.False(result.Success);
        Assert.Equal("forbidden", result.ErrorCode);
    }

    [Fact(DisplayName = "Telegram scheduling | Matching | Deve listar prestadores elegiveis ordenados por distancia")]
    public async Task GetEligibleProvidersAsync_ShouldReturnEligibleProvidersOrderedByDistance()
    {
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var requestRepositoryMock = new Mock<IServiceRequestRepository>();
        requestRepositoryMock
            .Setup(repository => repository.GetByIdAsync(requestId))
            .ReturnsAsync(new ServiceRequest
            {
                Id = requestId,
                ClientId = clientId,
                Status = ServiceRequestStatus.Created,
                Category = ServiceCategory.Appliances,
                Latitude = -23.5505,
                Longitude = -46.6333,
                Client = new User
                {
                    Id = clientId,
                    ClientProfileType = ClientProfileType.Pf
                }
            });

        var nearProvider = BuildProvider(
            name: "Prestador Perto",
            latitude: -23.5510,
            longitude: -46.6330,
            radiusKm: 8,
            categories: [ServiceCategory.Appliances],
            rating: 4.9,
            reviewCount: 34);

        var farProvider = BuildProvider(
            name: "Prestador Longe",
            latitude: -23.6200,
            longitude: -46.7000,
            radiusKm: 20,
            categories: [ServiceCategory.Appliances],
            rating: 4.2,
            reviewCount: 12);

        var wrongCategoryProvider = BuildProvider(
            name: "Categoria Errada",
            latitude: -23.5510,
            longitude: -46.6330,
            radiusKm: 8,
            categories: [ServiceCategory.Electrical],
            rating: 5.0,
            reviewCount: 100);

        var userRepositoryMock = new Mock<IUserRepository>();
        userRepositoryMock
            .Setup(repository => repository.GetAllAsync())
            .ReturnsAsync([nearProvider, farProvider, wrongCategoryProvider]);

        var service = BuildService(
            requestRepositoryMock,
            userRepositoryMock,
            out _,
            out _);

        var result = await service.GetEligibleProvidersAsync(clientId, requestId, take: 5);

        Assert.True(result.Success);
        Assert.Equal(2, result.Providers.Count);
        Assert.Equal("Prestador Perto", result.Providers[0].ProviderName);
        Assert.Equal("Prestador Longe", result.Providers[1].ProviderName);
    }

    [Fact(DisplayName = "Telegram scheduling | Matching | Deve bloquear cliente sem acesso ao pedido")]
    public async Task GetEligibleProvidersAsync_ShouldReturnForbidden_WhenClientDoesNotOwnRequest()
    {
        var requestId = Guid.NewGuid();
        var requestRepositoryMock = new Mock<IServiceRequestRepository>();
        requestRepositoryMock
            .Setup(repository => repository.GetByIdAsync(requestId))
            .ReturnsAsync(new ServiceRequest
            {
                Id = requestId,
                ClientId = Guid.NewGuid(),
                Status = ServiceRequestStatus.Created
            });

        var userRepositoryMock = new Mock<IUserRepository>();
        var service = BuildService(
            requestRepositoryMock,
            userRepositoryMock,
            out _,
            out _);

        var result = await service.GetEligibleProvidersAsync(Guid.NewGuid(), requestId, take: 5);

        Assert.False(result.Success);
        Assert.Equal("forbidden", result.ErrorCode);
    }

    [Fact(DisplayName = "Telegram scheduling | Matching | Deve retornar erro para pedido encerrado")]
    public async Task GetEligibleProvidersAsync_ShouldReturnRequestClosed_WhenRequestIsClosed()
    {
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var requestRepositoryMock = new Mock<IServiceRequestRepository>();
        requestRepositoryMock
            .Setup(repository => repository.GetByIdAsync(requestId))
            .ReturnsAsync(new ServiceRequest
            {
                Id = requestId,
                ClientId = clientId,
                Status = ServiceRequestStatus.Completed
            });

        var userRepositoryMock = new Mock<IUserRepository>();
        var service = BuildService(
            requestRepositoryMock,
            userRepositoryMock,
            out _,
            out _);

        var result = await service.GetEligibleProvidersAsync(clientId, requestId, take: 5);

        Assert.False(result.Success);
        Assert.Equal("request_closed", result.ErrorCode);
    }

    [Fact(DisplayName = "Telegram scheduling | Batch | Deve bloquear mais de 3 visitas")]
    public async Task ScheduleVisitsAsync_ShouldFail_WhenMoreThanThreeVisitsAreProvided()
    {
        var request = new TelegramChatbotBatchScheduleRequestDto(
            ClientId: Guid.NewGuid(),
            ServiceRequestId: Guid.NewGuid(),
            Visits:
            [
                BuildVisitRequest(Guid.NewGuid(), DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2)),
                BuildVisitRequest(Guid.NewGuid(), DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(2).AddHours(2)),
                BuildVisitRequest(Guid.NewGuid(), DateTime.UtcNow.AddDays(3), DateTime.UtcNow.AddDays(3).AddHours(2)),
                BuildVisitRequest(Guid.NewGuid(), DateTime.UtcNow.AddDays(4), DateTime.UtcNow.AddDays(4).AddHours(2))
            ]);

        var service = BuildService(
            new Mock<IServiceRequestRepository>(),
            new Mock<IUserRepository>(),
            out _,
            out _);

        var result = await service.ScheduleVisitsAsync(request);

        Assert.False(result.Success);
        Assert.Equal("max_visits_exceeded", result.ErrorCode);
    }

    [Fact(DisplayName = "Telegram scheduling | Batch | Deve bloquear visitas no mesmo dia")]
    public async Task ScheduleVisitsAsync_ShouldFail_WhenVisitsRepeatTheSameDay()
    {
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var serviceRequest = new ServiceRequest
        {
            Id = requestId,
            ClientId = clientId,
            Status = ServiceRequestStatus.Created,
            Category = ServiceCategory.Appliances,
            Latitude = -23.5505,
            Longitude = -46.6333,
            Client = new User
            {
                Id = clientId,
                ClientProfileType = ClientProfileType.Pf
            }
        };

        var requestRepositoryMock = new Mock<IServiceRequestRepository>();
        requestRepositoryMock
            .Setup(repository => repository.GetByIdAsync(requestId))
            .ReturnsAsync(serviceRequest);

        var userRepositoryMock = new Mock<IUserRepository>();
        userRepositoryMock
            .Setup(repository => repository.GetAllAsync())
            .ReturnsAsync([BuildProvider(
                name: "Prestador",
                latitude: -23.5510,
                longitude: -46.6330,
                radiusKm: 10,
                categories: [ServiceCategory.Appliances],
                rating: 4.8,
                reviewCount: 20)]);

        var service = BuildService(
            requestRepositoryMock,
            userRepositoryMock,
            out _,
            out _);

        var day = DateTime.UtcNow.Date.AddDays(2);
        var request = new TelegramChatbotBatchScheduleRequestDto(
            ClientId: clientId,
            ServiceRequestId: requestId,
            Visits:
            [
                BuildVisitRequest(Guid.NewGuid(), day.AddHours(9), day.AddHours(11)),
                BuildVisitRequest(Guid.NewGuid(), day.AddHours(13), day.AddHours(15))
            ]);

        var result = await service.ScheduleVisitsAsync(request);

        Assert.False(result.Success);
        Assert.Equal("duplicate_visit_day", result.ErrorCode);
    }

    [Fact(DisplayName = "Telegram scheduling | Batch | Deve retornar falha por visita quando create do agendamento conflita")]
    public async Task ScheduleVisitsAsync_ShouldReturnPerVisitFailure_WhenAppointmentCreationFails()
    {
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var providerId = Guid.NewGuid();

        var serviceRequest = new ServiceRequest
        {
            Id = requestId,
            ClientId = clientId,
            Status = ServiceRequestStatus.Created,
            Category = ServiceCategory.Appliances,
            Latitude = -23.5505,
            Longitude = -46.6333,
            Client = new User
            {
                Id = clientId,
                ClientProfileType = ClientProfileType.Pf
            }
        };

        var requestRepositoryMock = new Mock<IServiceRequestRepository>();
        requestRepositoryMock
            .Setup(repository => repository.GetByIdAsync(requestId))
            .ReturnsAsync(serviceRequest);

        var userRepositoryMock = new Mock<IUserRepository>();
        userRepositoryMock
            .Setup(repository => repository.GetAllAsync())
            .ReturnsAsync([BuildProvider(
                name: "Prestador",
                latitude: -23.5510,
                longitude: -46.6330,
                radiusKm: 10,
                categories: [ServiceCategory.Appliances],
                rating: 4.8,
                reviewCount: 20,
                providerId: providerId)]);

        var service = BuildService(
            requestRepositoryMock,
            userRepositoryMock,
            out var proposalRepositoryMock,
            out var appointmentServiceMock);

        appointmentServiceMock
            .Setup(appointments => appointments.CreateAsync(
                clientId,
                "Client",
                It.IsAny<CreateServiceAppointmentRequestDto>()))
            .ReturnsAsync(new ServiceAppointmentOperationResultDto(
                Success: false,
                ErrorCode: "slot_unavailable",
                ErrorMessage: "A janela escolhida nao esta disponivel para o prestador."));

        var request = new TelegramChatbotBatchScheduleRequestDto(
            ClientId: clientId,
            ServiceRequestId: requestId,
            Visits:
            [
                BuildVisitRequest(
                    providerId,
                    DateTime.UtcNow.Date.AddDays(3).AddHours(9),
                    DateTime.UtcNow.Date.AddDays(3).AddHours(11))
            ]);

        var result = await service.ScheduleVisitsAsync(request);

        Assert.False(result.Success);
        Assert.Single(result.Results);
        Assert.False(result.Results[0].Success);
        Assert.Equal("slot_unavailable", result.Results[0].ErrorCode);

        proposalRepositoryMock.Verify(repository => repository.AddAsync(It.IsAny<Proposal>()), Times.Once);
        appointmentServiceMock.Verify(
            appointments => appointments.CreateAsync(clientId, "Client", It.IsAny<CreateServiceAppointmentRequestDto>()),
            Times.Once);
    }

    [Fact(DisplayName = "Telegram scheduling | Batch | Deve criar sync pendente quando agendamento for criado com sucesso")]
    public async Task ScheduleVisitsAsync_ShouldCreatePendingCalendarSync_WhenAppointmentIsCreated()
    {
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();

        var serviceRequest = new ServiceRequest
        {
            Id = requestId,
            ClientId = clientId,
            Status = ServiceRequestStatus.Created,
            Category = ServiceCategory.Appliances,
            Latitude = -23.5505,
            Longitude = -46.6333,
            Client = new User
            {
                Id = clientId,
                ClientProfileType = ClientProfileType.Pf
            }
        };

        var requestRepositoryMock = new Mock<IServiceRequestRepository>();
        requestRepositoryMock
            .Setup(repository => repository.GetByIdAsync(requestId))
            .ReturnsAsync(serviceRequest);

        var userRepositoryMock = new Mock<IUserRepository>();
        userRepositoryMock
            .Setup(repository => repository.GetAllAsync())
            .ReturnsAsync([BuildProvider(
                name: "Prestador",
                latitude: -23.5510,
                longitude: -46.6330,
                radiusKm: 10,
                categories: [ServiceCategory.Appliances],
                rating: 4.8,
                reviewCount: 20,
                providerId: providerId)]);

        var calendarSyncRepositoryMock = new Mock<IServiceAppointmentCalendarSyncRepository>();
        calendarSyncRepositoryMock
            .Setup(repository => repository.GetByAppointmentIdAsync(appointmentId))
            .ReturnsAsync((ServiceAppointmentCalendarSync?)null);

        var service = BuildService(
            requestRepositoryMock,
            userRepositoryMock,
            out _,
            out var appointmentServiceMock,
            customCalendarSyncRepositoryMock: calendarSyncRepositoryMock);

        appointmentServiceMock
            .Setup(appointments => appointments.CreateAsync(
                clientId,
                "Client",
                It.IsAny<CreateServiceAppointmentRequestDto>()))
            .ReturnsAsync(new ServiceAppointmentOperationResultDto(
                Success: true,
                Appointment: BuildAppointmentDto(appointmentId, requestId, clientId, providerId)));

        var request = new TelegramChatbotBatchScheduleRequestDto(
            ClientId: clientId,
            ServiceRequestId: requestId,
            Visits:
            [
                BuildVisitRequest(
                    providerId,
                    DateTime.UtcNow.Date.AddDays(3).AddHours(9),
                    DateTime.UtcNow.Date.AddDays(3).AddHours(11))
            ]);

        var result = await service.ScheduleVisitsAsync(request);

        Assert.True(result.Success);
        Assert.Single(result.Results);
        Assert.True(result.Results[0].Success);
        Assert.Equal(appointmentId, result.Results[0].AppointmentId);

        calendarSyncRepositoryMock.Verify(
            repository => repository.GetByAppointmentIdAsync(appointmentId),
            Times.Once);
        calendarSyncRepositoryMock.Verify(
            repository => repository.AddAsync(It.Is<ServiceAppointmentCalendarSync>(sync =>
                sync.AppointmentId == appointmentId &&
                sync.SyncStatus == ServiceAppointmentCalendarSyncStatus.Pending &&
                sync.Error == null)),
            Times.Once);
        calendarSyncRepositoryMock.Verify(
            repository => repository.UpdateAsync(It.IsAny<ServiceAppointmentCalendarSync>()),
            Times.Never);
    }

    [Fact(DisplayName = "Telegram scheduling | Batch | Deve atualizar sync existente para pending quando agendamento for criado")]
    public async Task ScheduleVisitsAsync_ShouldUpdateCalendarSyncToPending_WhenSyncAlreadyExists()
    {
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();

        var serviceRequest = new ServiceRequest
        {
            Id = requestId,
            ClientId = clientId,
            Status = ServiceRequestStatus.Created,
            Category = ServiceCategory.Appliances,
            Latitude = -23.5505,
            Longitude = -46.6333,
            Client = new User
            {
                Id = clientId,
                ClientProfileType = ClientProfileType.Pf
            }
        };

        var requestRepositoryMock = new Mock<IServiceRequestRepository>();
        requestRepositoryMock
            .Setup(repository => repository.GetByIdAsync(requestId))
            .ReturnsAsync(serviceRequest);

        var userRepositoryMock = new Mock<IUserRepository>();
        userRepositoryMock
            .Setup(repository => repository.GetAllAsync())
            .ReturnsAsync([BuildProvider(
                name: "Prestador",
                latitude: -23.5510,
                longitude: -46.6330,
                radiusKm: 10,
                categories: [ServiceCategory.Appliances],
                rating: 4.8,
                reviewCount: 20,
                providerId: providerId)]);

        var existingSync = new ServiceAppointmentCalendarSync
        {
            AppointmentId = appointmentId,
            SyncStatus = ServiceAppointmentCalendarSyncStatus.Failed,
            Error = "timeout_google_api"
        };

        var calendarSyncRepositoryMock = new Mock<IServiceAppointmentCalendarSyncRepository>();
        calendarSyncRepositoryMock
            .Setup(repository => repository.GetByAppointmentIdAsync(appointmentId))
            .ReturnsAsync(existingSync);

        var service = BuildService(
            requestRepositoryMock,
            userRepositoryMock,
            out _,
            out var appointmentServiceMock,
            customCalendarSyncRepositoryMock: calendarSyncRepositoryMock);

        appointmentServiceMock
            .Setup(appointments => appointments.CreateAsync(
                clientId,
                "Client",
                It.IsAny<CreateServiceAppointmentRequestDto>()))
            .ReturnsAsync(new ServiceAppointmentOperationResultDto(
                Success: true,
                Appointment: BuildAppointmentDto(appointmentId, requestId, clientId, providerId)));

        var request = new TelegramChatbotBatchScheduleRequestDto(
            ClientId: clientId,
            ServiceRequestId: requestId,
            Visits:
            [
                BuildVisitRequest(
                    providerId,
                    DateTime.UtcNow.Date.AddDays(3).AddHours(9),
                    DateTime.UtcNow.Date.AddDays(3).AddHours(11))
            ]);

        var result = await service.ScheduleVisitsAsync(request);

        Assert.True(result.Success);
        Assert.Single(result.Results);
        Assert.True(result.Results[0].Success);

        calendarSyncRepositoryMock.Verify(
            repository => repository.AddAsync(It.IsAny<ServiceAppointmentCalendarSync>()),
            Times.Never);
        calendarSyncRepositoryMock.Verify(
            repository => repository.UpdateAsync(It.Is<ServiceAppointmentCalendarSync>(sync =>
                ReferenceEquals(sync, existingSync) &&
                sync.SyncStatus == ServiceAppointmentCalendarSyncStatus.Pending &&
                sync.Error == null)),
            Times.Once);
    }

    private static TelegramChatbotSchedulingService BuildService(
        Mock<IServiceRequestRepository> requestRepositoryMock,
        Mock<IUserRepository> userRepositoryMock,
        out Mock<IProposalRepository> proposalRepositoryMock,
        out Mock<IServiceAppointmentService> appointmentServiceMock,
        Mock<IServiceAppointmentCalendarSyncRepository>? customCalendarSyncRepositoryMock = null)
    {
        return BuildService(
            requestRepositoryMock,
            userRepositoryMock,
            out proposalRepositoryMock,
            out _,
            out appointmentServiceMock,
            customCalendarSyncRepositoryMock: customCalendarSyncRepositoryMock);
    }

    private static TelegramChatbotSchedulingService BuildService(
        Mock<IServiceRequestRepository> requestRepositoryMock,
        Mock<IUserRepository> userRepositoryMock,
        out Mock<IProposalRepository> proposalRepositoryMock,
        out Mock<IServiceAppointmentRepository> appointmentRepositoryMock,
        out Mock<IServiceAppointmentService> appointmentServiceMock,
        Mock<IServiceAppointmentRepository>? customAppointmentRepositoryMock = null,
        Mock<IServiceAppointmentCalendarSyncRepository>? customCalendarSyncRepositoryMock = null)
    {
        proposalRepositoryMock = new Mock<IProposalRepository>();
        appointmentRepositoryMock = customAppointmentRepositoryMock ?? new Mock<IServiceAppointmentRepository>();
        if (customAppointmentRepositoryMock is null)
        {
            appointmentRepositoryMock
                .Setup(repository => repository.GetByClientAsync(It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync([]);
        }

        var calendarSyncRepositoryMock = customCalendarSyncRepositoryMock ?? new Mock<IServiceAppointmentCalendarSyncRepository>();
        appointmentServiceMock = new Mock<IServiceAppointmentService>();

        return new TelegramChatbotSchedulingService(
            requestRepositoryMock.Object,
            userRepositoryMock.Object,
            proposalRepositoryMock.Object,
            appointmentRepositoryMock.Object,
            calendarSyncRepositoryMock.Object,
            appointmentServiceMock.Object);
    }

    private static TelegramChatbotBatchScheduleVisitRequestDto BuildVisitRequest(
        Guid providerId,
        DateTime windowStartUtc,
        DateTime windowEndUtc)
    {
        return new TelegramChatbotBatchScheduleVisitRequestDto(
            ProviderId: providerId,
            WindowStartUtc: windowStartUtc,
            WindowEndUtc: windowEndUtc,
            Reason: "Teste");
    }

    private static User BuildProvider(
        string name,
        double latitude,
        double longitude,
        double radiusKm,
        IReadOnlyList<ServiceCategory> categories,
        double rating,
        int reviewCount,
        Guid? providerId = null)
    {
        var id = providerId ?? Guid.NewGuid();
        return new User
        {
            Id = id,
            Name = name,
            Role = UserRole.Provider,
            IsActive = true,
            ProviderProfile = new ProviderProfile
            {
                UserId = id,
                BaseLatitude = latitude,
                BaseLongitude = longitude,
                RadiusKm = radiusKm,
                Categories = categories.ToList(),
                Rating = rating,
                ReviewCount = reviewCount,
                ClientPreference = ProviderClientPreference.Both
            }
        };
    }

    private static ServiceAppointmentDto BuildAppointmentDto(
        Guid appointmentId,
        Guid serviceRequestId,
        Guid clientId,
        Guid providerId)
    {
        return new ServiceAppointmentDto(
            Id: appointmentId,
            ServiceRequestId: serviceRequestId,
            ClientId: clientId,
            ProviderId: providerId,
            Status: ServiceAppointmentStatus.Confirmed.ToString(),
            WindowStartUtc: DateTime.UtcNow.AddDays(2),
            WindowEndUtc: DateTime.UtcNow.AddDays(2).AddHours(2),
            ExpiresAtUtc: null,
            Reason: "Teste",
            ProposedWindowStartUtc: null,
            ProposedWindowEndUtc: null,
            RescheduleRequestedAtUtc: null,
            RescheduleRequestedByRole: null,
            RescheduleRequestReason: null,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: null,
            History: Array.Empty<ServiceAppointmentHistoryDto>());
    }
}
