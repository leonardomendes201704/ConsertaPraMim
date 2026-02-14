using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Infrastructure.Repositories;
using ConsertaPraMim.Tests.Unit.Integration.Infrastructure;

namespace ConsertaPraMim.Tests.Unit.Integration.Repositories;

public class ServiceAppointmentRepositorySqliteIntegrationTests
{
    [Fact]
    public async Task AddAsync_AndGetByRequestIdAsync_ShouldPersistAppointmentAndHistory()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var client = CreateUser(UserRole.Client, "cliente.agenda@teste.com");
            var provider = CreateUser(UserRole.Provider, "prestador.agenda@teste.com");
            var request = CreateRequest(client.Id, ServiceCategory.Electrical, "Trocar lampada");

            context.Users.AddRange(client, provider);
            context.ServiceRequests.Add(request);
            await context.SaveChangesAsync();

            var repository = new ServiceAppointmentRepository(context);
            var appointment = new ServiceAppointment
            {
                ServiceRequestId = request.Id,
                ClientId = client.Id,
                ProviderId = provider.Id,
                WindowStartUtc = DateTime.UtcNow.AddHours(6),
                WindowEndUtc = DateTime.UtcNow.AddHours(8),
                ExpiresAtUtc = DateTime.UtcNow.AddHours(2),
                Status = ServiceAppointmentStatus.PendingProviderConfirmation
            };

            await repository.AddAsync(appointment);
            await repository.AddHistoryAsync(new ServiceAppointmentHistory
            {
                ServiceAppointmentId = appointment.Id,
                PreviousStatus = null,
                NewStatus = ServiceAppointmentStatus.PendingProviderConfirmation,
                ActorUserId = client.Id,
                ActorRole = ServiceAppointmentActorRole.Client,
                Reason = "Cliente solicitou agendamento"
            });

            var loaded = await repository.GetByRequestIdAsync(request.Id);

            Assert.NotNull(loaded);
            Assert.Equal(appointment.Id, loaded!.Id);
            Assert.Equal(ServiceAppointmentStatus.PendingProviderConfirmation, loaded.Status);
            Assert.Single(loaded.History);
            Assert.Equal(ServiceAppointmentActorRole.Client, loaded.History.First().ActorRole);
        }
    }

    [Fact]
    public async Task GetProviderAppointmentsByStatusesInRangeAsync_ShouldFilterByOverlapAndStatus()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var client = CreateUser(UserRole.Client, "cliente.range@teste.com");
            var provider = CreateUser(UserRole.Provider, "prestador.range@teste.com");

            var request1 = CreateRequest(client.Id, ServiceCategory.Plumbing, "Vazamento banheiro");
            var request2 = CreateRequest(client.Id, ServiceCategory.Plumbing, "Troca torneira");
            var request3 = CreateRequest(client.Id, ServiceCategory.Plumbing, "Desentupir pia");

            context.Users.AddRange(client, provider);
            context.ServiceRequests.AddRange(request1, request2, request3);
            await context.SaveChangesAsync();

            var now = DateTime.UtcNow;
            var repository = new ServiceAppointmentRepository(context);

            await repository.AddAsync(new ServiceAppointment
            {
                ServiceRequestId = request1.Id,
                ClientId = client.Id,
                ProviderId = provider.Id,
                WindowStartUtc = now.AddHours(2),
                WindowEndUtc = now.AddHours(3),
                Status = ServiceAppointmentStatus.Confirmed
            });

            await repository.AddAsync(new ServiceAppointment
            {
                ServiceRequestId = request2.Id,
                ClientId = client.Id,
                ProviderId = provider.Id,
                WindowStartUtc = now.AddHours(2),
                WindowEndUtc = now.AddHours(4),
                Status = ServiceAppointmentStatus.CancelledByProvider
            });

            await repository.AddAsync(new ServiceAppointment
            {
                ServiceRequestId = request3.Id,
                ClientId = client.Id,
                ProviderId = provider.Id,
                WindowStartUtc = now.AddDays(2),
                WindowEndUtc = now.AddDays(2).AddHours(1),
                Status = ServiceAppointmentStatus.Confirmed
            });

            var result = await repository.GetProviderAppointmentsByStatusesInRangeAsync(
                provider.Id,
                now.AddHours(1),
                now.AddHours(5),
                new[] { ServiceAppointmentStatus.Confirmed });

            Assert.Single(result);
            Assert.Equal(request1.Id, result[0].ServiceRequestId);
        }
    }

    private static User CreateUser(UserRole role, string email)
    {
        return new User
        {
            Name = role == UserRole.Provider ? "Prestador Agenda" : "Cliente Agenda",
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
            AddressStreet = "Rua Agenda",
            AddressCity = "Praia Grande",
            AddressZip = "11704150",
            Latitude = -24.01,
            Longitude = -46.41
        };
    }
}
