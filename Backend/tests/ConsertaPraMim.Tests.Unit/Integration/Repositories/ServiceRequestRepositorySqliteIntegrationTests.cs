using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Infrastructure.Repositories;
using ConsertaPraMim.Tests.Unit.Integration.Infrastructure;

namespace ConsertaPraMim.Tests.Unit.Integration.Repositories;

public class ServiceRequestRepositorySqliteIntegrationTests
{
    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Servico requisicao repository sqlite integracao | Obter matching for prestador | Deve retornar only requisicoes within radius category e status.
    /// </summary>
    [Fact(DisplayName = "Servico requisicao repository sqlite integracao | Obter matching for prestador | Deve retornar only requisicoes within radius category e status")]
    public async Task GetMatchingForProviderAsync_ShouldReturnOnlyRequestsWithinRadiusCategoryAndStatus()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var client = CreateClient("client1@teste.com");

            var matching = CreateRequest(
                client.Id,
                ServiceCategory.Electrical,
                ServiceRequestStatus.Created,
                "Consertar tomada da cozinha",
                -23.965,
                -46.33);

            var wrongStatus = CreateRequest(
                client.Id,
                ServiceCategory.Electrical,
                ServiceRequestStatus.Completed,
                "Consertar tomada da sala",
                -23.965,
                -46.33);

            var wrongCategory = CreateRequest(
                client.Id,
                ServiceCategory.Plumbing,
                ServiceRequestStatus.Created,
                "Vazamento no banheiro",
                -23.965,
                -46.33);

            var outOfRadius = CreateRequest(
                client.Id,
                ServiceCategory.Electrical,
                ServiceRequestStatus.Created,
                "Consertar disjuntor",
                -20.0,
                -43.0);

            context.Users.Add(client);
            context.ServiceRequests.AddRange(matching, wrongStatus, wrongCategory, outOfRadius);
            await context.SaveChangesAsync();

            var repository = new ServiceRequestRepository(context);
            var result = (await repository.GetMatchingForProviderAsync(
                    -23.965,
                    -46.33,
                    30,
                    new List<ServiceCategory> { ServiceCategory.Electrical },
                    "tomada"))
                .ToList();

            Assert.Single(result);
            Assert.Equal(matching.Id, result[0].Id);
        }
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Servico requisicao repository sqlite integracao | Obter por id | Deve load cliente e proposals.
    /// </summary>
    [Fact(DisplayName = "Servico requisicao repository sqlite integracao | Obter por id | Deve load cliente e proposals")]
    public async Task GetByIdAsync_ShouldLoadClientAndProposals()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var client = CreateClient("client2@teste.com");
            var provider = CreateProvider("provider1@teste.com");
            var request = CreateRequest(
                client.Id,
                ServiceCategory.Electrical,
                ServiceRequestStatus.Created,
                "Trocar tomada",
                -23.97,
                -46.31);

            var proposal = new Proposal
            {
                RequestId = request.Id,
                ProviderId = provider.Id,
                Message = "Posso ir hoje"
            };

            context.Users.AddRange(client, provider);
            context.ServiceRequests.Add(request);
            context.Proposals.Add(proposal);
            await context.SaveChangesAsync();

            var repository = new ServiceRequestRepository(context);
            var loaded = await repository.GetByIdAsync(request.Id);

            Assert.NotNull(loaded);
            Assert.NotNull(loaded!.Client);
            Assert.Single(loaded.Proposals);
            Assert.Equal(provider.Id, loaded.Proposals.First().ProviderId);
        }
    }

    private static User CreateClient(string email)
    {
        return new User
        {
            Name = "Cliente",
            Email = email,
            PasswordHash = "hash",
            Phone = "11999999999",
            Role = UserRole.Client
        };
    }

    private static User CreateProvider(string email)
    {
        return new User
        {
            Name = "Prestador",
            Email = email,
            PasswordHash = "hash",
            Phone = "11888888888",
            Role = UserRole.Provider
        };
    }

    private static ServiceRequest CreateRequest(
        Guid clientId,
        ServiceCategory category,
        ServiceRequestStatus status,
        string description,
        double latitude,
        double longitude)
    {
        return new ServiceRequest
        {
            ClientId = clientId,
            Category = category,
            Status = status,
            Description = description,
            AddressStreet = "Rua Teste",
            AddressCity = "Praia Grande",
            AddressZip = "11704150",
            Latitude = latitude,
            Longitude = longitude
        };
    }
}
