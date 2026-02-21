using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminChatsControllerTests
{
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
