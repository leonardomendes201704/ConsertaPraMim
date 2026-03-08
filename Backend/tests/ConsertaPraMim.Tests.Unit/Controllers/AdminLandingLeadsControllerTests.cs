using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ConsertaPraMim.Tests.Unit.Controllers;

public class AdminLandingLeadsControllerTests
{
    /// <summary>
    /// Cenario: portal admin consulta a listagem de leads captados pela landing.
    /// Passos: controller recebe filtros simples, delega ao service e retorna o payload paginado.
    /// Resultado esperado: `200 OK` com a resposta do modulo administrativo de leads.
    /// </summary>
    [Fact(DisplayName = "Admin landing leads controller | Lista | Deve retornar ok com leads paginados")]
    public async Task GetAll_ShouldReturnOkWithPagedLeads()
    {
        var serviceMock = new Mock<IAdminLandingLeadService>();
        serviceMock
            .Setup(service => service.GetLandingLeadsAsync(It.IsAny<AdminLandingLeadsQueryDto>()))
            .ReturnsAsync(new AdminLandingLeadsListResponseDto(
                1,
                20,
                1,
                1,
                0,
                new[]
                {
                    new AdminLandingLeadListItemDto(
                        Guid.NewGuid(),
                        LandingLeadOrigin.Client,
                        "Leonardo Silva",
                        "13999999999",
                        "leo@exemplo.com",
                        "Ocian - Praia Grande/SP",
                        "Praia Grande",
                        "SP",
                        "Ocian",
                        "Conserto de ar-condicionado",
                        "landing-marco",
                        DateTime.UtcNow)
                }));

        var controller = new AdminLandingLeadsController(serviceMock.Object);

        var result = await controller.GetAll("Ocian", "Client", "Praia Grande", "SP", null, null, 1, 20);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AdminLandingLeadsListResponseDto>(okResult.Value);
        Assert.Equal(1, payload.TotalCount);
        serviceMock.VerifyAll();
    }

    /// <summary>
    /// Cenario: admin tenta abrir um lead inexistente.
    /// Passos: service retorna `null` para o identificador informado.
    /// Resultado esperado: controller responde `404 NotFound`.
    /// </summary>
    [Fact(DisplayName = "Admin landing leads controller | Detalhe | Deve retornar not found quando lead nao existir")]
    public async Task GetById_ShouldReturnNotFoundWhenLeadDoesNotExist()
    {
        var serviceMock = new Mock<IAdminLandingLeadService>();
        serviceMock
            .Setup(service => service.GetLandingLeadByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((AdminLandingLeadDetailsDto?)null);

        var controller = new AdminLandingLeadsController(serviceMock.Object);

        var result = await controller.GetById(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
        serviceMock.VerifyAll();
    }
}
