using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;

namespace ConsertaPraMim.Application.Services;

public class ProviderGalleryService : IProviderGalleryService
{
    private const int AlbumNameMaxLength = 120;
    private const int CategoryMaxLength = 80;
    private const int CaptionMaxLength = 500;
    private const string DefaultAlbumName = "Portifolio Geral";

    private readonly IProviderGalleryRepository _galleryRepository;
    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly IFileStorageService _fileStorageService;

    public ProviderGalleryService(
        IProviderGalleryRepository galleryRepository,
        IServiceRequestRepository serviceRequestRepository,
        IFileStorageService fileStorageService)
    {
        _galleryRepository = galleryRepository;
        _serviceRequestRepository = serviceRequestRepository;
        _fileStorageService = fileStorageService;
    }

    public async Task<ProviderGalleryOverviewDto> GetOverviewAsync(Guid providerId, ProviderGalleryFilterDto filter)
    {
        var normalizedCategory = NormalizeCategory(filter.Category);
        var albums = await _galleryRepository.GetAlbumsByProviderAsync(providerId);
        var items = await _galleryRepository.GetItemsByProviderAsync(providerId);
        var completedServices = await _serviceRequestRepository.GetHistoryByProviderAsync(providerId);

        var albumDtos = albums
            .OrderByDescending(a => a.CreatedAt)
            .Select(MapAlbum)
            .ToList();

        var itemDtos = items
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => MapItem(i, i.Album))
            .ToList();

