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

    [Fact]
    public async Task GetEvidenceTimelineByServiceRequestAsync_ShouldReturnOperationalItemsInTemporalOrder()
    {
        var providerA = Guid.NewGuid();
        var providerB = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var galleryRepositoryMock = new Mock<IProviderGalleryRepository>();
        galleryRepositoryMock
            .Setup(r => r.GetItemsByServiceRequestAsync(requestId))
            .ReturnsAsync(new List<ProviderGalleryItem>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ProviderId = providerA,
                    Provider = new User { Id = providerA, Name = "Prestador A" },
                    ServiceRequestId = requestId,
                    ServiceAppointmentId = Guid.NewGuid(),
                    EvidencePhase = ServiceExecutionEvidencePhase.After,
                    FileUrl = "/uploads/provider-gallery/depois.jpg",
                    ThumbnailUrl = "/uploads/provider-gallery/depois-thumb.jpg",
                    PreviewUrl = "/uploads/provider-gallery/depois.jpg",
                    FileName = "depois.jpg",
                    ContentType = "image/jpeg",
                    MediaKind = "image",
                    Category = "Eletrica",
                    Caption = "Depois",
                    CreatedAt = now.AddMinutes(20)
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    ProviderId = providerB,
                    Provider = new User { Id = providerB, Name = "Prestador B" },
                    ServiceRequestId = requestId,
                    ServiceAppointmentId = null,
                    EvidencePhase = null,
                    FileUrl = "/uploads/provider-gallery/galeria.jpg",
                    ThumbnailUrl = null,
                    PreviewUrl = null,
                    FileName = "galeria.jpg",
                    ContentType = "image/jpeg",
                    MediaKind = "image",
                    Category = "Eletrica",
                    Caption = "Nao operacional",
                    CreatedAt = now.AddMinutes(10)
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    ProviderId = providerA,
                    Provider = new User { Id = providerA, Name = "Prestador A" },
                    ServiceRequestId = requestId,
                    ServiceAppointmentId = Guid.NewGuid(),
                    EvidencePhase = ServiceExecutionEvidencePhase.Before,
                    FileUrl = "/uploads/provider-gallery/antes.jpg",
                    ThumbnailUrl = "/uploads/provider-gallery/antes-thumb.jpg",
                    PreviewUrl = "/uploads/provider-gallery/antes.jpg",
                    FileName = "antes.jpg",
                    ContentType = "image/jpeg",
                    MediaKind = "image",
                    Category = "Eletrica",
                    Caption = "Antes",
                    CreatedAt = now
                }
            });

        var serviceRequestRepositoryMock = new Mock<IServiceRequestRepository>();
        var fileStorageMock = new Mock<IFileStorageService>();
        var service = new ProviderGalleryService(
            galleryRepositoryMock.Object,
            serviceRequestRepositoryMock.Object,
            fileStorageMock.Object);

        var result = await service.GetEvidenceTimelineByServiceRequestAsync(
            requestId,
            null,
            UserRole.Admin.ToString());

        Assert.Equal(2, result.Count);
        Assert.Equal("Before", result[0].EvidencePhase);
        Assert.Equal("After", result[1].EvidencePhase);
        Assert.Equal("Prestador A", result[0].ProviderName);
        Assert.Equal("Prestador A", result[1].ProviderName);
    }

    [Fact]
    public async Task GetEvidenceTimelineByServiceRequestAsync_ShouldReturnEmpty_WhenClientDoesNotOwnRequest()
    {
        var ownerClientId = Guid.NewGuid();
        var anotherClientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var serviceRequestRepositoryMock = new Mock<IServiceRequestRepository>();
        serviceRequestRepositoryMock
            .Setup(r => r.GetByIdAsync(requestId))
            .ReturnsAsync(new ServiceRequest
            {
                Id = requestId,
                ClientId = ownerClientId
            });

        var galleryRepositoryMock = new Mock<IProviderGalleryRepository>();
        var fileStorageMock = new Mock<IFileStorageService>();
        var service = new ProviderGalleryService(
            galleryRepositoryMock.Object,
            serviceRequestRepositoryMock.Object,
            fileStorageMock.Object);

        var result = await service.GetEvidenceTimelineByServiceRequestAsync(
            requestId,
            anotherClientId,
            UserRole.Client.ToString());

        Assert.Empty(result);
        galleryRepositoryMock.Verify(r => r.GetItemsByServiceRequestAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GetEvidenceTimelineByServiceRequestAsync_ShouldReturnEmpty_WhenProviderHasNoAcceptedProposal()
    {
        var providerId = Guid.NewGuid();
        var anotherProviderId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var serviceRequestRepositoryMock = new Mock<IServiceRequestRepository>();
        serviceRequestRepositoryMock
            .Setup(r => r.GetByIdAsync(requestId))
            .ReturnsAsync(new ServiceRequest
            {
                Id = requestId,
                Proposals =
                {
                    new Proposal
                    {
                        ProviderId = providerId,
                        Accepted = true,
                        IsInvalidated = false
                    }
                }
            });

        var galleryRepositoryMock = new Mock<IProviderGalleryRepository>();
        var fileStorageMock = new Mock<IFileStorageService>();
        var service = new ProviderGalleryService(
            galleryRepositoryMock.Object,
            serviceRequestRepositoryMock.Object,
            fileStorageMock.Object);

        var result = await service.GetEvidenceTimelineByServiceRequestAsync(
            requestId,
            anotherProviderId,
            UserRole.Provider.ToString());

        Assert.Empty(result);
        galleryRepositoryMock.Verify(r => r.GetItemsByServiceRequestAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GetEvidenceTimelineByServiceRequestAsync_ShouldAllowAdminRole()
    {
        var requestId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var providerId = Guid.NewGuid();

        var serviceRequestRepositoryMock = new Mock<IServiceRequestRepository>();
        var galleryRepositoryMock = new Mock<IProviderGalleryRepository>();
        galleryRepositoryMock
            .Setup(r => r.GetItemsByServiceRequestAsync(requestId))
            .ReturnsAsync(new List<ProviderGalleryItem>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ProviderId = providerId,
                    Provider = new User { Id = providerId, Name = "Prestador Admin View" },
                    ServiceRequestId = requestId,
                    ServiceAppointmentId = Guid.NewGuid(),
                    EvidencePhase = ServiceExecutionEvidencePhase.Before,
                    FileUrl = "/uploads/provider-gallery/admin.jpg",
                    FileName = "admin.jpg",
                    ContentType = "image/jpeg",
                    MediaKind = "image",
                    CreatedAt = now
                }
            });

        var fileStorageMock = new Mock<IFileStorageService>();
        var service = new ProviderGalleryService(
            galleryRepositoryMock.Object,
            serviceRequestRepositoryMock.Object,
            fileStorageMock.Object);

        var result = await service.GetEvidenceTimelineByServiceRequestAsync(
            requestId,
            null,
            UserRole.Admin.ToString());

        Assert.Single(result);
        galleryRepositoryMock.Verify(r => r.GetItemsByServiceRequestAsync(requestId), Times.Once);
        serviceRequestRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
    }
}
