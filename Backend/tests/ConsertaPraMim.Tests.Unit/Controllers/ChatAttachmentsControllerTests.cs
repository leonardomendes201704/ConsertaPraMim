using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace ConsertaPraMim.Tests.Unit.Controllers;

public class ChatAttachmentsControllerTests
{
    [Fact(DisplayName = "Chat anexos controller | Upload | Deve retornar invalida requisicao quando file missing")]
    public async Task Upload_ShouldReturnBadRequest_WhenFileIsMissing()
    {
        var fileStorageMock = new Mock<IFileStorageService>();
        var chatServiceMock = new Mock<IChatService>();
        var controller = CreateController(fileStorageMock.Object, chatServiceMock.Object, null);

        var result = await controller.Upload(new ChatAttachmentsController.ChatAttachmentUploadRequest
        {
            RequestId = Guid.NewGuid(),
            ProviderId = Guid.NewGuid(),
            File = null
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact(DisplayName = "Chat anexos controller | Upload | Deve retornar invalida requisicao quando file extension nao supported")]
    public async Task Upload_ShouldReturnBadRequest_WhenFileExtensionIsNotSupported()
    {
        var fileStorageMock = new Mock<IFileStorageService>();
        var chatServiceMock = new Mock<IChatService>();
        var controller = CreateController(fileStorageMock.Object, chatServiceMock.Object, BuildUser(Guid.NewGuid(), "Client"));

        var result = await controller.Upload(new ChatAttachmentsController.ChatAttachmentUploadRequest
        {
            RequestId = Guid.NewGuid(),
            ProviderId = Guid.NewGuid(),
            File = CreateFormFile("malware.exe", "application/octet-stream", 1024)
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact(DisplayName = "Chat anexos controller | Upload | Deve retornar nao autorizado quando claims invalido")]
    public async Task Upload_ShouldReturnUnauthorized_WhenClaimsAreInvalid()
    {
        var fileStorageMock = new Mock<IFileStorageService>();
        var chatServiceMock = new Mock<IChatService>();
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        }, "Test"));
        var controller = CreateController(fileStorageMock.Object, chatServiceMock.Object, user);

        var result = await controller.Upload(new ChatAttachmentsController.ChatAttachmentUploadRequest
        {
            RequestId = Guid.NewGuid(),
            ProviderId = Guid.NewGuid(),
            File = CreateFormFile("foto.jpg", "image/jpeg", 1024)
        });

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact(DisplayName = "Chat anexos controller | Upload | Deve retornar forbid quando usuario nao pode access conversation")]
    public async Task Upload_ShouldReturnForbid_WhenUserCannotAccessConversation()
    {
        var fileStorageMock = new Mock<IFileStorageService>();
        var chatServiceMock = new Mock<IChatService>();

        var senderId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var providerId = Guid.NewGuid();

        chatServiceMock
            .Setup(s => s.CanAccessConversationAsync(requestId, providerId, senderId, "Client"))
            .ReturnsAsync(false);

        var controller = CreateController(fileStorageMock.Object, chatServiceMock.Object, BuildUser(senderId, "Client"));

        var result = await controller.Upload(new ChatAttachmentsController.ChatAttachmentUploadRequest
        {
            RequestId = requestId,
            ProviderId = providerId,
            File = CreateFormFile("foto.jpg", "image/jpeg", 1024)
        });

        Assert.IsType<ForbidResult>(result);
    }

    [Fact(DisplayName = "Chat anexos controller | Upload | Deve retornar absolute file url quando upload sucesso")]
    public async Task Upload_ShouldReturnAbsoluteFileUrl_WhenUploadSucceeds()
    {
        var fileStorageMock = new Mock<IFileStorageService>();
        var chatServiceMock = new Mock<IChatService>();

        var senderId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var providerId = Guid.NewGuid();

        chatServiceMock
            .Setup(s => s.CanAccessConversationAsync(requestId, providerId, senderId, "Client"))
            .ReturnsAsync(true);
        fileStorageMock
            .Setup(s => s.SaveFileAsync(It.IsAny<Stream>(), "foto.jpg", "chat"))
            .ReturnsAsync("/uploads/chat/foto.jpg");

        var controller = CreateController(fileStorageMock.Object, chatServiceMock.Object, BuildUser(senderId, "Client"));

        var result = await controller.Upload(new ChatAttachmentsController.ChatAttachmentUploadRequest
        {
            RequestId = requestId,
            ProviderId = providerId,
            File = CreateFormFile("foto.jpg", "image/jpeg", 1024)
        });

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var payloadType = okResult.Value!.GetType();
        var fileUrl = payloadType.GetProperty("fileUrl")?.GetValue(okResult.Value)?.ToString();
        var fileName = payloadType.GetProperty("fileName")?.GetValue(okResult.Value)?.ToString();

        Assert.Equal("https://localhost:7281/uploads/chat/foto.jpg", fileUrl);
        Assert.Equal("foto.jpg", fileName);
        fileStorageMock.Verify(s => s.SaveFileAsync(It.IsAny<Stream>(), "foto.jpg", "chat"), Times.Once);
    }

    private static ChatAttachmentsController CreateController(
        IFileStorageService fileStorageService,
        IChatService chatService,
        ClaimsPrincipal? user)
    {
        var controller = new ChatAttachmentsController(fileStorageService, chatService);
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("localhost:7281");
        if (user != null)
        {
            context.User = user;
        }

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = context
        };

        return controller;
    }

    private static ClaimsPrincipal BuildUser(Guid userId, string role)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        }, "Test"));
    }

    private static IFormFile CreateFormFile(string fileName, string contentType, int sizeBytes)
    {
        var bytes = new byte[sizeBytes];
        var stream = new MemoryStream(bytes);

        return new FormFile(stream, 0, sizeBytes, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
