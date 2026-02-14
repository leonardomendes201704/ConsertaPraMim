using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IProviderGalleryMediaProcessor
{
    Task<ProcessedProviderGalleryMediaDto> ProcessAndStoreAsync(
        Stream source,
        string originalFileName,
        string contentType,
        long originalSizeBytes,
        CancellationToken cancellationToken = default);
}
