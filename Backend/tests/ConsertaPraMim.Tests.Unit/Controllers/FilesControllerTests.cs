using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Controllers;

public class FilesControllerTests
{
    /// <summary>
    /// Cenario: cliente envia anexo da aba de ajuda usando a pasta `support`.
    /// Passos: chama o endpoint de upload com arquivo valido e folder `support`.
    /// Resultado esperado: API aceita a pasta, salva o arquivo e retorna URLs para o portal continuar o fluxo.
    /// </summary>
    [Fact(DisplayName = "Files controller | Upload | Deve aceitar folder support para anexos de ajuda")]
    public async Task Upload_ShouldAcceptSupportFolder()
    {
        var storageMock = new Mock<IFileStorageService>();
        storageMock
            .Setup(storage => storage.SaveFileAsync(It.IsAny<Stream>(), "ajuda.png", "support"))
            .ReturnsAsync("/uploads/support/ajuda.png");

        var controller = new FilesController(storageMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        controller.ControllerContext.HttpContext.Request.Scheme = "https";
        controller.ControllerContext.HttpContext.Request.Host = new HostString("localhost:5193");

        var result = await controller.Upload(new FilesController.FileUploadRequest
        {
            Folder = "support",
            File = CreateFormFile("ajuda.png", "image/png", 1024)
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        storageMock.VerifyAll();
    }

    private static IFormFile CreateFormFile(string fileName, string contentType, int sizeBytes)
    {
        var content = new byte[sizeBytes];
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, sizeBytes, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
