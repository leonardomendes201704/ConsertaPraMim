using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Infrastructure.Repositories;
using ConsertaPraMim.Tests.Unit.Integration.Infrastructure;

namespace ConsertaPraMim.Tests.Unit.Integration.Repositories;

public class UserRepositoryInMemoryIntegrationTests
{
    [Fact]
    public async Task GetByEmailAsync_ShouldReturnProviderWithProfile()
    {
        await using var context = InfrastructureTestDbContextFactory.CreateInMemoryContext();
        var repository = new UserRepository(context);

        var provider = new User
        {
            Name = "Prestador 01",
            Email = "provider.profile@teste.com",
            PasswordHash = "hash",
            Phone = "11911112222",
            Role = UserRole.Provider
        };

        provider.ProviderProfile = new ProviderProfile
        {
            UserId = provider.Id,
            RadiusKm = 50,
            BaseZipCode = "11704150",
            Categories = new List<ServiceCategory> { ServiceCategory.Electrical, ServiceCategory.Plumbing }
        };

        context.Users.Add(provider);
        await context.SaveChangesAsync();

        var loaded = await repository.GetByEmailAsync(provider.Email);

        Assert.NotNull(loaded);
        Assert.NotNull(loaded!.ProviderProfile);
        Assert.Equal("11704150", loaded.ProviderProfile!.BaseZipCode);
        Assert.Equal(2, loaded.ProviderProfile.Categories.Count);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnUsersOrderedByCreatedAtDescending()
    {
        await using var context = InfrastructureTestDbContextFactory.CreateInMemoryContext();
        var repository = new UserRepository(context);

        var oldUser = new User
        {
            Name = "Usuario Antigo",
            Email = "old.user@teste.com",
            PasswordHash = "hash",
            Phone = "11910000000",
            Role = UserRole.Client,
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };

        var recentUser = new User
        {
            Name = "Usuario Novo",
            Email = "new.user@teste.com",
            PasswordHash = "hash",
            Phone = "11920000000",
            Role = UserRole.Client,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.AddRange(oldUser, recentUser);
        await context.SaveChangesAsync();

        var users = (await repository.GetAllAsync()).ToList();

        Assert.Equal(2, users.Count);
        Assert.Equal(recentUser.Id, users[0].Id);
    }
}
