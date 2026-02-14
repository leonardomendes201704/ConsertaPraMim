using System.Security.Claims;
using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Controllers;

public class ServiceAppointmentEvidencesControllerTests
{
    [Fact]
    public async Task Upload_ShouldReturnBadRequest_WhenPhaseIsInvalid()
    {
        var serviceMock = new Mock<IServiceAppointmentService>();
        var galleryMock = new Mock<IProviderGalleryService>();
        var fileStorageMock = new Mock<IFileStorageService>();

        var controller = CreateController(
            serviceMock.Object,
            galleryMock.Object,
            fileStorageMock.Object,
            Guid.NewGuid(),
            UserRole.Provider.ToString());

        var file = BuildFormFile(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "teste.jpg", "image/jpeg");
        var result = await controller.Upload(Guid.NewGuid(), new ServiceAppointmentEvidencesController.UploadServiceAppointmentEvidenceRequest
        {
            File = file,
            Phase = "XYZ"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_ShouldReturnOk_WhenRequestIsValid()
    {
        var appointmentId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var serviceRequestId = Guid.NewGuid();

        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.GetByIdAsync(providerId, UserRole.Provider.ToString(), appointmentId))
            .ReturnsAsync(new ServiceAppointmentOperationResultDto(
                true,
                new ServiceAppointmentDto(
                    appointmentId,
                    serviceRequestId,
                    Guid.NewGuid(),
                    providerId,
                    ServiceAppointmentStatus.InProgress.ToString(),
                    DateTime.UtcNow.AddHours(1),
                    DateTime.UtcNow.AddHours(2),
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    DateTime.UtcNow,
                    null,
                    Array.Empty<ServiceAppointmentHistoryDto>())));

        var galleryMock = new Mock<IProviderGalleryService>();
        galleryMock
            .Setup(s => s.AddItemAsync(providerId, It.IsAny<CreateProviderGalleryItemDto>()))
            .ReturnsAsync(new ProviderGalleryItemDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Album",
                serviceRequestId,
                appointmentId,
                "Before",
                "Pedido #123",
                "/uploads/provider-gallery/foto.jpg",
                "foto.jpg",
                "image/jpeg",
                4,
                "image",
                "Hidraulica",
                "ANTES",
                DateTime.UtcNow));

        var fileStorageMock = new Mock<IFileStorageService>();
        fileStorageMock
            .Setup(s => s.SaveFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), "provider-gallery"))
            .ReturnsAsync("/uploads/provider-gallery/foto.jpg");

        var controller = CreateController(
            serviceMock.Object,
            galleryMock.Object,
            fileStorageMock.Object,
            providerId,
            UserRole.Provider.ToString());

        var file = BuildFormFile(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "foto.jpg", "image/jpeg");
        var result = await controller.Upload(appointmentId, new ServiceAppointmentEvidencesController.UploadServiceAppointmentEvidenceRequest
        {
            File = file,
            Phase = "ANTES",
            Category = "Hidraulica",
            Caption = "Antes"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        galleryMock.Verify(s => s.AddItemAsync(providerId, It.Is<CreateProviderGalleryItemDto>(d =>
            d.ServiceRequestId == serviceRequestId &&
            d.ServiceAppointmentId == appointmentId &&
            d.EvidencePhase == "Before")), Times.Once);
    }

    [Fact]
    public async Task Upload_ShouldReturnBadRequest_WhenExtensionDoesNotMatchContentType()
    {
        var serviceMock = new Mock<IServiceAppointmentService>();
        var galleryMock = new Mock<IProviderGalleryService>();
        var fileStorageMock = new Mock<IFileStorageService>();

        var controller = CreateController(
            serviceMock.Object,
            galleryMock.Object,
            fileStorageMock.Object,
            Guid.NewGuid(),
            UserRole.Provider.ToString());

        var file = BuildFormFile(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "foto.png", "image/jpeg");
        var result = await controller.Upload(Guid.NewGuid(), new ServiceAppointmentEvidencesController.UploadServiceAppointmentEvidenceRequest
        {
            File = file,
            Phase = "ANTES"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_ShouldReturnBadRequest_WhenBasicScanFindsSuspiciousContent()
    {
        var appointmentId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var serviceRequestId = Guid.NewGuid();

        var serviceMock = new Mock<IServiceAppointmentService>();
        serviceMock
            .Setup(s => s.GetByIdAsync(providerId, UserRole.Provider.ToString(), appointmentId))
            .ReturnsAsync(new ServiceAppointmentOperationResultDto(
                true,
                new ServiceAppointmentDto(
                    appointmentId,
                    serviceRequestId,
                    Guid.NewGuid(),
                    providerId,
                    ServiceAppointmentStatus.InProgress.ToString(),
                    DateTime.UtcNow.AddHours(1),
                    DateTime.UtcNow.AddHours(2),
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    DateTime.UtcNow,
                    null,
                    Array.Empty<ServiceAppointmentHistoryDto>())));

        var galleryMock = new Mock<IProviderGalleryService>();
        var fileStorageMock = new Mock<IFileStorageService>();

        var controller = CreateController(
            serviceMock.Object,
            galleryMock.Object,
            fileStorageMock.Object,
            providerId,
            UserRole.Provider.ToString());

        var payload = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }
            .Concat(System.Text.Encoding.UTF8.GetBytes("<script>alert('x')</script>"))
            .ToArray();
        var file = BuildFormFile(payload, "foto.jpg", "image/jpeg");

        var result = await controller.Upload(appointmentId, new ServiceAppointmentEvidencesController.UploadServiceAppointmentEvidenceRequest
        {
            File = file,
            Phase = "ANTES"
        });

        Assert.IsType<BadRequestObjectResult>(result);
        galleryMock.Verify(s => s.AddItemAsync(It.IsAny<Guid>(), It.IsAny<CreateProviderGalleryItemDto>()), Times.Never);
        fileStorageMock.Verify(s => s.SaveFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    private static ServiceAppointmentEvidencesController CreateController(
        IServiceAppointmentService appointmentService,
        IProviderGalleryService galleryService,
        IFileStorageService fileStorageService,
        Guid userId,
        string role)
    {
        var controller = new ServiceAppointmentEvidencesController(appointmentService, galleryService, fileStorageService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role)
        };

        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(claims, "TestAuth"));

        return controller;
    }

    private static IFormFile BuildFormFile(byte[] bytes, string fileName, string contentType)
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
