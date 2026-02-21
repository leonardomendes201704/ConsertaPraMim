using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Infrastructure.Repositories;
using ConsertaPraMim.Tests.Unit.Integration.Infrastructure;

namespace ConsertaPraMim.Tests.Unit.Integration.Repositories;

public class UserRepositorySqliteOnboardingIntegrationTests
{
    [Fact(DisplayName = "Usuario repository sqlite onboarding integracao | Atualizar | Deve persistir onboarding graph quando profile criado during tracked atualizar")]
    public async Task UpdateAsync_ShouldPersistOnboardingGraph_WhenProfileIsCreatedDuringTrackedUpdate()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        await using var dbContext = context;
        using var sqliteConnection = connection;
        var repository = new UserRepository(dbContext);

        var user = new User
        {
            Name = "Prestador Sem Perfil",
            Email = "provider.no.profile@teste.com",
            PasswordHash = "hash",
            Phone = "11912345678",
            Role = UserRole.Provider
        };

        await repository.AddAsync(user);

        var trackedUser = await repository.GetByIdAsync(user.Id);
        Assert.NotNull(trackedUser);
        Assert.Null(trackedUser!.ProviderProfile);

        trackedUser.ProviderProfile = new ProviderProfile
        {
            UserId = trackedUser.Id,
            Plan = ProviderPlan.Bronze,
            IsOnboardingCompleted = false,
            OnboardingStatus = ProviderOnboardingStatus.PendingDocumentation,
            OnboardingStartedAt = DateTime.UtcNow
        };

        trackedUser.ProviderProfile.OnboardingDocuments.Add(new ProviderOnboardingDocument
        {
            DocumentType = ProviderDocumentType.IdentityDocument,
            Status = ProviderDocumentStatus.Pending,
            FileName = "rg.pdf",
            MimeType = "application/pdf",
            SizeBytes = 2048,
            FileUrl = "/uploads/provider-docs/rg.pdf"
        });

        await repository.UpdateAsync(trackedUser);

        var reloadedUser = await repository.GetByIdAsync(user.Id);
        Assert.NotNull(reloadedUser);
        Assert.NotNull(reloadedUser!.ProviderProfile);
        Assert.Equal(ProviderPlan.Bronze, reloadedUser.ProviderProfile!.Plan);
        Assert.Single(reloadedUser.ProviderProfile.OnboardingDocuments);
        Assert.Equal(
            ProviderDocumentType.IdentityDocument,
            reloadedUser.ProviderProfile.OnboardingDocuments.First().DocumentType);
    }
}
