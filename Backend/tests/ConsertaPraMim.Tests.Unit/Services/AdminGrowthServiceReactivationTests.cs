using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
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

    /// <summary>
    /// Cenario: tentativa de rodada bloqueada por cadencia minima.
    /// Passos: ultima campanha registrada ha poucas horas e forceRun desabilitado.
    /// Resultado esperado: rodada nao executa e retorna status skipped_cadence.
    /// </summary>
    [Fact(DisplayName = "Admin growth service | Campanha reativacao | Deve bloquear por cadencia")]
    public async Task RunProviderReactivationCampaignAsync_ShouldSkipWhenCadenceBlocksRun()
    {
        var nowUtc = DateTime.UtcNow;
        var provider = new User
        {
            Id = Guid.NewGuid(),
            Name = "Prestador Cadencia",
            Email = "cadencia@teste.com",
            Role = UserRole.Provider,
            IsActive = true,
            CreatedAt = nowUtc.AddDays(-120),
            ProviderProfile = new ProviderProfile
            {
                BaseZipCode = "01311-000",
                Categories = new List<ServiceCategory> { ServiceCategory.Electrical }
            }
        };

        var userRepositoryMock = new Mock<IUserRepository>();
        userRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<User> { provider });

        var requestRepositoryMock = new Mock<IServiceRequestRepository>();
        var proposalRepositoryMock = new Mock<IProposalRepository>();
        proposalRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Proposal>());

        var auditLogRepositoryMock = new Mock<IAdminAuditLogRepository>();
        auditLogRepositoryMock
            .Setup(x => x.GetByTargetAndPeriodAsync(
                "ProviderReactivationCampaign",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                null,
                null,
                "campaign_run_completed",
                1))
            .ReturnsAsync(new List<AdminAuditLog>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    TargetType = "ProviderReactivationCampaign",
                    Action = "campaign_run_completed",
                    CreatedAt = nowUtc.AddHours(-2)
                }
            });

        auditLogRepositoryMock
            .Setup(x => x.GetByTargetAndPeriodAsync(
                "UserAuth",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                null,
                null,
                "user_login",
                20000))
            .ReturnsAsync(new List<AdminAuditLog>());

        var service = new AdminGrowthService(
            userRepositoryMock.Object,
            requestRepositoryMock.Object,
            proposalRepositoryMock.Object,
            auditLogRepositoryMock.Object);

        var result = await service.RunProviderReactivationCampaignAsync(
            new AdminProviderReactivationCampaignRunRequestDto(
                AsOfUtc: nowUtc,
                CadenceHours: 24,
                MaxRecipients: 100,
                ForceRun: false,
                SegmentCode: null),
            Guid.NewGuid(),
            "growth-admin@teste.com");

        Assert.False(result.Executed);
        Assert.Equal("skipped_cadence", result.Status);
        auditLogRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<AdminAuditLog>()),
            Times.Never);
    }

    /// <summary>
    /// Cenario: rodada liberada por cadencia com prestadores elegiveis.
    /// Passos: sem campanha recente e um prestador inativo no segmento warm.
    /// Resultado esperado: rodada executada e auditoria registrada.
    /// </summary>
    [Fact(DisplayName = "Admin growth service | Campanha reativacao | Deve registrar rodada quando elegivel")]
    public async Task RunProviderReactivationCampaignAsync_ShouldExecuteAndRegisterAudit()
    {
        var nowUtc = DateTime.UtcNow;
        var provider = new User
        {
            Id = Guid.NewGuid(),
            Name = "Prestador Warm",
            Email = "warm-campanha@teste.com",
            Role = UserRole.Provider,
            IsActive = true,
            CreatedAt = nowUtc.AddDays(-90),
            ProviderProfile = new ProviderProfile
            {
                BaseZipCode = "01001-000",
                Categories = new List<ServiceCategory> { ServiceCategory.Electrical }
            }
        };

        var userRepositoryMock = new Mock<IUserRepository>();
        userRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<User> { provider });

        var requestRepositoryMock = new Mock<IServiceRequestRepository>();
        var proposalRepositoryMock = new Mock<IProposalRepository>();
        proposalRepositoryMock
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(new List<Proposal>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ProviderId = provider.Id,
                    RequestId = Guid.NewGuid(),
                    CreatedAt = nowUtc.AddDays(-8)
                }
            });

        var auditLogRepositoryMock = new Mock<IAdminAuditLogRepository>();
        auditLogRepositoryMock
            .Setup(x => x.GetByTargetAndPeriodAsync(
                "ProviderReactivationCampaign",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                null,
                null,
                "campaign_run_completed",
                1))
            .ReturnsAsync(new List<AdminAuditLog>());

        auditLogRepositoryMock
            .Setup(x => x.GetByTargetAndPeriodAsync(
                "UserAuth",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                null,
                null,
                "user_login",
                20000))
            .ReturnsAsync(new List<AdminAuditLog>());

        var service = new AdminGrowthService(
            userRepositoryMock.Object,
            requestRepositoryMock.Object,
            proposalRepositoryMock.Object,
            auditLogRepositoryMock.Object);

        var result = await service.RunProviderReactivationCampaignAsync(
            new AdminProviderReactivationCampaignRunRequestDto(
                AsOfUtc: nowUtc,
                CadenceHours: 24,
                MaxRecipients: 100,
                ForceRun: false,
                SegmentCode: "warm"),
            Guid.NewGuid(),
            "growth-admin@teste.com");

        Assert.True(result.Executed);
        Assert.Equal("completed", result.Status);
        Assert.True(result.SelectedProviders > 0);
        auditLogRepositoryMock.Verify(
            x => x.AddAsync(It.Is<AdminAuditLog>(log =>
                log.TargetType == "ProviderReactivationCampaign" &&
                log.Action == "campaign_run_completed")),
            Times.Once);
    }

    /// <summary>
    /// Cenario: rodada com canais habilitados para acao de reativacao.
    /// Passos: um prestador elegivel e servicos de sistema/push/email mockados.
    /// Resultado esperado: resumo de entrega contabiliza os tres canais e sem falhas.
    /// </summary>
    [Fact(DisplayName = "Admin growth service | Campanha reativacao | Deve integrar canais sistema/push/email")]
    public async Task RunProviderReactivationCampaignAsync_ShouldDispatchConfiguredChannels()
    {
        var nowUtc = DateTime.UtcNow;
        var provider = new User
        {
            Id = Guid.NewGuid(),
            Name = "Prestador Canais",
            Email = "canais@teste.com",
            Role = UserRole.Provider,
            IsActive = true,
            CreatedAt = nowUtc.AddDays(-180),
            ProviderProfile = new ProviderProfile
            {
                BaseZipCode = "01001-000",
                Categories = new List<ServiceCategory> { ServiceCategory.Electrical }
            }
        };

        var userRepositoryMock = new Mock<IUserRepository>();
        userRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<User> { provider });

        var requestRepositoryMock = new Mock<IServiceRequestRepository>();
        var proposalRepositoryMock = new Mock<IProposalRepository>();
        proposalRepositoryMock
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(new List<Proposal>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ProviderId = provider.Id,
                    RequestId = Guid.NewGuid(),
                    CreatedAt = nowUtc.AddDays(-8)
                }
            });

        var auditLogRepositoryMock = new Mock<IAdminAuditLogRepository>();
        auditLogRepositoryMock
            .Setup(x => x.GetByTargetAndPeriodAsync(
                "ProviderReactivationCampaign",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                null,
                null,
                "campaign_run_completed",
                1))
            .ReturnsAsync(new List<AdminAuditLog>());
        auditLogRepositoryMock
            .Setup(x => x.GetByTargetAndPeriodAsync(
                "UserAuth",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                null,
                null,
                "user_login",
                20000))
            .ReturnsAsync(new List<AdminAuditLog>());

        var notificationServiceMock = new Mock<INotificationService>();
        notificationServiceMock
            .Setup(x => x.SendNotificationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>()))
            .Returns(Task.CompletedTask);

        var pushServiceMock = new Mock<IMobilePushNotificationService>();
        pushServiceMock
            .Setup(x => x.SendToUserAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var emailServiceMock = new Mock<IEmailService>();
        emailServiceMock
            .Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var service = new AdminGrowthService(
            userRepositoryMock.Object,
            requestRepositoryMock.Object,
            proposalRepositoryMock.Object,
            auditLogRepositoryMock.Object,
            notificationServiceMock.Object,
            pushServiceMock.Object,
            emailServiceMock.Object);

        var result = await service.RunProviderReactivationCampaignAsync(
            new AdminProviderReactivationCampaignRunRequestDto(
                AsOfUtc: nowUtc,
                CadenceHours: 24,
                MaxRecipients: 50,
                ForceRun: false,
                SegmentCode: "warm",
                SendSystem: true,
                SendPush: true,
                SendEmail: true,
                MessageTemplate: null),
            Guid.NewGuid(),
            "growth-admin@teste.com");

        Assert.True(result.Executed);
        Assert.NotNull(result.Delivery);
        Assert.Equal(1, result.Delivery!.SystemSent);
        Assert.Equal(1, result.Delivery.PushSent);
        Assert.Equal(1, result.Delivery.EmailSent);
        Assert.Equal(0, result.Delivery.Failed);
    }

    /// <summary>
    /// Cenario: consolidado de performance calcula reativacao por campanha.
    /// Passos: campanha com 2 destinatarios e 1 login apos disparo.
    /// Resultado esperado: taxa de reativacao de 50%.
    /// </summary>
    [Fact(DisplayName = "Admin growth service | Performance campanha | Deve calcular taxa de reativacao")]
    public async Task GetProviderReactivationCampaignPerformanceAsync_ShouldCalculateReactivationRate()
    {
        var nowUtc = DateTime.UtcNow;
        var provider1 = Guid.NewGuid();
        var provider2 = Guid.NewGuid();
        var campaignId = Guid.NewGuid();

        var userRepositoryMock = new Mock<IUserRepository>();
        var requestRepositoryMock = new Mock<IServiceRequestRepository>();
        var proposalRepositoryMock = new Mock<IProposalRepository>();

        var metadataJson = """
            {
              "selectedProviders": 2,
              "providerIds": [
                "__PROVIDER_1__",
                "__PROVIDER_2__"
              ],
              "delivery": {
                "systemSent": 2,
                "pushSent": 2,
                "emailSent": 1,
                "failed": 0
              }
            }
            """
            .Replace("__PROVIDER_1__", provider1.ToString("D"))
            .Replace("__PROVIDER_2__", provider2.ToString("D"));

        var campaignLog = new AdminAuditLog
        {
            Id = Guid.NewGuid(),
            TargetType = "ProviderReactivationCampaign",
            TargetId = campaignId,
            Action = "campaign_run_completed",
            CreatedAt = nowUtc.AddHours(-2),
            Metadata = metadataJson
        };

        var loginLog = new AdminAuditLog
        {
            Id = Guid.NewGuid(),
            TargetType = "UserAuth",
            Action = "user_login",
            ActorUserId = provider1,
            CreatedAt = nowUtc.AddHours(-1)
        };

        var auditLogRepositoryMock = new Mock<IAdminAuditLogRepository>();
        auditLogRepositoryMock
            .Setup(x => x.GetByTargetAndPeriodAsync(
                "ProviderReactivationCampaign",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                null,
                null,
                "campaign_run_completed",
                It.IsAny<int>()))
            .ReturnsAsync(new List<AdminAuditLog> { campaignLog });

        auditLogRepositoryMock
            .Setup(x => x.GetByTargetAndPeriodAsync(
                "UserAuth",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                null,
                null,
                "user_login",
                20000))
            .ReturnsAsync(new List<AdminAuditLog> { loginLog });

        var service = new AdminGrowthService(
            userRepositoryMock.Object,
            requestRepositoryMock.Object,
            proposalRepositoryMock.Object,
            auditLogRepositoryMock.Object);

        var result = await service.GetProviderReactivationCampaignPerformanceAsync(
            new AdminProviderReactivationCampaignPerformanceQueryDto(
                FromUtc: nowUtc.AddDays(-7),
                ToUtc: nowUtc,
                Take: 20));

        Assert.Equal(1, result.TotalCampaigns);
        Assert.Equal(2, result.TotalSelectedProviders);
        Assert.Equal(1, result.TotalReactivatedProviders);
        Assert.Equal(50m, result.ReactivationRatePercent);
        Assert.Single(result.Items);
        Assert.Equal(2, result.Items[0].SystemSent);
        Assert.Equal(2, result.Items[0].PushSent);
        Assert.Equal(1, result.Items[0].EmailSent);
    }

    /// <summary>
    /// Cenario: politicas de opt-out e frequencia suprimem destinatarios da campanha.
    /// Passos: um prestador em opt-out e outro acima do limite de toques na janela.
    /// Resultado esperado: campanha finaliza sem elegiveis e retorna contadores de supressao.
    /// </summary>
    [Fact(DisplayName = "Admin growth service | Campanha reativacao | Deve suprimir por opt-out e frequencia")]
    public async Task RunProviderReactivationCampaignAsync_ShouldApplyOptOutAndFrequencyPolicy()
    {
        var nowUtc = DateTime.UtcNow;
        var optOutProvider = new User
        {
            Id = Guid.NewGuid(),
            Name = "Prestador OptOut",
            Email = "optout@teste.com",
            Role = UserRole.Provider,
            IsActive = true,
            CreatedAt = nowUtc.AddDays(-150),
            ProviderProfile = new ProviderProfile
            {
                BaseZipCode = "01001-000",
                Categories = new List<ServiceCategory> { ServiceCategory.Electrical }
            }
        };

        var cappedProvider = new User
        {
            Id = Guid.NewGuid(),
            Name = "Prestador Capped",
            Email = "capped@teste.com",
            Role = UserRole.Provider,
            IsActive = true,
            CreatedAt = nowUtc.AddDays(-140),
            ProviderProfile = new ProviderProfile
            {
                BaseZipCode = "20031-170",
                Categories = new List<ServiceCategory> { ServiceCategory.Plumbing }
            }
        };

        var userRepositoryMock = new Mock<IUserRepository>();
        userRepositoryMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<User> { optOutProvider, cappedProvider });

        var requestRepositoryMock = new Mock<IServiceRequestRepository>();
        var proposalRepositoryMock = new Mock<IProposalRepository>();
        proposalRepositoryMock
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(new List<Proposal>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ProviderId = optOutProvider.Id,
                    RequestId = Guid.NewGuid(),
                    CreatedAt = nowUtc.AddDays(-8)
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    ProviderId = cappedProvider.Id,
                    RequestId = Guid.NewGuid(),
                    CreatedAt = nowUtc.AddDays(-8)
                }
            });

        var preferenceMetadata = $$"""
            {
              "optOut": true,
              "maxTouchesPerWeek": 3,
              "reason": "Solicitacao direta"
            }
            """;

        var touchMetadata = $$"""
            {
              "selectedProviders": 1,
              "providerIds": ["{{cappedProvider.Id:D}}"]
            }
            """;

        var auditLogRepositoryMock = new Mock<IAdminAuditLogRepository>();
        auditLogRepositoryMock
            .Setup(x => x.GetByTargetAndPeriodAsync(
                "ProviderReactivationCampaign",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                null,
                null,
                "campaign_run_completed",
                1))
            .ReturnsAsync(new List<AdminAuditLog>());

        auditLogRepositoryMock
            .Setup(x => x.GetByTargetAndPeriodAsync(
                "ProviderReactivationPreference",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                null,
                null,
                "upsert",
                20000))
            .ReturnsAsync(new List<AdminAuditLog>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    TargetType = "ProviderReactivationPreference",
                    TargetId = optOutProvider.Id,
                    Action = "upsert",
                    CreatedAt = nowUtc.AddDays(-1),
                    Metadata = preferenceMetadata
                }
            });

        auditLogRepositoryMock
            .Setup(x => x.GetByTargetAndPeriodAsync(
                "ProviderReactivationCampaign",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                null,
                null,
                "campaign_run_completed",
                5000))
            .ReturnsAsync(new List<AdminAuditLog>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    TargetType = "ProviderReactivationCampaign",
                    Action = "campaign_run_completed",
                    CreatedAt = nowUtc.AddDays(-2),
                    Metadata = touchMetadata
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    TargetType = "ProviderReactivationCampaign",
                    Action = "campaign_run_completed",
                    CreatedAt = nowUtc.AddDays(-1),
                    Metadata = touchMetadata
                }
            });

        auditLogRepositoryMock
            .Setup(x => x.GetByTargetAndPeriodAsync(
                "UserAuth",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                null,
                null,
                "user_login",
                20000))
            .ReturnsAsync(new List<AdminAuditLog>());

        var service = new AdminGrowthService(
            userRepositoryMock.Object,
            requestRepositoryMock.Object,
            proposalRepositoryMock.Object,
            auditLogRepositoryMock.Object);

        var result = await service.RunProviderReactivationCampaignAsync(
            new AdminProviderReactivationCampaignRunRequestDto(
                AsOfUtc: nowUtc,
                CadenceHours: 24,
                MaxRecipients: 20,
                ForceRun: false,
                SegmentCode: "warm",
                RespectOptOut: true,
                DefaultMaxTouchesPerWeek: 2,
                FrequencyWindowDays: 7,
                SendSystem: true,
                SendPush: true,
                SendEmail: false,
                MessageTemplate: null),
            Guid.NewGuid(),
            "growth-admin@teste.com");

        Assert.Equal("completed_without_recipients", result.Status);
        Assert.Equal(0, result.SelectedProviders);
        Assert.NotNull(result.Policy);
        Assert.Equal(1, result.Policy!.SuppressedByOptOut);
        Assert.Equal(1, result.Policy.SuppressedByFrequency);
        Assert.Equal(0, result.Policy.EligibleAfterPolicy);
    }
}
