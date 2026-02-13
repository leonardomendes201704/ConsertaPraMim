using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class ProviderOnboardingServiceTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly ProviderOnboardingService _service;

    public ProviderOnboardingServiceTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _service = new ProviderOnboardingService(_userRepositoryMock.Object);
    }

    [Fact]
    public async Task SavePlanAsync_ShouldReturnFalse_WhenPlanIsNotAllowed()
    {
        var userId = Guid.NewGuid();
        var user = BuildProvider(userId);
        _userRepositoryMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

        var result = await _service.SavePlanAsync(userId, ProviderPlan.Trial);

        Assert.False(result);
        _userRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task SavePlanAsync_ShouldPersistPlanAndTimestamp_WhenPlanIsAllowed()
    {
        var userId = Guid.NewGuid();
        var user = BuildProvider(userId);
        _userRepositoryMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

        var result = await _service.SavePlanAsync(userId, ProviderPlan.Silver);

        Assert.True(result);
        Assert.Equal(ProviderPlan.Silver, user.ProviderProfile!.Plan);
        Assert.NotNull(user.ProviderProfile.PlanSelectedAt);
        _userRepositoryMock.Verify(r => r.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task CompleteAsync_ShouldReturnFailure_WhenRequiredDocumentsAreMissing()
    {
        var userId = Guid.NewGuid();
        var user = BuildProvider(userId);
        user.ProviderProfile = new ProviderProfile
        {
            UserId = userId,
            IsOnboardingCompleted = false,
            OnboardingStatus = ProviderOnboardingStatus.PendingDocumentation,
            Plan = ProviderPlan.Bronze,
            PlanSelectedAt = DateTime.UtcNow,
            OnboardingDocuments =
            {
                new ProviderOnboardingDocument
                {
                    DocumentType = ProviderDocumentType.IdentityDocument,
                    FileName = "rg.pdf",
                    MimeType = "application/pdf",
                    SizeBytes = 100,
                    FileUrl = "/uploads/provider-docs/rg.pdf"
                }
            }
        };

        _userRepositoryMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

        var result = await _service.CompleteAsync(userId);

        Assert.False(result.Success);
        Assert.Contains("documentos obrigatorios", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        _userRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task CompleteAsync_ShouldCompleteOnboarding_WhenRulesAreSatisfied()
    {
        var userId = Guid.NewGuid();
        var user = BuildProvider(userId);
        user.ProviderProfile = new ProviderProfile
        {
            UserId = userId,
            IsOnboardingCompleted = false,
            OnboardingStatus = ProviderOnboardingStatus.PendingDocumentation,
            Plan = ProviderPlan.Gold,
            PlanSelectedAt = DateTime.UtcNow,
            OnboardingDocuments =
            {
                new ProviderOnboardingDocument
                {
                    DocumentType = ProviderDocumentType.IdentityDocument,
                    FileName = "rg.pdf",
                    MimeType = "application/pdf",
                    SizeBytes = 100,
                    FileUrl = "/uploads/provider-docs/rg.pdf"
                },
                new ProviderOnboardingDocument
                {
                    DocumentType = ProviderDocumentType.SelfieWithDocument,
                    FileName = "selfie.jpg",
                    MimeType = "image/jpeg",
                    SizeBytes = 100,
                    FileUrl = "/uploads/provider-docs/selfie.jpg"
                }
            }
        };

        _userRepositoryMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

        var result = await _service.CompleteAsync(userId);

        Assert.True(result.Success);
        Assert.True(user.ProviderProfile.IsOnboardingCompleted);
        Assert.Equal(ProviderOnboardingStatus.PendingApproval, user.ProviderProfile.OnboardingStatus);
        Assert.NotNull(user.ProviderProfile.OnboardingCompletedAt);
        _userRepositoryMock.Verify(r => r.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task AddDocumentAsync_ShouldReturnNull_WhenDocumentLimitIsReached()
    {
        var userId = Guid.NewGuid();
        var user = BuildProvider(userId);
        user.ProviderProfile = new ProviderProfile
        {
            UserId = userId,
            IsOnboardingCompleted = false,
            OnboardingStatus = ProviderOnboardingStatus.PendingDocumentation,
            Plan = ProviderPlan.Bronze
        };

        for (var i = 0; i < 6; i++)
        {
            user.ProviderProfile.OnboardingDocuments.Add(new ProviderOnboardingDocument
            {
                DocumentType = ProviderDocumentType.AddressProof,
                FileName = $"doc-{i}.pdf",
                MimeType = "application/pdf",
                SizeBytes = 100,
                FileUrl = $"/uploads/provider-docs/doc-{i}.pdf"
            });
        }

        _userRepositoryMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

        var result = await _service.AddDocumentAsync(userId, new AddProviderOnboardingDocumentDto(
            ProviderDocumentType.IdentityDocument,
            "rg.pdf",
            "application/pdf",
            100,
            "/uploads/provider-docs/rg.pdf",
            "ABC"));

        Assert.Null(result);
        _userRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    private static User BuildProvider(Guid userId)
    {
        return new User
        {
            Id = userId,
            Name = "Prestador Teste",
            Email = "prestador@test.com",
            Phone = "11999999999",
            Role = UserRole.Provider,
            ProviderProfile = new ProviderProfile
            {
                UserId = userId,
                IsOnboardingCompleted = false,
                OnboardingStatus = ProviderOnboardingStatus.PendingDocumentation,
                Plan = ProviderPlan.Bronze
            }
        };
    }
}
