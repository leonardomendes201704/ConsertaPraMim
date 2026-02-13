using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Infrastructure.Repositories;
using ConsertaPraMim.Tests.Unit.Integration.Infrastructure;

namespace ConsertaPraMim.Tests.Unit.Integration.Repositories;

public class ProposalRepositoryInMemoryIntegrationTests
{
    [Fact]
    public async Task GetByProviderIdAsync_ShouldReturnProposalsOrderedByCreatedAtDesc()
    {
        await using var context = InfrastructureTestDbContextFactory.CreateInMemoryContext();
        var repository = new ProposalRepository(context);

        var client = CreateClient("client.proposal@teste.com");
        var provider = CreateProvider("provider.proposal@teste.com");
        var request = CreateRequest(client.Id, "Instalacao de chuveiro");

        var oldProposal = new Proposal
        {
            RequestId = request.Id,
            ProviderId = provider.Id,
            Message = "Consigo na segunda",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };

        var recentProposal = new Proposal
        {
            RequestId = request.Id,
            ProviderId = provider.Id,
            Message = "Consigo hoje",
            CreatedAt = DateTime.UtcNow
        };

        context.Users.AddRange(client, provider);
        context.ServiceRequests.Add(request);
        context.Proposals.AddRange(oldProposal, recentProposal);
        await context.SaveChangesAsync();

        var result = (await repository.GetByProviderIdAsync(provider.Id)).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal(recentProposal.Id, result[0].Id);
        Assert.NotNull(result[0].Provider);
        Assert.NotNull(result[0].Request);
    }

    [Fact]
    public async Task UpdateAsync_ShouldPersistChanges()
    {
        await using var context = InfrastructureTestDbContextFactory.CreateInMemoryContext();
        var repository = new ProposalRepository(context);

        var client = CreateClient("client.update@teste.com");
        var provider = CreateProvider("provider.update@teste.com");
        var request = CreateRequest(client.Id, "Troca de tomada");
        var proposal = new Proposal
        {
            RequestId = request.Id,
            ProviderId = provider.Id,
            Message = "Posso ir amanha",
            Accepted = false
        };

        context.Users.AddRange(client, provider);
        context.ServiceRequests.Add(request);
        context.Proposals.Add(proposal);
        await context.SaveChangesAsync();

        proposal.Accepted = true;
        await repository.UpdateAsync(proposal);

        var persisted = await context.Proposals.FindAsync(proposal.Id);
        Assert.NotNull(persisted);
        Assert.True(persisted!.Accepted);
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

    private static ServiceRequest CreateRequest(Guid clientId, string description)
    {
        return new ServiceRequest
        {
            ClientId = clientId,
            Category = ServiceCategory.Electrical,
            Status = ServiceRequestStatus.Created,
            Description = description,
            AddressStreet = "Rua Teste",
            AddressCity = "Sao Vicente",
            AddressZip = "11310100",
            Latitude = -23.96,
            Longitude = -46.39
        };
    }
}
