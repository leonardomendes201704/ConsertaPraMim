using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace ConsertaPraMim.Tests.Unit.Controllers;

public class ProviderOnboardingControllerTests
{
    [Fact(DisplayName = "Prestador onboarding controller | Upload document | Deve retornar invalida requisicao quando extension invalido")]
    public async Task UploadDocument_ShouldReturnBadRequest_WhenExtensionIsInvalid()
    {
        var onboardingServiceMock = new Mock<IProviderOnboardingService>();
        var fileStorageMock = new Mock<IFileStorageService>();
        var controller = CreateController(onboardingServiceMock.Object, fileStorageMock.Object, BuildProviderUser());

        var result = await controller.UploadDocument(new ProviderOnboardingController.UploadProviderOnboardingDocumentRequest
        {
            DocumentType = ProviderDocumentType.IdentityDocument,
            File = CreateFormFile("malware.exe", "application/octet-stream", 1024)
        });

        Assert.IsType<BadRequestObjectResult>(result);
        fileStorageMock.Verify(s => s.SaveFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName = "Prestador onboarding controller | Upload document | Deve retornar invalida requisicao quando mime type invalido")]
    public async Task UploadDocument_ShouldReturnBadRequest_WhenMimeTypeIsInvalid()
    {
        var onboardingServiceMock = new Mock<IProviderOnboardingService>();
        var fileStorageMock = new Mock<IFileStorageService>();
        var controller = CreateController(onboardingServiceMock.Object, fileStorageMock.Object, BuildProviderUser());

        var result = await controller.UploadDocument(new ProviderOnboardingController.UploadProviderOnboardingDocumentRequest
        {
            DocumentType = ProviderDocumentType.IdentityDocument,
            File = CreateFormFile("doc.pdf", "text/plain", 1024)
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact(DisplayName = "Prestador onboarding controller | Upload document | Deve retornar invalida requisicao quando file exceeds limit")]
    public async Task UploadDocument_ShouldReturnBadRequest_WhenFileExceedsLimit()
    {
        var onboardingServiceMock = new Mock<IProviderOnboardingService>();
        var fileStorageMock = new Mock<IFileStorageService>();
        var controller = CreateController(onboardingServiceMock.Object, fileStorageMock.Object, BuildProviderUser());

        var result = await controller.UploadDocument(new ProviderOnboardingController.UploadProviderOnboardingDocumentRequest
        {
            DocumentType = ProviderDocumentType.IdentityDocument,
            File = CreateFormFile("doc.pdf", "application/pdf", 10_000_001)
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact(DisplayName = "Prestador onboarding controller | Upload document | Deve sanitize file name before saving")]
    public async Task UploadDocument_ShouldSanitizeFileName_BeforeSaving()
    {
        var onboardingServiceMock = new Mock<IProviderOnboardingService>();
        var fileStorageMock = new Mock<IFileStorageService>();

        fileStorageMock
            .Setup(s => s.SaveFileAsync(It.IsAny<Stream>(), "evil__.pdf", "provider-docs"))
            .ReturnsAsync("/uploads/provider-docs/123.pdf");

        onboardingServiceMock
            .Setup(s => s.AddDocumentAsync(It.IsAny<Guid>(), It.IsAny<AddProviderOnboardingDocumentDto>()))
            .ReturnsAsync(new ProviderOnboardingDocumentDto(
                Guid.NewGuid(),
                ProviderDocumentType.IdentityDocument,
                ProviderDocumentStatus.Pending,
                "evil__.pdf",
                "application/pdf",
                100,
                "/uploads/provider-docs/123.pdf",
                DateTime.UtcNow,
                null));

        var controller = CreateController(onboardingServiceMock.Object, fileStorageMock.Object, BuildProviderUser());

        var result = await controller.UploadDocument(new ProviderOnboardingController.UploadProviderOnboardingDocumentRequest
        {
            DocumentType = ProviderDocumentType.IdentityDocument,
            File = CreateFormFile("../../evil%?.pdf", "application/pdf", 100)
        });

        Assert.IsType<OkObjectResult>(result);
        fileStorageMock.Verify(s => s.SaveFileAsync(It.IsAny<Stream>(), "evil__.pdf", "provider-docs"), Times.Once);
    }

    private static ProviderOnboardingController CreateController(
        IProviderOnboardingService onboardingService,
        IFileStorageService fileStorageService,
        ClaimsPrincipal user)
    {
        var controller = new ProviderOnboardingController(onboardingService, fileStorageService);
        var context = new DefaultHttpContext
        {
            User = user
        };

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = context
        };

        return controller;
    }

    private static ClaimsPrincipal BuildProviderUser()
    {
        return new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, UserRole.Provider.ToString())
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
