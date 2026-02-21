using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace ConsertaPraMim.Tests.Unit.Controllers;

public class ChatAttachmentsControllerTests
{
    /// <summary>
    /// Cenario: usuario tenta enviar anexo no chat sem selecionar arquivo.
    /// Passos: chama endpoint Upload informando request e provider, mas com File nulo.
    /// Resultado esperado: API responde BadRequest para bloquear upload vazio e preservar integridade do fluxo.
    /// </summary>
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

    /// <summary>
    /// Cenario: usuario anexa arquivo com extensao proibida no chat.
    /// Passos: envia upload autenticado com arquivo .exe e content type generico.
    /// Resultado esperado: retorno BadRequest, evitando anexo potencialmente malicioso na conversa.
    /// </summary>
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

    /// <summary>
    /// Cenario: requisicao chega sem claims minimas para identificar perfil do remetente.
    /// Passos: monta principal sem claim de role valido e executa upload de arquivo permitido.
    /// Resultado esperado: endpoint responde Unauthorized por nao conseguir validar identidade/autorizacao.
    /// </summary>
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

    /// <summary>
    /// Cenario: usuario autenticado tenta anexar arquivo em conversa que nao pertence a ele.
    /// Passos: mocka servico de chat retornando CanAccessConversationAsync=false e chama Upload.
    /// Resultado esperado: retorno Forbid, garantindo isolamento de conversas entre participantes.
    /// </summary>
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

    /// <summary>
    /// Cenario: participante autorizado envia anexo valido para a conversa.
    /// Passos: libera acesso no servico de chat, salva arquivo no storage e executa Upload com host/scheme definidos.
    /// Resultado esperado: resposta OK com URL absoluta e nome do arquivo, alem de persistencia do upload no storage.
    /// </summary>
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