        var serviceOptions = completedServices
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new ProviderGalleryServiceOptionDto(
                s.Id,
                s.Description,
                s.Category.ToPtBr(),
                s.CreatedAt))
            .ToList();

        return new ProviderGalleryOverviewDto(
            new ProviderGalleryFilterDto(filter.AlbumId, normalizedCategory, filter.ServiceRequestId),
            serviceOptions,
            albumDtos,
            itemDtos);
    }

    public async Task<ProviderGalleryAlbumDto> CreateAlbumAsync(Guid providerId, CreateProviderGalleryAlbumDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));

        var normalizedName = NormalizeAlbumName(dto.Name);
        var normalizedCategory = NormalizeCategory(dto.Category);

        ServiceRequest? relatedRequest = null;
        var isServiceAlbum = dto.ServiceRequestId.HasValue;
        if (dto.ServiceRequestId.HasValue)
        {
            relatedRequest = await EnsureProviderCanUseRequestAsync(providerId, dto.ServiceRequestId.Value, requireCompleted: true);
            var existingServiceAlbum = await _galleryRepository.GetServiceAlbumAsync(providerId, dto.ServiceRequestId.Value);
            if (existingServiceAlbum != null)
            {
                return MapAlbum(existingServiceAlbum);
            }

            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                normalizedName = BuildServiceAlbumName(relatedRequest);
            }
        }

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new InvalidOperationException("Nome do album obrigatorio.");
        }

        var album = new ProviderGalleryAlbum
        {
            ProviderId = providerId,
            ServiceRequestId = dto.ServiceRequestId,
            Name = normalizedName,
            Category = normalizedCategory,
            IsServiceAlbum = isServiceAlbum
        };

        await _galleryRepository.AddAlbumAsync(album);

        if (relatedRequest != null)
        {
            album.ServiceRequest = relatedRequest;
        }

        return MapAlbum(album);
    }

    public async Task<ProviderGalleryItemDto> AddItemAsync(Guid providerId, CreateProviderGalleryItemDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        if (string.IsNullOrWhiteSpace(dto.FileName)) throw new InvalidOperationException("Nome do arquivo obrigatorio.");
        if (string.IsNullOrWhiteSpace(dto.ContentType)) throw new InvalidOperationException("Content-Type obrigatorio.");
        if (dto.SizeBytes <= 0) throw new InvalidOperationException("Tamanho invalido.");

        if (!TryNormalizeGalleryUrl(dto.FileUrl, out var normalizedFileUrl))
        {
            throw new InvalidOperationException("URL do arquivo invalida para a galeria.");
        }

        var mediaKind = ResolveMediaKind(dto.ContentType);
        if (mediaKind != "image" && mediaKind != "video")
        {
            throw new InvalidOperationException("A galeria aceita apenas fotos e videos.");
        }

        var normalizedCategory = NormalizeCategory(dto.Category);
        var normalizedCaption = NormalizeCaption(dto.Caption);
        var evidencePhase = ParseEvidencePhase(dto.EvidencePhase);

        if (dto.ServiceAppointmentId.HasValue && !dto.ServiceRequestId.HasValue)
        {
            throw new InvalidOperationException("Para vincular a evidencia a um agendamento, informe tambem o pedido.");
        }

        ServiceRequest? relatedRequest = null;
        if (dto.ServiceRequestId.HasValue)
        {
            relatedRequest = await EnsureProviderCanUseRequestAsync(providerId, dto.ServiceRequestId.Value, requireCompleted: true);
        }

        var album = await ResolveTargetAlbumAsync(providerId, dto.AlbumId, relatedRequest, normalizedCategory);
        if (dto.ServiceRequestId.HasValue && album.ServiceRequestId.HasValue && album.ServiceRequestId != dto.ServiceRequestId.Value)
        {
            throw new InvalidOperationException("Album selecionado pertence a outro pedido.");
        }

        var item = new ProviderGalleryItem
        {
            ProviderId = providerId,
            AlbumId = album.Id,
            ServiceRequestId = dto.ServiceRequestId ?? album.ServiceRequestId,
            ServiceAppointmentId = dto.ServiceAppointmentId,
            FileUrl = normalizedFileUrl,
            FileName = dto.FileName.Trim(),
            ContentType = dto.ContentType.Trim(),
            SizeBytes = dto.SizeBytes,
            MediaKind = mediaKind,
            EvidencePhase = evidencePhase,
            Category = normalizedCategory,
            Caption = normalizedCaption
        };

        await _galleryRepository.AddItemAsync(item);
        item.Album = album;
        if (relatedRequest != null)
        {
            item.ServiceRequest = relatedRequest;
        }

        return MapItem(item, album);
    }

    public async Task<bool> DeleteItemAsync(Guid providerId, Guid itemId)
    {
        var item = await _galleryRepository.GetItemByIdAsync(itemId);
        if (item == null || item.ProviderId != providerId)
        {
            return false;
        }

        var fileUrl = item.FileUrl;
        await _galleryRepository.DeleteItemAsync(item);
        _fileStorageService.DeleteFile(fileUrl);
        return true;
    }

    private async Task<ProviderGalleryAlbum> ResolveTargetAlbumAsync(
        Guid providerId,
        Guid? albumId,
        ServiceRequest? relatedRequest,
        string? normalizedCategory)
    {
        if (albumId.HasValue)
        {
            var existingAlbum = await _galleryRepository.GetAlbumByIdAsync(albumId.Value);
            if (existingAlbum == null || existingAlbum.ProviderId != providerId)
            {
                throw new InvalidOperationException("Album nao encontrado.");
            }

            return existingAlbum;
        }

        if (relatedRequest != null)
        {
            var serviceAlbum = await _galleryRepository.GetServiceAlbumAsync(providerId, relatedRequest.Id);
            if (serviceAlbum != null)
            {
                return serviceAlbum;
            }

            var newServiceAlbum = new ProviderGalleryAlbum
            {
                ProviderId = providerId,
                ServiceRequestId = relatedRequest.Id,
                Name = BuildServiceAlbumName(relatedRequest),
                Category = normalizedCategory,
                IsServiceAlbum = true
            };

            await _galleryRepository.AddAlbumAsync(newServiceAlbum);
            newServiceAlbum.ServiceRequest = relatedRequest;
            return newServiceAlbum;
        }

        var albums = await _galleryRepository.GetAlbumsByProviderAsync(providerId);
        var generalAlbum = albums.FirstOrDefault(a =>
            a.ServiceRequestId == null &&
            a.Name.Equals(DefaultAlbumName, StringComparison.OrdinalIgnoreCase));

        if (generalAlbum != null)
        {
            return generalAlbum;
        }

        var createdGeneralAlbum = new ProviderGalleryAlbum
        {
            ProviderId = providerId,
            Name = DefaultAlbumName,
            IsServiceAlbum = false
        };

        await _galleryRepository.AddAlbumAsync(createdGeneralAlbum);
        return createdGeneralAlbum;
    }

    private async Task<ServiceRequest> EnsureProviderCanUseRequestAsync(Guid providerId, Guid requestId, bool requireCompleted)
    {
        var request = await _serviceRequestRepository.GetByIdAsync(requestId);
        if (request == null)
        {
            throw new InvalidOperationException("Pedido nao encontrado.");
        }

        var hasAcceptedProposal = request.Proposals.Any(p => p.ProviderId == providerId && p.Accepted);
        if (!hasAcceptedProposal)
        {
            throw new InvalidOperationException("Voce nao pode anexar midias neste pedido.");
        }

        if (requireCompleted &&
            request.Status != ServiceRequestStatus.Completed &&
            request.Status != ServiceRequestStatus.Validated)
        {
            throw new InvalidOperationException("Apenas pedidos executados podem receber fotos e videos da galeria.");
        }

        return request;
    }

    private static ProviderGalleryAlbumDto MapAlbum(ProviderGalleryAlbum album)
    {
        var cover = album.Items
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => i.FileUrl)
            .FirstOrDefault();

        return new ProviderGalleryAlbumDto(
            album.Id,
            album.Name,
            album.Category,
            album.IsServiceAlbum,
            album.ServiceRequestId,
            BuildServiceLabel(album.ServiceRequest),
            album.Items.Count,
            cover,
            album.CreatedAt);
    }

    private static ProviderGalleryItemDto MapItem(ProviderGalleryItem item, ProviderGalleryAlbum? album)
    {
        var resolvedAlbum = album ?? item.Album;
        var serviceLabel = BuildServiceLabel(item.ServiceRequest ?? resolvedAlbum?.ServiceRequest);

        return new ProviderGalleryItemDto(
            item.Id,
            item.AlbumId,
            resolvedAlbum?.Name ?? "Album",
            item.ServiceRequestId ?? resolvedAlbum?.ServiceRequestId,
            item.ServiceAppointmentId,
            item.EvidencePhase?.ToString(),
            serviceLabel,
            item.FileUrl,
            item.FileName,
            item.ContentType,
            item.SizeBytes,
            item.MediaKind,
            item.Category,
            item.Caption,
            item.CreatedAt);
    }

    private static string BuildServiceAlbumName(ServiceRequest request)
    {
        var shortId = request.Id.ToString("N")[..8];
        return $"Servico {ResolveCategoryName(request)} #{shortId}";
    }

    private static string? BuildServiceLabel(ServiceRequest? request)
    {
        if (request == null)
        {
            return null;
        }

        var shortId = request.Id.ToString("N")[..8];
        return $"Pedido #{shortId} - {ResolveCategoryName(request)}";
    }

    private static string? NormalizeAlbumName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= AlbumNameMaxLength
            ? trimmed
            : trimmed[..AlbumNameMaxLength];
    }

    private static string? NormalizeCategory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = ServiceCategoryExtensions.ToPtBrOrOriginal(value);
        return trimmed.Length <= CategoryMaxLength
            ? trimmed
            : trimmed[..CategoryMaxLength];
    }

    private static string? NormalizeCaption(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= CaptionMaxLength
            ? trimmed
            : trimmed[..CaptionMaxLength];
    }

    private static string ResolveMediaKind(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return "file";
        }

        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return "image";
        }

        if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        {
            return "video";
        }

        return "file";
    }

    private static bool TryNormalizeGalleryUrl(string? fileUrl, out string normalizedUrl)
    {
        normalizedUrl = string.Empty;
        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            return false;
        }

        var trimmed = fileUrl.Trim();
        if (trimmed.StartsWith("/uploads/provider-gallery/", StringComparison.OrdinalIgnoreCase))
        {
            normalizedUrl = trimmed;
            return true;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        if (!uri.AbsolutePath.StartsWith("/uploads/provider-gallery/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        normalizedUrl = uri.AbsoluteUri;
        return true;
    }

    private static string ResolveCategoryName(ServiceRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.CategoryDefinition?.Name))
        {
            return request.CategoryDefinition.Name;
        }

        return request.Category.ToPtBr();
    }

    private static ServiceExecutionEvidencePhase? ParseEvidencePhase(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Enum.TryParse<ServiceExecutionEvidencePhase>(value.Trim(), true, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException("Fase da evidencia invalida. Use Before, During ou After.");
    }
}
