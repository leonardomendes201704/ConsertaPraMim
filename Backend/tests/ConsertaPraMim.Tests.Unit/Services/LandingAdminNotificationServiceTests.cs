using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ConsertaPraMim.Tests.Unit.Services;

public class LandingAdminNotificationServiceTests
{
    [Fact(DisplayName = "Landing admin notification service | Acesso | Deve notificar apenas admins ativos")]
    public async Task NotifyLandingAccessAsync_ShouldNotifyOnlyActiveAdmins()
    {
        var userRepositoryMock = new Mock<IUserRepository>();
        var notificationServiceMock = new Mock<INotificationService>();
        userRepositoryMock
            .Setup(repository => repository.GetAllAsync())
            .ReturnsAsync(new[]
            {
                new User { Id = Guid.NewGuid(), Name = "Admin ativo", Email = "admin@teste.com", Role = UserRole.Admin, IsActive = true },
                new User { Id = Guid.NewGuid(), Name = "Admin inativo", Email = "admin2@teste.com", Role = UserRole.Admin, IsActive = false },
                new User { Id = Guid.NewGuid(), Name = "Cliente", Email = "cliente@teste.com", Role = UserRole.Client, IsActive = true }
            });

        var service = new LandingAdminNotificationService(
            userRepositoryMock.Object,
            notificationServiceMock.Object,
            NullLogger<LandingAdminNotificationService>.Instance);

        await service.NotifyLandingAccessAsync(new NotifyLandingAccessRequestDto(
            VisitorId: "visitor-admin-001",
            SessionId: "session-admin-001",
            CurrentUrl: "https://www.consertapramim.com/Prestador",
            Path: "/Prestador",
            Host: "www.consertapramim.com",
            Scheme: "https",
            InitialLeadOrigin: "provider",
            IpAddress: "187.77.48.150",
            ForwardedFor: "187.77.48.150",
            UserAgent: "Mozilla/5.0 Chrome/136",
            AcceptLanguage: "pt-BR",
            RefererUrl: "https://google.com"));

        notificationServiceMock.Verify(service => service.SendNotificationAsync(
                It.IsAny<string>(),
                "Novo acesso na landing",
                It.Is<string>(message => message.Contains("Landing /Prestador") && message.Contains("187.77.48.150")),
                "/AdminHome/Index",
                It.Is<IReadOnlyDictionary<string, string>>(data =>
                    data["visitorId"] == "visitor-admin-001" &&
                    data["sessionId"] == "session-admin-001" &&
                    data["type"] == "landing_public_access" &&
                    data["path"] == "/Prestador" &&
                    data["ipAddress"] == "187.77.48.150")),
            Times.Once);
    }

    [Fact(DisplayName = "Landing admin notification service | Lead | Deve enviar acao para detalhe do lead")]
    public async Task NotifyLandingLeadCapturedAsync_ShouldSendLeadDetailsAction()
    {
        var userRepositoryMock = new Mock<IUserRepository>();
        var notificationServiceMock = new Mock<INotificationService>();
        var adminId = Guid.NewGuid();

        userRepositoryMock
            .Setup(repository => repository.GetAllAsync())
            .ReturnsAsync(new[]
            {
                new User { Id = adminId, Name = "Admin ativo", Email = "admin@teste.com", Role = UserRole.Admin, IsActive = true }
            });

        var leadId = Guid.NewGuid();
        var lead = new LandingLead
        {
            Id = leadId,
            Origin = LandingLeadOrigin.Client,
            VisitorId = "visitor-admin-002",
            FullName = "Leonardo Silva",
            City = "Praia Grande",
            State = "SP",
            Neighborhood = "Ocian",
            RequestedService = "Instalacao de ar-condicionado",
            Phone = "13999999999",
            Email = "leo@teste.com",
            IpAddress = "187.77.48.150",
            UserAgent = "Mozilla/5.0"
        };

        var service = new LandingAdminNotificationService(
            userRepositoryMock.Object,
            notificationServiceMock.Object,
            NullLogger<LandingAdminNotificationService>.Instance);

        await service.NotifyLandingLeadCapturedAsync(lead);

        notificationServiceMock.Verify(service => service.SendNotificationAsync(
                adminId.ToString("N"),
                "Novo lead de cliente na landing",
                It.Is<string>(message => message.Contains("Leonardo Silva") && message.Contains("Praia Grande/SP")),
                $"/AdminLandingLeads/Details/{leadId}",
                It.Is<IReadOnlyDictionary<string, string>>(data =>
                    data["type"] == "landing_lead_captured" &&
                    data["leadId"] == leadId.ToString("N") &&
                    data["visitorId"] == "visitor-admin-002" &&
                    data["origin"] == "cliente")),
            Times.Once);
    }
}
