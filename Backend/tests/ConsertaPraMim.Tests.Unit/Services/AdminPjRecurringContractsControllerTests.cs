using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminPjRecurringContractsControllerTests
{
    /// <summary>
    /// Cenario: validacao de seguranca da carteira PJ admin.
    /// Passos: inspeciona atributo de autorizacao do controller.
    /// Resultado esperado: policy AdminOnly aplicada.
    /// </summary>
    [Fact(DisplayName = "Admin PJ recurring contracts controller | Controller | Deve exigir policy AdminOnly")]
    public void Controller_ShouldRequireAdminOnlyPolicy()
    {
        var authorize = typeof(AdminPjRecurringContractsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal("AdminOnly", authorize!.Policy);
    }

    /// <summary>
    /// Cenario: operacao admin carrega carteira PJ com filtros.
    /// Passos: mock de service retorna snapshot consolidado.
    /// Resultado esperado: endpoint responde 200 com payload esperado.
    /// </summary>
    [Fact(DisplayName = "Admin PJ recurring contracts controller | Portfolio | Deve retornar ok com carteira consolidada")]
    public async Task GetPortfolio_ShouldReturnOk_WithPortfolioPayload()
    {
        var serviceMock = new Mock<IPjRecurringContractService>();
        serviceMock
            .Setup(x => x.GetAdminPortfolioAsync(
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<PjRecurringContractStatus?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminPjRecurringPortfolioDto(
                DateTime.UtcNow,
                1,
                1,
                0,
                400m,
                400m,
                new List<AdminPjRecurringStatusBreakdownDto>
                {
                    new(PjRecurringContractStatus.Active, 1, 400m)
                },
                new List<AdminPjRecurringCategoryBreakdownDto>
                {
                    new(ServiceCategory.Electrical, 1, 400m)
                },
                new List<AdminPjRecurringPortfolioItemDto>()));

        var controller = new AdminPjRecurringContractsController(serviceMock.Object);
        var result = await controller.GetPortfolio(null, null, null);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AdminPjRecurringPortfolioDto>(okResult.Value);
        Assert.Equal(1, payload.TotalContracts);
    }
}
