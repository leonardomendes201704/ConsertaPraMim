using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Infrastructure.Data;
using ConsertaPraMim.Infrastructure.Repositories;
using ConsertaPraMim.Tests.Unit.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ConsertaPraMim.Tests.Unit.Integration.Services;

public class ServiceAppointmentServiceSqliteIntegrationTests
{
    [Fact(DisplayName = "Servico appointment servico sqlite integracao | Requisicao reschedule | Deve persistir proposal quando rules allow window")]
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

    [Fact(DisplayName = "Servico appointment servico sqlite integracao | Respond reschedule | Deve apply proposed window quando accepted por counterparty")]
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

    [Fact(DisplayName = "Servico appointment servico sqlite integracao | Cancelar | Deve retornar politica violation quando window too fechar")]
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

    [Fact(DisplayName = "Servico appointment servico sqlite integracao | Marcar arrived | Deve idempotent quando called twice for same appointment")]
    public async Task MarkArrivedAsync_ShouldBeIdempotent_WhenCalledTwiceForSameAppointment()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var client = CreateUser(UserRole.Client, "cliente.arrive.idempotent@teste.com");
            var provider = CreateUser(UserRole.Provider, "prestador.arrive.idempotent@teste.com");
            var request = CreateRequest(client.Id, ServiceCategory.Electrical, "Queda de energia");
            request.Status = ServiceRequestStatus.Scheduled;

            context.Users.AddRange(client, provider);
            context.ServiceRequests.Add(request);
            context.ServiceAppointments.Add(new ServiceAppointment
            {
                ServiceRequestId = request.Id,
                ClientId = client.Id,
                ProviderId = provider.Id,
                WindowStartUtc = DateTime.UtcNow.AddHours(3),
                WindowEndUtc = DateTime.UtcNow.AddHours(4),
                Status = ServiceAppointmentStatus.Confirmed
            });

            await context.SaveChangesAsync();

            var appointmentId = context.ServiceAppointments.Single().Id;
            var service = BuildService(context);

            var first = await service.MarkArrivedAsync(
                provider.Id,
                UserRole.Provider.ToString(),
                appointmentId,
                new MarkServiceAppointmentArrivalRequestDto(-24.01, -46.41, 9.8));

            var second = await service.MarkArrivedAsync(
                provider.Id,
                UserRole.Provider.ToString(),
                appointmentId,
                new MarkServiceAppointmentArrivalRequestDto(-24.01, -46.41, 9.8));

            Assert.True(first.Success);
            Assert.False(second.Success);
            Assert.Equal("duplicate_checkin", second.ErrorCode);

            var loaded = await context.ServiceAppointments.FindAsync(appointmentId);
            Assert.NotNull(loaded);
            Assert.Equal(ServiceAppointmentStatus.Arrived, loaded!.Status);
            Assert.NotNull(loaded.ArrivedAtUtc);
        }
    }

    [Fact(DisplayName = "Servico appointment servico sqlite integracao | Marcar arrived | Deve allow only one sucesso quando requisicoes concurrent")]
    public async Task MarkArrivedAsync_ShouldAllowOnlyOneSuccess_WhenRequestsAreConcurrent()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"cpm-arrive-{Guid.NewGuid():N}.db");
        try
        {
            var providerId = Guid.NewGuid();
            var clientId = Guid.NewGuid();
            var appointmentId = Guid.Empty;

            await using (var setupContext = CreateFileSqliteContext(dbPath))
            {
                var client = CreateUser(UserRole.Client, "cliente.arrive.concurrent@teste.com");
                client.Id = clientId;
                var provider = CreateUser(UserRole.Provider, "prestador.arrive.concurrent@teste.com");
                provider.Id = providerId;
                var request = CreateRequest(client.Id, ServiceCategory.Plumbing, "Vazamento no banheiro");
                request.Status = ServiceRequestStatus.Scheduled;

                setupContext.Users.AddRange(client, provider);
                setupContext.ServiceRequests.Add(request);
                setupContext.ServiceAppointments.Add(new ServiceAppointment
                {
                    ServiceRequestId = request.Id,
                    ClientId = client.Id,
                    ProviderId = provider.Id,
                    WindowStartUtc = DateTime.UtcNow.AddHours(2),
                    WindowEndUtc = DateTime.UtcNow.AddHours(3),
                    Status = ServiceAppointmentStatus.Confirmed
                });

                await setupContext.SaveChangesAsync();
                appointmentId = setupContext.ServiceAppointments.Single().Id;
            }

            await using var contextA = CreateFileSqliteContext(dbPath);
            await using var contextB = CreateFileSqliteContext(dbPath);
            var serviceA = BuildService(contextA);
            var serviceB = BuildService(contextB);

            var markArrivalA = serviceA.MarkArrivedAsync(
                providerId,
                UserRole.Provider.ToString(),
                appointmentId,
                new MarkServiceAppointmentArrivalRequestDto(-24.011, -46.412, 7.2));

            var markArrivalB = serviceB.MarkArrivedAsync(
                providerId,
                UserRole.Provider.ToString(),
                appointmentId,
                new MarkServiceAppointmentArrivalRequestDto(-24.011, -46.412, 7.2));

            var results = await Task.WhenAll(markArrivalA, markArrivalB);

            Assert.Single(results, r => r.Success);
            Assert.Single(results, r => !r.Success && r.ErrorCode == "duplicate_checkin");

            await using var verifyContext = CreateFileSqliteContext(dbPath);
            var loaded = await verifyContext.ServiceAppointments.FindAsync(appointmentId);
            Assert.NotNull(loaded);
            Assert.Equal(ServiceAppointmentStatus.Arrived, loaded!.Status);
            Assert.NotNull(loaded.ArrivedAtUtc);
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                try
                {
                    File.Delete(dbPath);
                }
                catch (IOException)
                {
                    // Ignore transient sqlite file lock in CI/local test teardown.
                }
            }
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

    private static ConsertaPraMimDbContext CreateFileSqliteContext(string dbPath)
    {
        var options = new DbContextOptionsBuilder<ConsertaPraMimDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        var context = new ConsertaPraMimDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
