using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminChatsControllerTests
{
    /// <summary>
    /// Cenario: conferencia de seguranca no controller de conversas para uso do suporte/admin.
    /// Passos: inspeciona os atributos de autorizacao declarados na classe AdminChatsController.
    /// Resultado esperado: policy AdminOnly aplicada, evitando acesso de perfis nao administrativos.
    /// </summary>
    [Fact(DisplayName = "Admin chats controller | Controller | Deve protected com admin only politica")]
    public void Controller_ShouldBeProtectedWithAdminOnlyPolicy()
    {
        var authorize = typeof(AdminChatsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal("AdminOnly", authorize!.Policy);
    }

    /// <summary>
    /// Cenario: admin consulta conversa por request+provider, mas nao existe historico cadastrado.
    /// Passos: servico retorna null para a combinacao informada e controller processa a ausencia de dados.
    /// Resultado esperado: resposta NotFound para sinalizar que nao ha conversa para aquele par de identificadores.
    /// </summary>
    [Fact(DisplayName = "Admin chats controller | Obter por requisicao e prestador | Deve retornar nao encontrado quando servico returns nulo")]
    public async Task GetByRequestAndProvider_ShouldReturnNotFound_WhenServiceReturnsNull()
    {
        var serviceMock = new Mock<IAdminChatNotificationService>();
        serviceMock.Setup(s => s.GetChatAsync(It.IsAny<Guid>(), It.IsAny<Guid>())).ReturnsAsync((ConsertaPraMim.Application.DTOs.AdminChatDetailsDto?)null);
        var controller = new AdminChatsController(serviceMock.Object);

        var result = await controller.GetByRequestAndProvider(Guid.NewGuid(), Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
    }
}
