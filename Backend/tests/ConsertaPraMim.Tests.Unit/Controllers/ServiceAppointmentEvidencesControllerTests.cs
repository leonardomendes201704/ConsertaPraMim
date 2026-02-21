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
    /// <summary>
    /// Cenario: prestador tenta anexar evidencia com fase operacional inexistente.
    /// Passos: envia upload com Phase="XYZ" para um agendamento qualquer.
    /// Resultado esperado: BadRequest por fase invalida, garantindo taxonomia padronizada de evidencias.
    /// </summary>
    [Fact(DisplayName = "Servico appointment evidences controller | Upload | Deve retornar invalida requisicao quando phase invalido")]
    public async Task Upload_ShouldReturnBadRequest_WhenPhaseIsInvalid()
    {
        var serviceMock = new Mock<IServiceAppointmentService>();
        var galleryMock = new Mock<IProviderGalleryService>();
        var fileStorageMock = new Mock<IFileStorageService>();
        var mediaProcessorMock = new Mock<IProviderGalleryMediaProcessor>();

        var controller = CreateController(
            serviceMock.Object,
            galleryMock.Object,
            fileStorageMock.Object,
            mediaProcessorMock.Object,
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

    /// <summary>
    /// Cenario: prestador autorizado envia foto valida de evidencia durante agendamento em andamento.
    /// Passos: valida acesso ao appointment, processa midia, cria item de galeria e executa upload completo.
    /// Resultado esperado: resposta OK e gravacao de evidencia com fase mapeada para "Before" e links gerados.
    /// </summary>
    [Fact(DisplayName = "Servico appointment evidences controller | Upload | Deve retornar ok quando requisicao valido")]
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
                "/uploads/provider-gallery/foto-thumb.jpg",
                "/uploads/provider-gallery/foto.jpg",
                "foto.jpg",
                "image/jpeg",
                4,
                "image",
                "Hidraulica",
                "ANTES",
                DateTime.UtcNow));

        var fileStorageMock = new Mock<IFileStorageService>();
        var mediaProcessorMock = new Mock<IProviderGalleryMediaProcessor>();
        mediaProcessorMock
            .Setup(s => s.ProcessAndStoreAsync(
                It.IsAny<Stream>(),
                "foto.jpg",
                "image/jpeg",
                4,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessedProviderGalleryMediaDto(
                "/uploads/provider-gallery/foto.jpg",
                "image/jpeg",
                4,
                "/uploads/provider-gallery/foto-thumb.jpg",
                "/uploads/provider-gallery/foto.jpg"));

        var controller = CreateController(
            serviceMock.Object,
            galleryMock.Object,
            fileStorageMock.Object,
            mediaProcessorMock.Object,
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
        mediaProcessorMock.Verify(s => s.ProcessAndStoreAsync(
            It.IsAny<Stream>(),
            "foto.jpg",
            "image/jpeg",
            4,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Cenario: arquivo chega com extensao e content type divergentes.
    /// Passos: tenta enviar "foto.png" declarando MIME "image/jpeg".
    /// Resultado esperado: BadRequest para bloquear inconsistencias que podem mascarar conteudo indevido.
    /// </summary>
    [Fact(DisplayName = "Servico appointment evidences controller | Upload | Deve retornar invalida requisicao quando extension nao match content type")]
    public async Task Upload_ShouldReturnBadRequest_WhenExtensionDoesNotMatchContentType()
    {
        var serviceMock = new Mock<IServiceAppointmentService>();
        var galleryMock = new Mock<IProviderGalleryService>();
        var fileStorageMock = new Mock<IFileStorageService>();
        var mediaProcessorMock = new Mock<IProviderGalleryMediaProcessor>();

        var controller = CreateController(
            serviceMock.Object,
            galleryMock.Object,
            fileStorageMock.Object,
            mediaProcessorMock.Object,
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

    /// <summary>
    /// Cenario: imagem aparentemente valida carrega payload script injetado no binario.
    /// Passos: monta arquivo JPEG com trecho "<script>" e submete ao endpoint de evidencia.
    /// Resultado esperado: BadRequest com bloqueio no scan basico e sem persistencia em galeria/processamento.
    /// </summary>
    [Fact(DisplayName = "Servico appointment evidences controller | Upload | Deve retornar invalida requisicao quando basic scan finds suspicious content")]
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
        var mediaProcessorMock = new Mock<IProviderGalleryMediaProcessor>();

        var controller = CreateController(
            serviceMock.Object,
            galleryMock.Object,
            fileStorageMock.Object,
            mediaProcessorMock.Object,
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
        mediaProcessorMock.Verify(s => s.ProcessAndStoreAsync(
            It.IsAny<Stream>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<long>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Cenario: executavel disfarcado de imagem tenta burlar validacao apenas pelo nome/content type.
    /// Passos: envia arquivo com assinatura "MZ" (PE executavel) chamado como .jpg.
    /// Resultado esperado: BadRequest com errorCode "invalid_file_signature" e nenhuma acao de persistencia.
    /// </summary>
    [Fact(DisplayName = "Servico appointment evidences controller | Upload | Deve retornar invalida requisicao quando file signature executable disguised como jpeg")]
    public async Task Upload_ShouldReturnBadRequest_WhenFileSignatureIsExecutableDisguisedAsJpeg()
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
        var mediaProcessorMock = new Mock<IProviderGalleryMediaProcessor>();

        var controller = CreateController(
            serviceMock.Object,
            galleryMock.Object,
            fileStorageMock.Object,
            mediaProcessorMock.Object,
            providerId,
            UserRole.Provider.ToString());

        var file = BuildFormFile(new byte[] { 0x4D, 0x5A, 0x90, 0x00 }, "foto.jpg", "image/jpeg");
        var result = await controller.Upload(appointmentId, new ServiceAppointmentEvidencesController.UploadServiceAppointmentEvidenceRequest
        {
            File = file,
            Phase = "ANTES"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("invalid_file_signature", GetAnonymousProperty(badRequest.Value, "errorCode"));
        galleryMock.Verify(s => s.AddItemAsync(It.IsAny<Guid>(), It.IsAny<CreateProviderGalleryItemDto>()), Times.Never);
        mediaProcessorMock.Verify(s => s.ProcessAndStoreAsync(
            It.IsAny<Stream>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<long>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Cenario: payload textual de PowerShell embutido em arquivo de imagem.
    /// Passos: submete JPEG com string "powershell -ExecutionPolicy bypass" para upload.
    /// Resultado esperado: BadRequest com errorCode "malicious_content_detected" e bloqueio total do fluxo.
    /// </summary>
    [Fact(DisplayName = "Servico appointment evidences controller | Upload | Deve retornar invalida requisicao quando basic scan finds power shell payload")]
    public async Task Upload_ShouldReturnBadRequest_WhenBasicScanFindsPowerShellPayload()
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
        var mediaProcessorMock = new Mock<IProviderGalleryMediaProcessor>();

        var controller = CreateController(
            serviceMock.Object,
            galleryMock.Object,
            fileStorageMock.Object,
            mediaProcessorMock.Object,
            providerId,
            UserRole.Provider.ToString());

        var payload = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }
            .Concat(System.Text.Encoding.UTF8.GetBytes("powershell -ExecutionPolicy bypass"))
            .ToArray();
        var file = BuildFormFile(payload, "foto.jpg", "image/jpeg");

        var result = await controller.Upload(appointmentId, new ServiceAppointmentEvidencesController.UploadServiceAppointmentEvidenceRequest
        {
            File = file,
            Phase = "ANTES"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("malicious_content_detected", GetAnonymousProperty(badRequest.Value, "errorCode"));
        galleryMock.Verify(s => s.AddItemAsync(It.IsAny<Guid>(), It.IsAny<CreateProviderGalleryItemDto>()), Times.Never);
        mediaProcessorMock.Verify(s => s.ProcessAndStoreAsync(
            It.IsAny<Stream>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<long>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ServiceAppointmentEvidencesController CreateController(
        IServiceAppointmentService appointmentService,
        IProviderGalleryService galleryService,
        IFileStorageService fileStorageService,
        IProviderGalleryMediaProcessor mediaProcessor,
        Guid userId,
        string role)
    {
        var controller = new ServiceAppointmentEvidencesController(appointmentService, galleryService, fileStorageService, mediaProcessor)
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

    private static string? GetAnonymousProperty(object? value, string propertyName)
    {
        if (value == null)
        {
            return null;
        }

        var property = value.GetType().GetProperty(propertyName);
        return property?.GetValue(value)?.ToString();
    }
}
