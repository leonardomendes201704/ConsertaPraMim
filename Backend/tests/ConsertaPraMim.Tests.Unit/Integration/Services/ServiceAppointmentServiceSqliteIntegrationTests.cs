using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Infrastructure.Data;
using ConsertaPraMim.Infrastructure.Repositories;
using ConsertaPraMim.Tests.Unit.Integration.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace ConsertaPraMim.Tests.Unit.Integration.Services;

public class ServiceAppointmentServiceSqliteIntegrationTests
{
    [Fact]
    public async Task RequestRescheduleAsync_ShouldPersistProposal_WhenRulesAllowWindow()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var client = CreateUser(UserRole.Client, "cliente.reschedule.int@teste.com");
            var provider = CreateUser(UserRole.Provider, "prestador.reschedule.int@teste.com");
            var request = CreateRequest(client.Id, ServiceCategory.Electrical, "Troca de tomada");
            request.Status = ServiceRequestStatus.Scheduled;

            var currentWindowStartUtc = NextUtcAtHour(10);
            var currentWindowEndUtc = currentWindowStartUtc.AddHours(1);
            var proposedWindowStartUtc = currentWindowStartUtc.AddHours(2);
            var proposedWindowEndUtc = proposedWindowStartUtc.AddHours(1);

            context.Users.AddRange(client, provider);
            context.ServiceRequests.Add(request);
            context.ProviderAvailabilityRules.Add(new ProviderAvailabilityRule
            {
                ProviderId = provider.Id,
                DayOfWeek = proposedWindowStartUtc.DayOfWeek,
                StartTime = TimeSpan.FromHours(8),
                EndTime = TimeSpan.FromHours(22),
                SlotDurationMinutes = 30,
                IsActive = true
            });

            context.ServiceAppointments.Add(new ServiceAppointment
            {
                ServiceRequestId = request.Id,
                ClientId = client.Id,
                ProviderId = provider.Id,
                WindowStartUtc = currentWindowStartUtc,
                WindowEndUtc = currentWindowEndUtc,
                Status = ServiceAppointmentStatus.Confirmed
            });

            await context.SaveChangesAsync();

            var appointmentId = context.ServiceAppointments.Single().Id;
            var service = BuildService(context);
            var result = await service.RequestRescheduleAsync(
                client.Id,
                UserRole.Client.ToString(),
                appointmentId,
                new RequestServiceAppointmentRescheduleDto(
                    proposedWindowStartUtc,
                    proposedWindowEndUtc,
                    "Preciso reagendar por compromisso"));

