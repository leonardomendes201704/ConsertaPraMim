using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Controllers;

public class ProviderGalleryControllerTests
{
    [Fact(DisplayName = "Provider gallery controller | Public photos base64 | Deve retornar bad request quando provider invalido")]
    public async Task GetPublicProviderAlbumPhotosBase64_ShouldReturnBadRequest_WhenProviderIdIsInvalid()
    {
        var serviceMock = new Mock<IProviderGalleryService>();
        var controller = CreateController(serviceMock.Object);

        var result = await controller.GetPublicProviderAlbumPhotosBase64(Guid.Empty);

        Assert.IsType<BadRequestObjectResult>(result);
        serviceMock.Verify(
            service => service.GetOverviewAsync(It.IsAny<Guid>(), It.IsAny<ProviderGalleryFilterDto>()),
            Times.Never);
    }

    [Fact(DisplayName = "Provider gallery controller | Public photos base64 | Deve retornar fotos em base64 quando anonimo")]
    public async Task GetPublicProviderAlbumPhotosBase64_ShouldReturnPhotosBase64_WhenAnonymous()
    {
        var providerId = Guid.NewGuid();
        var albumId = Guid.NewGuid();
        var tempRoot = Path.Combine(Path.GetTempPath(), "cpm-provider-gallery-tests", Guid.NewGuid().ToString("N"));
        var webRoot = Path.Combine(tempRoot, "wwwroot");
        var galleryFolder = Path.Combine(webRoot, "uploads", "provider-gallery");
        Directory.CreateDirectory(galleryFolder);

        var photoBytes = new byte[] { 10, 20, 30, 40, 50 };
        var photoPath = Path.Combine(galleryFolder, "foto-ok.jpg");
        await File.WriteAllBytesAsync(photoPath, photoBytes);

        try
        {
            var serviceMock = new Mock<IProviderGalleryService>();
            serviceMock
                .Setup(service => service.GetOverviewAsync(
                    providerId,
                    It.IsAny<ProviderGalleryFilterDto>()))
                .ReturnsAsync(new ProviderGalleryOverviewDto(
                    new ProviderGalleryFilterDto(null, null, null),
                    [],
                    [
                        new ProviderGalleryAlbumDto(
                            albumId,
                            "Album Principal",
                            "Reformas",
                            false,
                            null,
                            null,
                            3,
                            null,
                            DateTime.UtcNow)
                    ],
                    [
                        new ProviderGalleryItemDto(
                            Guid.NewGuid(),
                            albumId,
                            "Album Principal",
                            null,
                            null,
                            null,
                            null,
                            "/uploads/provider-gallery/foto-ok.jpg",
                            null,
                            null,
                            "foto-ok.jpg",
                            "image/jpeg",
                            photoBytes.Length,
                            "image",
                            "Reformas",
                            "Foto valida",
                            DateTime.UtcNow.AddMinutes(-5)),
                        new ProviderGalleryItemDto(
                            Guid.NewGuid(),
                            albumId,
                            "Album Principal",
                            null,
                            null,
                            null,
                            null,
                            "/uploads/provider-gallery/foto-ausente.jpg",
                            null,
                            null,
                            "foto-ausente.jpg",
                            "image/jpeg",
                            1024,
                            "image",
                            "Reformas",
                            "Foto ausente",
                            DateTime.UtcNow.AddMinutes(-4)),
                        new ProviderGalleryItemDto(
                            Guid.NewGuid(),
                            albumId,
                            "Album Principal",
                            null,
                            null,
                            null,
                            null,
                            "/uploads/provider-gallery/video.mp4",
                            null,
                            null,
                            "video.mp4",
                            "video/mp4",
                            4096,
                            "video",
                            "Reformas",
                            "Video",
                            DateTime.UtcNow.AddMinutes(-3))
                    ]));

            var controller = CreateController(serviceMock.Object, webRoot);
            var result = await controller.GetPublicProviderAlbumPhotosBase64(providerId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var payload = Assert.IsType<PublicProviderGalleryPhotosBase64ResponseDto>(okResult.Value);

            Assert.Equal(providerId, payload.ProviderId);
            Assert.Equal(1, payload.TotalPhotos);
            Assert.Equal(1, payload.UnavailablePhotosCount);
            Assert.Single(payload.Albums);
            Assert.Single(payload.Albums[0].Photos);
            Assert.Equal(Convert.ToBase64String(photoBytes), payload.Albums[0].Photos[0].Base64Content);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static ProviderGalleryController CreateController(
        IProviderGalleryService providerGalleryService,
        string? webRootPath = null)
    {
        var mediaProcessorMock = new Mock<IProviderGalleryMediaProcessor>();
        var environmentMock = new Mock<IWebHostEnvironment>();
        environmentMock
            .SetupGet(environment => environment.WebRootPath)
            .Returns(webRootPath ?? Path.Combine(Path.GetTempPath(), "cpm-provider-gallery-tests-empty"));
        environmentMock
            .SetupGet(environment => environment.ContentRootPath)
            .Returns(webRootPath ?? Path.Combine(Path.GetTempPath(), "cpm-provider-gallery-tests-empty"));

        return new ProviderGalleryController(
            providerGalleryService,
            mediaProcessorMock.Object,
            environmentMock.Object);
    }
}
