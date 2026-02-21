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
    /// <summary>
    /// Cenario: o prestador em atendimento ativo envia evidencia operacional de execucao.
    /// Passos: o teste informa requisicao em andamento com proposta aceita e adiciona item Before vinculado ao agendamento.
    /// Resultado esperado: o sistema cria album de servico quando necessario e persiste a evidencia com metadados operacionais.
    /// </summary>
    [Fact(DisplayName = "Prestador gallery servico | Add item | Deve attach operational evidence para servico album quando requisicao em progress")]
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

    /// <summary>
    /// Cenario: o prestador tenta anexar foto comum sem contexto de evidencia operacional obrigatoria.
    /// Passos: o teste envia upload sem appointment e sem phase operacional para uma requisicao em progresso.
    /// Resultado esperado: a operacao falha por regra de conclusao e nenhum album ou item eh criado.
    /// </summary>
    [Fact(DisplayName = "Prestador gallery servico | Add item | Deve keep completion rule for regular gallery upload sem operational evidence")]
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

    /// <summary>
    /// Cenario: a timeline de evidencias contem itens operacionais e nao operacionais misturados.
    /// Passos: o teste popula galeria com registros Before, After e item comum sem fase operacional.
    /// Resultado esperado: apenas evidencias operacionais sao retornadas em ordem temporal esperada para leitura administrativa.
    /// </summary>
    [Fact(DisplayName = "Prestador gallery servico | Obter evidence timeline por servico requisicao | Deve retornar operational items em temporal pedido")]
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

    /// <summary>
    /// Cenario: um cliente sem vinculo de ownership tenta consultar evidencias de uma solicitacao.
    /// Passos: o teste define requisicao com outro dono e executa a consulta com cliente diferente.
    /// Resultado esperado: a resposta vem vazia e a busca de itens na galeria nem eh disparada.
    /// </summary>
    [Fact(DisplayName = "Prestador gallery servico | Obter evidence timeline por servico requisicao | Deve retornar vazio quando cliente nao own requisicao")]
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

    /// <summary>
    /// Cenario: um prestador nao vencedor tenta acessar timeline de evidencias de uma solicitacao.
    /// Passos: o teste cadastra proposta aceita para outro prestador e solicita timeline com usuario sem aceite.
    /// Resultado esperado: o servico bloqueia o acesso retornando colecao vazia sem consultar itens da galeria.
    /// </summary>
    [Fact(DisplayName = "Prestador gallery servico | Obter evidence timeline por servico requisicao | Deve retornar vazio quando prestador tem no accepted proposal")]
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

    /// <summary>
    /// Cenario: o administrador precisa inspecionar evidencias sem depender de ownership do cliente ou aceite de proposta.
    /// Passos: o teste consulta timeline com role Admin e valida fluxo direto para repositorio da galeria.
    /// Resultado esperado: o acesso eh permitido, com retorno de itens e sem consulta de ownership da requisicao.
    /// </summary>
    [Fact(DisplayName = "Prestador gallery servico | Obter evidence timeline por servico requisicao | Deve allow admin role")]
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

    /// <summary>
    /// Cenario: a rotina de limpeza deve remover apenas evidencias antigas elegiveis por estado terminal ou orfandade.
    /// Passos: o teste injeta tres candidatos (concluido, ativo e orfao) e executa cleanup com retencao configurada.
    /// Resultado esperado: somente os elegiveis sao apagados da base e do storage, preservando item de requisicao ativa.
    /// </summary>
    [Fact(DisplayName = "Prestador gallery servico | Cleanup old operational evidences | Deve excluir only terminal ou orphan evidences")]
    public async Task CleanupOldOperationalEvidencesAsync_ShouldDeleteOnlyTerminalOrOrphanEvidences()
    {
        var now = DateTime.UtcNow;
        var olderThanUtc = now.AddDays(-180);

        var eligibleCompleted = new ProviderGalleryItem
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = Guid.NewGuid(),
            ServiceRequest = new ServiceRequest { Status = ServiceRequestStatus.Completed },
            FileUrl = "/uploads/provider-gallery/a.jpg",
            ThumbnailUrl = "/uploads/provider-gallery/a-thumb.jpg",
            PreviewUrl = "/uploads/provider-gallery/a.jpg",
            EvidencePhase = ServiceExecutionEvidencePhase.Before,
            CreatedAt = olderThanUtc.AddMinutes(-1)
        };

        var ineligibleActive = new ProviderGalleryItem
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = Guid.NewGuid(),
            ServiceRequest = new ServiceRequest { Status = ServiceRequestStatus.Matching },
            FileUrl = "/uploads/provider-gallery/b.jpg",
            ThumbnailUrl = null,
            PreviewUrl = null,
            EvidencePhase = ServiceExecutionEvidencePhase.Before,
            CreatedAt = olderThanUtc.AddMinutes(-1)
        };

        var eligibleOrphan = new ProviderGalleryItem
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = null,
            ServiceRequest = null,
            FileUrl = "/uploads/provider-gallery/c.jpg",
            ThumbnailUrl = null,
            PreviewUrl = "/uploads/provider-gallery/c-preview.jpg",
            EvidencePhase = ServiceExecutionEvidencePhase.After,
            CreatedAt = olderThanUtc.AddMinutes(-1)
        };

        var galleryRepositoryMock = new Mock<IProviderGalleryRepository>();
        galleryRepositoryMock
            .Setup(r => r.GetOperationalEvidenceCleanupCandidatesAsync(It.IsAny<DateTime>(), 200))
            .ReturnsAsync(new List<ProviderGalleryItem>
            {
                eligibleCompleted,
                ineligibleActive,
                eligibleOrphan
            });

        IReadOnlyCollection<ProviderGalleryItem>? deletedItems = null;
        galleryRepositoryMock
            .Setup(r => r.DeleteItemsAsync(It.IsAny<IReadOnlyCollection<ProviderGalleryItem>>()))
            .Callback<IReadOnlyCollection<ProviderGalleryItem>>(items => deletedItems = items)
            .Returns(Task.CompletedTask);

        var serviceRequestRepositoryMock = new Mock<IServiceRequestRepository>();
        var fileStorageMock = new Mock<IFileStorageService>();
        var service = new ProviderGalleryService(
            galleryRepositoryMock.Object,
            serviceRequestRepositoryMock.Object,
            fileStorageMock.Object);

        var result = await service.CleanupOldOperationalEvidencesAsync(180, 200);

        Assert.Equal(3, result.ScannedCount);
        Assert.Equal(2, result.DeletedCount);
        Assert.NotNull(deletedItems);
        Assert.Equal(2, deletedItems!.Count);
        Assert.Contains(deletedItems, i => i.Id == eligibleCompleted.Id);
        Assert.Contains(deletedItems, i => i.Id == eligibleOrphan.Id);
        Assert.DoesNotContain(deletedItems, i => i.Id == ineligibleActive.Id);

        fileStorageMock.Verify(f => f.DeleteFile("/uploads/provider-gallery/a.jpg"), Times.Once);
        fileStorageMock.Verify(f => f.DeleteFile("/uploads/provider-gallery/a-thumb.jpg"), Times.Once);
        fileStorageMock.Verify(f => f.DeleteFile("/uploads/provider-gallery/c.jpg"), Times.Once);
        fileStorageMock.Verify(f => f.DeleteFile("/uploads/provider-gallery/c-preview.jpg"), Times.Once);
        fileStorageMock.Verify(f => f.DeleteFile("/uploads/provider-gallery/b.jpg"), Times.Never);
    }
}
