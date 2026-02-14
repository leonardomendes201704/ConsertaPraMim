using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Infrastructure.Data;
using ConsertaPraMim.Infrastructure.Hubs;
using ConsertaPraMim.Infrastructure.Repositories;
using ConsertaPraMim.Infrastructure.Services;
using ConsertaPraMim.Tests.Unit.Integration.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Integration.Services;

public class ServiceAppointmentRealtimeIntegrationTests
{
    [Fact]
    public async Task UpdateOperationalStatusAsync_ShouldPersistAndBroadcastRealtimeNotification()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var client = CreateUser(UserRole.Client, "cliente.realtime.int@teste.com");
            var provider = CreateUser(UserRole.Provider, "prestador.realtime.int@teste.com");
            var request = CreateRequest(client.Id, ServiceCategory.Electrical, "Troca de disjuntor");
            request.Status = ServiceRequestStatus.Scheduled;

            var appointment = new ServiceAppointment
            {
                ServiceRequestId = request.Id,
                ClientId = client.Id,
                ProviderId = provider.Id,
                WindowStartUtc = DateTime.UtcNow.AddHours(2),
                WindowEndUtc = DateTime.UtcNow.AddHours(3),
                Status = ServiceAppointmentStatus.Arrived,
                ArrivedAtUtc = DateTime.UtcNow.AddMinutes(-20),
                OperationalStatus = ServiceAppointmentOperationalStatus.OnSite
            };

            context.Users.AddRange(client, provider);
            context.ServiceRequests.Add(request);
            context.ServiceAppointments.Add(appointment);
            await context.SaveChangesAsync();

            var notificationHarness = CreateHubNotificationHarness();
            var service = BuildService(context, notificationHarness.NotificationService);

            var result = await service.UpdateOperationalStatusAsync(
                provider.Id,
                UserRole.Provider.ToString(),
                appointment.Id,
                new UpdateServiceAppointmentOperationalStatusRequestDto("InService", "Inicio do atendimento"));

            Assert.True(result.Success, $"{result.ErrorCode} - {result.ErrorMessage}");

            var loaded = await context.ServiceAppointments.FindAsync(appointment.Id);
            Assert.NotNull(loaded);
            Assert.Equal(ServiceAppointmentStatus.InProgress, loaded!.Status);
            Assert.Equal(ServiceAppointmentOperationalStatus.InService, loaded.OperationalStatus);
            Assert.Equal("Inicio do atendimento", loaded.OperationalStatusReason);
            Assert.NotNull(loaded.OperationalStatusUpdatedAtUtc);

            var history = await context.ServiceAppointmentHistories
                .Where(h => h.ServiceAppointmentId == appointment.Id)
                .OrderByDescending(h => h.OccurredAtUtc)
                .FirstAsync();
            Assert.Equal(ServiceAppointmentOperationalStatus.OnSite, history.PreviousOperationalStatus);
            Assert.Equal(ServiceAppointmentOperationalStatus.InService, history.NewOperationalStatus);

            Assert.Equal(2, notificationHarness.GroupCalls.Count);
            Assert.Contains(NotificationHub.BuildUserGroup(client.Id), notificationHarness.GroupCalls);
            Assert.Contains(NotificationHub.BuildUserGroup(provider.Id), notificationHarness.GroupCalls);

