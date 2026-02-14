using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace ConsertaPraMim.Infrastructure.Services;

public class ProviderGalleryMediaProcessor : IProviderGalleryMediaProcessor
{
    private const int MaxImageWidth = 1920;
    private const int MaxImageHeight = 1920;
    private const int ThumbnailWidth = 480;
    private const int ThumbnailHeight = 320;

    private readonly IFileStorageService _fileStorageService;

    public ProviderGalleryMediaProcessor(IFileStorageService fileStorageService)
    {
        _fileStorageService = fileStorageService;
    }

    public async Task<ProcessedProviderGalleryMediaDto> ProcessAndStoreAsync(
        Stream source,
        string originalFileName,
        string contentType,
        long originalSizeBytes,
        CancellationToken cancellationToken = default)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (string.IsNullOrWhiteSpace(originalFileName)) throw new ArgumentException("Nome do arquivo obrigatorio.", nameof(originalFileName));
        if (string.IsNullOrWhiteSpace(contentType)) throw new ArgumentException("Content-Type obrigatorio.", nameof(contentType));

        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return await ProcessImageAsync(source, originalFileName, cancellationToken);
        }

        if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        {
            return await ProcessVideoAsync(source, originalFileName, contentType, originalSizeBytes);
        }

        throw new InvalidOperationException("Tipo de midia nao suportado.");
    }

    private async Task<ProcessedProviderGalleryMediaDto> ProcessImageAsync(
        Stream source,
        string originalFileName,
        CancellationToken cancellationToken)
    {
        if (source.CanSeek)
        {
            source.Position = 0;
        }

        await using var copy = new MemoryStream();
        await source.CopyToAsync(copy, cancellationToken);
        copy.Position = 0;

        using var image = await Image.LoadAsync(copy, cancellationToken);
        image.Mutate(ctx =>
        {
            ctx.AutoOrient();
            ctx.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(MaxImageWidth, MaxImageHeight)
            });
        });

        await using var compressedImageStream = new MemoryStream();
        await image.SaveAsWebpAsync(compressedImageStream, new WebpEncoder
        {
            Quality = 82
        }, cancellationToken);
        var compressedSizeBytes = compressedImageStream.Length;
        compressedImageStream.Position = 0;

        using var thumbnailImage = image.Clone(ctx =>
            ctx.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Crop,
                Position = AnchorPositionMode.Center,
                Size = new Size(ThumbnailWidth, ThumbnailHeight)
            }));

        await using var thumbnailStream = new MemoryStream();
        await thumbnailImage.SaveAsWebpAsync(thumbnailStream, new WebpEncoder
        {
            Quality = 74
        }, cancellationToken);
        thumbnailStream.Position = 0;

        string? fileUrl = null;
        string? thumbnailUrl = null;
        try
        {
            fileUrl = await _fileStorageService.SaveFileAsync(
                compressedImageStream,
                $"{Path.GetFileNameWithoutExtension(originalFileName)}.webp",
                "provider-gallery");

            thumbnailUrl = await _fileStorageService.SaveFileAsync(
                thumbnailStream,
                $"{Path.GetFileNameWithoutExtension(originalFileName)}-thumb.webp",
                "provider-gallery");

            return new ProcessedProviderGalleryMediaDto(
                fileUrl,
                "image/webp",
                compressedSizeBytes,
                thumbnailUrl,
                null);
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(fileUrl))
            {
                _fileStorageService.DeleteFile(fileUrl);
            }

            if (!string.IsNullOrWhiteSpace(thumbnailUrl))
            {
                _fileStorageService.DeleteFile(thumbnailUrl);
            }

            throw;
        }
    }

    private async Task<ProcessedProviderGalleryMediaDto> ProcessVideoAsync(
        Stream source,
        string originalFileName,
        string contentType,
        long originalSizeBytes)
    {
        if (source.CanSeek)
        {
            source.Position = 0;
        }

        var fileUrl = await _fileStorageService.SaveFileAsync(source, originalFileName, "provider-gallery");
        var normalizedSize = originalSizeBytes > 0 ? originalSizeBytes : 1;

        return new ProcessedProviderGalleryMediaDto(
            fileUrl,
            contentType.Trim(),
            normalizedSize,
            null,
            fileUrl);
    }
}
