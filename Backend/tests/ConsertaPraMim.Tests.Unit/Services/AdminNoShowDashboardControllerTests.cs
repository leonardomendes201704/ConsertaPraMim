using System.Text;
using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminNoShowDashboardControllerTests
{
    /// <summary>
    /// Cenario: painel de no-show exposto apenas para equipe administrativa.
    /// Passos: inspeciona atributos de autorizacao do controller por reflexao.
    /// Resultado esperado: aplicacao da policy AdminOnly no controller.
    /// </summary>
    [Fact(DisplayName = "Admin no show dashboard controller | Controller | Deve protected com admin only politica")]
    public void Controller_ShouldBeProtectedWithAdminOnlyPolicy()
    {
        var authorize = typeof(AdminNoShowDashboardController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal("AdminOnly", authorize!.Policy);
    }

    /// <summary>
    /// Cenario: operacao de exportacao do dashboard de no-show para consumo operacional externo.
    /// Passos: mocka servico de exportacao CSV e executa ExportDashboard com query valida.
    /// Resultado esperado: arquivo CSV com content-type correto, nome padronizado e conteudo retornado pelo servico.
    /// </summary>
    [Fact(DisplayName = "Admin no show dashboard controller | Export dashboard | Deve retornar csv file")]
    public async Task ExportDashboard_ShouldReturnCsvFile()
    {
        const string csvPayload = "Section,Name\r\nKpi,Resumo\r\n";
        var serviceMock = new Mock<IAdminNoShowDashboardService>();
        serviceMock
            .Setup(s => s.ExportDashboardCsvAsync(It.IsAny<AdminNoShowDashboardQueryDto>()))
            .ReturnsAsync(csvPayload);

        var controller = new AdminNoShowDashboardController(serviceMock.Object);

        var result = await controller.ExportDashboard(new AdminNoShowDashboardQueryDto());

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv; charset=utf-8", file.ContentType);
        Assert.StartsWith("admin-no-show-dashboard-", file.FileDownloadName, StringComparison.Ordinal);
        Assert.EndsWith(".csv", file.FileDownloadName, StringComparison.Ordinal);
        Assert.Equal(csvPayload, Encoding.UTF8.GetString(file.FileContents));
        serviceMock.Verify(s => s.ExportDashboardCsvAsync(It.IsAny<AdminNoShowDashboardQueryDto>()), Times.Once);
    }
}
