using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminGrowthServiceReactivationTests
{
    /// <summary>
    /// Cenario: growth segmenta prestadores por inatividade e devolve preview operacional.
    /// Passos: base com prestadores ativos, ultimos logins e ultimas propostas em periodos distintos.
    /// Resultado esperado: snapshot com segmentos de inatividade e preview ordenado por maior risco.
    /// </summary>
    [Fact(DisplayName = "Admin growth service | Reativacao | Deve segmentar prestadores por inatividade")]
    public async Task GetProviderReactivationSegmentsAsync_ShouldSegmentInactiveProviders()
    {
        var asOfUtc = new DateTime(2026, 2, 24, 12, 0, 0, DateTimeKind.Utc);

        var warmProvider = new User
        {
            Id = Guid.NewGuid(),
            Name = "Prestador Warm",
            Email = "warm@teste.com",
            Role = UserRole.Provider,
            IsActive = true,
            CreatedAt = asOfUtc.AddDays(-120),
            ProviderProfile = new ProviderProfile
            {
                BaseZipCode = "01311-000",
                Categories = new List<ServiceCategory> { ServiceCategory.Electrical }
            }
        };

        var dormantProvider = new User
        {
            Id = Guid.NewGuid(),
            Name = "Prestador Dormant",
            Email = "dormant@teste.com",
            Role = UserRole.Provider,
            IsActive = true,
            CreatedAt = asOfUtc.AddDays(-200),
            ProviderProfile = new ProviderProfile
            {
                BaseZipCode = "20031-170",
                Categories = new List<ServiceCategory> { ServiceCategory.Plumbing }
            }
        };

        var activeProvider = new User
        {
            Id = Guid.NewGuid(),
            Name = "Prestador Ativo",
            Email = "active@teste.com",
            Role = UserRole.Provider,
            IsActive = true,
            CreatedAt = asOfUtc.AddDays(-60),
            ProviderProfile = new ProviderProfile
            {
                BaseZipCode = "30110-041",
                Categories = new List<ServiceCategory> { ServiceCategory.Cleaning }
            }
        };

        var userRepositoryMock = new Mock<IUserRepository>();
        userRepositoryMock
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(new List<User> { warmProvider, dormantProvider, activeProvider });

        var requestRepositoryMock = new Mock<IServiceRequestRepository>();
        var proposalRepositoryMock = new Mock<IProposalRepository>();
        proposalRepositoryMock
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(new List<Proposal>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ProviderId = warmProvider.Id,
                    RequestId = Guid.NewGuid(),
                    CreatedAt = asOfUtc.AddDays(-10)
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    ProviderId = activeProvider.Id,
                    RequestId = Guid.NewGuid(),
                    CreatedAt = asOfUtc.AddDays(-2)
                }
            });

        var auditLogRepositoryMock = new Mock<IAdminAuditLogRepository>();
        auditLogRepositoryMock
            .Setup(x => x.GetByTargetAndPeriodAsync(
                "UserAuth",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                null,
                null,
                "user_login",
                20000))
            .ReturnsAsync(new List<AdminAuditLog>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ActorUserId = dormantProvider.Id,
                    ActorEmail = dormantProvider.Email,
                    Action = "user_login",
                    TargetType = "UserAuth",
                    CreatedAt = asOfUtc.AddDays(-45)
                }
            });

        var service = new AdminGrowthService(
            userRepositoryMock.Object,
            requestRepositoryMock.Object,
            proposalRepositoryMock.Object,
            auditLogRepositoryMock.Object);

        var result = await service.GetProviderReactivationSegmentsAsync(
            new AdminProviderReactivationSegmentsQueryDto(
                AsOfUtc: asOfUtc,
                WarmFromDays: 7,
                ColdFromDays: 15,
                DormantFromDays: 31,
                HibernatedFromDays: 61,
                PreviewTake: 10));

        Assert.Equal(3, result.TotalProviders);
        Assert.Equal(1, result.ActiveProviders);
        Assert.Equal(2, result.InactiveProviders);
        Assert.Contains(result.Segments, segment => segment.SegmentCode == "warm" && segment.Providers == 1);
        Assert.Contains(result.Segments, segment => segment.SegmentCode == "dormant" && segment.Providers == 1);
        Assert.Equal(2, result.Preview.Count);
        Assert.Equal("Prestador Dormant", result.Preview.First().ProviderName);
    }
}