            Assert.True(result.Success);
            var loaded = await context.ServiceAppointments.FindAsync(appointmentId);
            Assert.NotNull(loaded);
            Assert.Equal(ServiceAppointmentStatus.RescheduleRequestedByClient, loaded!.Status);
            Assert.Equal(proposedWindowStartUtc, loaded.ProposedWindowStartUtc);
            Assert.Equal(proposedWindowEndUtc, loaded.ProposedWindowEndUtc);
            Assert.Equal(ServiceAppointmentActorRole.Client, loaded.RescheduleRequestedByRole);
        }
    }

    [Fact]
    public async Task RespondRescheduleAsync_ShouldApplyProposedWindow_WhenAcceptedByCounterparty()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var client = CreateUser(UserRole.Client, "cliente.respond.int@teste.com");
            var provider = CreateUser(UserRole.Provider, "prestador.respond.int@teste.com");
            var request = CreateRequest(client.Id, ServiceCategory.Plumbing, "Conserto de vazamento");
            request.Status = ServiceRequestStatus.Scheduled;

            var currentWindowStartUtc = NextUtcAtHour(10);
            var currentWindowEndUtc = currentWindowStartUtc.AddHours(1);
            var proposedWindowStartUtc = currentWindowStartUtc.AddHours(3);
            var proposedWindowEndUtc = proposedWindowStartUtc.AddHours(1);

            context.Users.AddRange(client, provider);
            context.ServiceRequests.Add(request);
            context.ProviderAvailabilityRules.Add(new ProviderAvailabilityRule
            {
                ProviderId = provider.Id,
                DayOfWeek = proposedWindowStartUtc.DayOfWeek,
                StartTime = TimeSpan.FromHours(8),
                EndTime = TimeSpan.FromHours(22),
                SlotDurationMinutes = 30,
                IsActive = true
            });

            context.ServiceAppointments.Add(new ServiceAppointment
            {
                ServiceRequestId = request.Id,
                ClientId = client.Id,
                ProviderId = provider.Id,
                WindowStartUtc = currentWindowStartUtc,
                WindowEndUtc = currentWindowEndUtc,
                Status = ServiceAppointmentStatus.RescheduleRequestedByClient,
                ProposedWindowStartUtc = proposedWindowStartUtc,
                ProposedWindowEndUtc = proposedWindowEndUtc,
                RescheduleRequestedAtUtc = DateTime.UtcNow,
                RescheduleRequestedByRole = ServiceAppointmentActorRole.Client,
                RescheduleRequestReason = "Troca de horario"
            });

            await context.SaveChangesAsync();

            var appointmentId = context.ServiceAppointments.Single().Id;
            var service = BuildService(context);
            var result = await service.RespondRescheduleAsync(
                provider.Id,
                UserRole.Provider.ToString(),
                appointmentId,
                new RespondServiceAppointmentRescheduleRequestDto(true));

            Assert.True(result.Success);
            var loaded = await context.ServiceAppointments.FindAsync(appointmentId);
            Assert.NotNull(loaded);
            Assert.Equal(ServiceAppointmentStatus.RescheduleConfirmed, loaded!.Status);
            Assert.Equal(proposedWindowStartUtc, loaded.WindowStartUtc);
            Assert.Equal(proposedWindowEndUtc, loaded.WindowEndUtc);
            Assert.Null(loaded.ProposedWindowStartUtc);
            Assert.Null(loaded.ProposedWindowEndUtc);
            Assert.Null(loaded.RescheduleRequestedByRole);
        }
    }

    [Fact]
    public async Task CancelAsync_ShouldReturnPolicyViolation_WhenWindowIsTooClose()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var client = CreateUser(UserRole.Client, "cliente.cancel.violation@teste.com");
            var provider = CreateUser(UserRole.Provider, "prestador.cancel.violation@teste.com");
            var request = CreateRequest(client.Id, ServiceCategory.Cleaning, "Limpeza pos-obra");
            request.Status = ServiceRequestStatus.Scheduled;

            context.Users.AddRange(client, provider);
            context.ServiceRequests.Add(request);
            context.ServiceAppointments.Add(new ServiceAppointment
            {
                ServiceRequestId = request.Id,
                ClientId = client.Id,
                ProviderId = provider.Id,
                WindowStartUtc = DateTime.UtcNow.AddMinutes(50),
                WindowEndUtc = DateTime.UtcNow.AddHours(2),
                Status = ServiceAppointmentStatus.Confirmed
            });

            await context.SaveChangesAsync();

            var appointmentId = context.ServiceAppointments.Single().Id;
            var service = BuildService(context);
            var result = await service.CancelAsync(
                client.Id,
                UserRole.Client.ToString(),
                appointmentId,
                new CancelServiceAppointmentRequestDto("Emergencia familiar"));

            Assert.False(result.Success);
            Assert.Equal("policy_violation", result.ErrorCode);

            var loaded = await context.ServiceAppointments.FindAsync(appointmentId);
            Assert.NotNull(loaded);
            Assert.Equal(ServiceAppointmentStatus.Confirmed, loaded!.Status);
        }
    }

    private static ServiceAppointmentService BuildService(ConsertaPraMimDbContext context)
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
            new NoOpNotificationService(),
            configuration);
    }

    private static User CreateUser(UserRole role, string email)
    {
        return new User
        {
            Name = role == UserRole.Provider ? "Prestador Integracao Agenda" : "Cliente Integracao Agenda",
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
            AddressStreet = "Rua Integracao",
            AddressCity = "Praia Grande",
            AddressZip = "11704150",
            Latitude = -24.01,
            Longitude = -46.41
        };
    }

    private sealed class NoOpNotificationService : INotificationService
    {
        public Task SendNotificationAsync(string recipient, string subject, string message, string? actionUrl = null)
        {
            return Task.CompletedTask;
        }
    }

    private static DateTime NextUtcAtHour(int hourUtc)
    {
        var date = DateTime.UtcNow.Date.AddDays(1);
        return DateTime.SpecifyKind(date.AddHours(hourUtc), DateTimeKind.Utc);
    }
}
