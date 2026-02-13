namespace ConsertaPraMim.Application.DTOs;

public record ProviderGalleryFilterDto(
    Guid? AlbumId,
    string? Category,
    Guid? ServiceRequestId);

public record ProviderGalleryServiceOptionDto(
    Guid RequestId,
    string Description,
    string Category,
    DateTime CompletedAt);

public record ProviderGalleryAlbumDto(
    Guid Id,
    string Name,
    string? Category,
    bool IsServiceAlbum,
    Guid? ServiceRequestId,
    string? ServiceLabel,
    int ItemsCount,
    string? CoverUrl,
    DateTime CreatedAt);

public record ProviderGalleryItemDto(
    Guid Id,
    Guid AlbumId,
    string AlbumName,
    Guid? ServiceRequestId,
    string? ServiceLabel,
    string FileUrl,
    string FileName,
    string ContentType,
    long SizeBytes,
    string MediaKind,
    string? Category,
    string? Caption,
    DateTime CreatedAt);

public record ProviderGalleryOverviewDto(
    ProviderGalleryFilterDto AppliedFilters,
    IReadOnlyList<ProviderGalleryServiceOptionDto> ServiceOptions,
    IReadOnlyList<ProviderGalleryAlbumDto> Albums,
    IReadOnlyList<ProviderGalleryItemDto> Items);

public record CreateProviderGalleryAlbumDto(
    string Name,
    string? Category,
    Guid? ServiceRequestId);

public record CreateProviderGalleryItemDto(
    Guid? AlbumId,
    Guid? ServiceRequestId,
    string? Category,
    string? Caption,
    string FileUrl,
    string FileName,
    string ContentType,
    long SizeBytes);