            Assert.Equal(2, notificationHarness.Payloads.Count);
            Assert.All(notificationHarness.Payloads, payload =>
            {
                Assert.Equal("Agendamento: status operacional atualizado", payload.Subject);
                Assert.Equal($"/ServiceRequests/Details/{request.Id}", payload.ActionUrl);
                Assert.Contains("Em atendimento", payload.Message);
            });
        }
    }

    [Fact]
    public async Task UpdateOperationalStatusAsync_ShouldNotBroadcast_WhenTransitionIsInvalid()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var client = CreateUser(UserRole.Client, "cliente.realtime.invalid@teste.com");
            var provider = CreateUser(UserRole.Provider, "prestador.realtime.invalid@teste.com");
            var request = CreateRequest(client.Id, ServiceCategory.Plumbing, "Vazamento na pia");
            request.Status = ServiceRequestStatus.Scheduled;

            var appointment = new ServiceAppointment
            {
                ServiceRequestId = request.Id,
                ClientId = client.Id,
                ProviderId = provider.Id,
                WindowStartUtc = DateTime.UtcNow.AddHours(2),
                WindowEndUtc = DateTime.UtcNow.AddHours(3),
                Status = ServiceAppointmentStatus.Confirmed
            };

            context.Users.AddRange(client, provider);
            context.ServiceRequests.Add(request);
            context.ServiceAppointments.Add(appointment);
            await context.SaveChangesAsync();

            var notificationHarness = CreateHubNotificationHarness();
            var service = BuildService(context, notificationHarness.NotificationService);

            var result = await service.UpdateOperationalStatusAsync(
                provider.Id,
                UserRole.Provider.ToString(),
                appointment.Id,
                new UpdateServiceAppointmentOperationalStatusRequestDto("InService", "Tentativa invalida"));

            Assert.False(result.Success);
            Assert.Equal("invalid_operational_transition", result.ErrorCode);
            Assert.Empty(notificationHarness.GroupCalls);
            Assert.Empty(notificationHarness.Payloads);
        }
    }

    private static ServiceAppointmentService BuildService(
        ConsertaPraMimDbContext context,
        INotificationService notificationService)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServiceAppointments:ConfirmationExpiryHours"] = "12",
                ["ServiceAppointments:CancelMinimumHoursBeforeWindow"] = "2",
                ["ServiceAppointments:RescheduleMinimumHoursBeforeWindow"] = "2",
                ["ServiceAppointments:RescheduleMaximumAdvanceDays"] = "30",
                ["ServiceAppointments:AvailabilityTimeZoneId"] = "UTC"
            })
            .Build();

        return new ServiceAppointmentService(
            new ServiceAppointmentRepository(context),
            new ServiceRequestRepository(context),
            new UserRepository(context),
            notificationService,
            configuration);
    }

    private static NotificationHarness CreateHubNotificationHarness()
    {
        var payloads = new List<NotificationPayload>();
        var groupCalls = new List<string>();

        var clientProxyMock = new Mock<IClientProxy>();
        clientProxyMock
            .Setup(proxy => proxy.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, object?[], CancellationToken>((method, args, _) =>
            {
                if (!string.Equals(method, "ReceiveNotification", StringComparison.Ordinal))
                {
                    return;
                }

                var payload = args.Length > 0 ? args[0] : null;
                payloads.Add(new NotificationPayload(
                    ReadProperty(payload, "subject") ?? string.Empty,
                    ReadProperty(payload, "message") ?? string.Empty,
                    ReadProperty(payload, "actionUrl")));
            })
            .Returns(Task.CompletedTask);

        var clientsMock = new Mock<IHubClients>();
        clientsMock
            .Setup(clients => clients.Group(It.IsAny<string>()))
            .Returns((string groupName) =>
            {
                groupCalls.Add(groupName);
                return clientProxyMock.Object;
            });

        var hubContextMock = new Mock<IHubContext<NotificationHub>>();
        hubContextMock.SetupGet(hub => hub.Clients).Returns(clientsMock.Object);

        var loggerMock = new Mock<ILogger<HubNotificationService>>();
        var notificationService = new HubNotificationService(loggerMock.Object, hubContextMock.Object);
        return new NotificationHarness(notificationService, payloads, groupCalls);
    }

    private static string? ReadProperty(object? source, string propertyName)
    {
        if (source == null)
        {
            return null;
        }

        var property = source.GetType().GetProperty(propertyName);
        return property?.GetValue(source)?.ToString();
    }

    private static User CreateUser(UserRole role, string email)
    {
        return new User
        {
            Name = role == UserRole.Provider ? "Prestador Realtime" : "Cliente Realtime",
            Email = email,
            PasswordHash = "hash",
            Phone = "11999999999",
            Role = role
        };
    }

    private static ServiceRequest CreateRequest(Guid clientId, ServiceCategory category, string description)
    {
        return new ServiceRequest
        {
            ClientId = clientId,
            Category = category,
            Status = ServiceRequestStatus.Created,
            Description = description,
            AddressStreet = "Rua Realtime",
            AddressCity = "Praia Grande",
            AddressZip = "11704150",
            Latitude = -24.01,
            Longitude = -46.41
        };
    }

    private sealed record NotificationPayload(string Subject, string Message, string? ActionUrl);

    private sealed record NotificationHarness(
        INotificationService NotificationService,
        List<NotificationPayload> Payloads,
        List<string> GroupCalls);
}
