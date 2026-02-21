using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminDashboardControllerTests
{
    /// <summary>
    /// Cenario: dashboard administrativo deve ser acessivel somente por administradores.
    /// Passos: verifica via reflexao o atributo de autorizacao declarado no controller.
    /// Resultado esperado: presenca da policy AdminOnly protegendo o endpoint.
    /// </summary>
    [Fact(DisplayName = "Admin dashboard controller | Controller | Deve protected com admin only politica")]
    public void Controller_ShouldBeProtectedWithAdminOnlyPolicy()
    {
        var authorize = typeof(AdminDashboardController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal("AdminOnly", authorize!.Policy);
    }

    /// <summary>
    /// Cenario: endpoint de dashboard recebe consulta e o servico retorna snapshot consolidado de KPIs.
    /// Passos: mocka IAdminDashboardService com DTO completo e chama GetDashboard no controller.
    /// Resultado esperado: resposta OK com o mesmo payload de negocio retornado pelo servico.
    /// </summary>
    [Fact(DisplayName = "Admin dashboard controller | Obter dashboard | Deve retornar ok com servico payload")]
    public async Task GetDashboard_ShouldReturnOkWithServicePayload()
    {
        var serviceMock = new Mock<IAdminDashboardService>();
        var expected = new AdminDashboardDto(
            TotalUsers: 10,
            ActiveUsers: 9,
            InactiveUsers: 1,
            TotalProviders: 4,
            TotalClients: 5,
            OnlineProviders: 2,
            OnlineClients: 3,
            PayingProviders: 3,
            MonthlySubscriptionRevenue: 409.70m,
            RevenueByPlan: new List<AdminPlanRevenueDto>
            {
                new("Gold", 1, 199.90m, 199.90m),
                new("Silver", 1, 129.90m, 129.90m),
                new("Bronze", 1, 79.90m, 79.90m)
            },
            TotalAdmins: 1,
            TotalRequests: 12,
            ActiveRequests: 7,
            RequestsInPeriod: 3,
            RequestsByStatus: new List<AdminStatusCountDto> { new("Created", 2), new("Completed", 1) },
            RequestsByCategory: new List<AdminCategoryCountDto> { new("Hidraulica", 2), new("Eletrica", 1) },
            ProposalsInPeriod: 6,
            AcceptedProposalsInPeriod: 2,
            ActiveChatConversationsLast24h: 3,
            FromUtc: DateTime.UtcNow.AddDays(-1),
            ToUtc: DateTime.UtcNow,
            Page: 1,
            PageSize: 20,
            TotalEvents: 1,
            RecentEvents: new List<AdminRecentEventDto>
            {
                new("request", Guid.NewGuid(), DateTime.UtcNow, "Pedido criado", "Descricao")
            });

        serviceMock
            .Setup(s => s.GetDashboardAsync(It.IsAny<AdminDashboardQueryDto>()))
            .ReturnsAsync(expected);

        var controller = new AdminDashboardController(serviceMock.Object);

        var result = await controller.GetDashboard(null, null, "all", null, null, 1, 20);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AdminDashboardDto>(ok.Value);
        Assert.Equal(expected.TotalUsers, payload.TotalUsers);
        Assert.Equal(expected.TotalEvents, payload.TotalEvents);
    }
}
