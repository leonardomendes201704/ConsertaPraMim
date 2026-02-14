using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class ProviderGalleryServiceTests
{
    [Fact]
    public async Task AddItemAsync_ShouldAttachOperationalEvidenceToServiceAlbum_WhenRequestIsInProgress()
    {
        var providerId = Guid.NewGuid();
        var serviceRequestId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();

        var request = new ServiceRequest
        {
            Id = serviceRequestId,
            Category = ServiceCategory.Electrical,
            Status = ServiceRequestStatus.InProgress,
            Description = "Trocar fiacao",
            Proposals =
            {
                new Proposal
                {
                    ProviderId = providerId,
                    Accepted = true
                }
            }
        };

        var galleryRepositoryMock = new Mock<IProviderGalleryRepository>();
        var serviceRequestRepositoryMock = new Mock<IServiceRequestRepository>();
        var fileStorageMock = new Mock<IFileStorageService>();

        serviceRequestRepositoryMock
            .Setup(r => r.GetByIdAsync(serviceRequestId))
            .ReturnsAsync(request);

        galleryRepositoryMock
            .Setup(r => r.GetServiceAlbumAsync(providerId, serviceRequestId))
            .ReturnsAsync((ProviderGalleryAlbum?)null);

        ProviderGalleryAlbum? createdAlbum = null;
        galleryRepositoryMock
            .Setup(r => r.AddAlbumAsync(It.IsAny<ProviderGalleryAlbum>()))
            .Callback<ProviderGalleryAlbum>(album => createdAlbum = album)
            .Returns(Task.CompletedTask);

        ProviderGalleryItem? createdItem = null;
        galleryRepositoryMock
            .Setup(r => r.AddItemAsync(It.IsAny<ProviderGalleryItem>()))
            .Callback<ProviderGalleryItem>(item => createdItem = item)
            .Returns(Task.CompletedTask);

        var service = new ProviderGalleryService(
            galleryRepositoryMock.Object,
            serviceRequestRepositoryMock.Object,
            fileStorageMock.Object);

        var result = await service.AddItemAsync(
            providerId,
            new CreateProviderGalleryItemDto(
                AlbumId: null,
                ServiceRequestId: serviceRequestId,
                Category: null,
                Caption: "Antes da execucao",
                FileUrl: "/uploads/provider-gallery/evidencia.jpg",
                ThumbnailUrl: "/uploads/provider-gallery/evidencia-thumb.jpg",
                PreviewUrl: "/uploads/provider-gallery/evidencia.jpg",
                FileName: "evidencia.jpg",
                ContentType: "image/jpeg",
                SizeBytes: 1200,
                ServiceAppointmentId: appointmentId,
                EvidencePhase: "Before"));

        Assert.NotNull(createdAlbum);
        Assert.True(createdAlbum!.IsServiceAlbum);
        Assert.Equal(serviceRequestId, createdAlbum.ServiceRequestId);
        Assert.StartsWith("Servico Eletrica #", createdAlbum.Name, StringComparison.Ordinal);

        Assert.NotNull(createdItem);
        Assert.Equal(createdAlbum.Id, createdItem!.AlbumId);
        Assert.Equal(serviceRequestId, createdItem.ServiceRequestId);
        Assert.Equal(appointmentId, createdItem.ServiceAppointmentId);
        Assert.Equal(ServiceExecutionEvidencePhase.Before, createdItem.EvidencePhase);
        Assert.Equal("Eletrica", createdItem.Category);

        Assert.Equal(createdAlbum.Name, result.AlbumName);
        Assert.Equal("Before", result.EvidencePhase);
        Assert.Equal(serviceRequestId, result.ServiceRequestId);

        galleryRepositoryMock.Verify(r => r.AddAlbumAsync(It.IsAny<ProviderGalleryAlbum>()), Times.Once);
        galleryRepositoryMock.Verify(r => r.AddItemAsync(It.IsAny<ProviderGalleryItem>()), Times.Once);
    }

    [Fact]
    public async Task AddItemAsync_ShouldKeepCompletionRule_ForRegularGalleryUploadWithoutOperationalEvidence()
    {
        var providerId = Guid.NewGuid();
        var serviceRequestId = Guid.NewGuid();

        var request = new ServiceRequest
        {
            Id = serviceRequestId,
            Category = ServiceCategory.Plumbing,
            Status = ServiceRequestStatus.InProgress,
            Description = "Vazamento na pia",
            Proposals =
            {
                new Proposal
                {
                    ProviderId = providerId,
                    Accepted = true
                }
            }
        };

        var galleryRepositoryMock = new Mock<IProviderGalleryRepository>();
        var serviceRequestRepositoryMock = new Mock<IServiceRequestRepository>();
        var fileStorageMock = new Mock<IFileStorageService>();

        serviceRequestRepositoryMock
            .Setup(r => r.GetByIdAsync(serviceRequestId))
            .ReturnsAsync(request);

        var service = new ProviderGalleryService(
            galleryRepositoryMock.Object,
            serviceRequestRepositoryMock.Object,
            fileStorageMock.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddItemAsync(
                providerId,
                new CreateProviderGalleryItemDto(
                    AlbumId: null,
                    ServiceRequestId: serviceRequestId,
                    Category: null,
                    Caption: "Foto da galeria",
                    FileUrl: "/uploads/provider-gallery/galeria.jpg",
                    ThumbnailUrl: "/uploads/provider-gallery/galeria-thumb.jpg",
                    PreviewUrl: "/uploads/provider-gallery/galeria.jpg",
                    FileName: "galeria.jpg",
                    ContentType: "image/jpeg",
                    SizeBytes: 800,
                    ServiceAppointmentId: null,
                    EvidencePhase: null)));

        galleryRepositoryMock.Verify(r => r.AddAlbumAsync(It.IsAny<ProviderGalleryAlbum>()), Times.Never);
        galleryRepositoryMock.Verify(r => r.AddItemAsync(It.IsAny<ProviderGalleryItem>()), Times.Never);
    }
}
