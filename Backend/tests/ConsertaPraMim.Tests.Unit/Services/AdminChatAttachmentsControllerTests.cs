using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminChatAttachmentsControllerTests
{
    /// <summary>
    /// Cenario: validacao de blindagem de acesso na API administrativa de anexos de chat.
    /// Passos: le atributos de autorizacao do controller e identifica politica configurada no endpoint.
    /// Resultado esperado: controller protegido por AdminOnly para restringir consulta de anexos a administradores.
    /// </summary>
    [Fact(DisplayName = "Admin chat anexos controller | Controller | Deve protected com admin only politica")]
    public void Controller_ShouldBeProtectedWithAdminOnlyPolicy()
    {
        var authorize = typeof(AdminChatAttachmentsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal("AdminOnly", authorize!.Policy);
    }

    /// <summary>
    /// Cenario: admin lista anexos de chat com filtros e paginacao.
    /// Passos: controller invoca servico de notificacao/chat e recebe resposta padronizada, mesmo sem resultados.
    /// Resultado esperado: retorno HTTP 200 com payload de listagem para manter contrato de consulta.
    /// </summary>
    [Fact(DisplayName = "Admin chat anexos controller | Obter all | Deve retornar ok result")]
    public async Task GetAll_ShouldReturnOkResult()
    {
        var serviceMock = new Mock<IAdminChatNotificationService>();
        serviceMock.Setup(s => s.GetChatAttachmentsAsync(It.IsAny<AdminChatAttachmentsQueryDto>()))
            .ReturnsAsync(new AdminChatAttachmentsListResponseDto(1, 20, 0, Array.Empty<AdminChatAttachmentListItemDto>()));

        var controller = new AdminChatAttachmentsController(serviceMock.Object);

        var result = await controller.GetAll(null, null, null, null, null, 1, 20);

        Assert.IsType<OkObjectResult>(result);
    }
}
