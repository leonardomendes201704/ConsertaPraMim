using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminChatAttachmentsControllerTests
{
    [Fact]
    public void Controller_ShouldBeProtectedWithAdminOnlyPolicy()
    {
        var authorize = typeof(AdminChatAttachmentsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal("AdminOnly", authorize!.Policy);
    }

    [Fact]
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
