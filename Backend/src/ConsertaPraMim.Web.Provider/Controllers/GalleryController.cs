using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ConsertaPraMim.Web.Provider.Controllers;

[Authorize(Roles = "Provider")]
public class GalleryController : Controller
{
    private const long MaxFileSizeBytes = 25_000_000;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".mp4", ".webm", ".mov"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "video/mp4", "video/webm", "video/quicktime"
    };

    private readonly IProviderGalleryService _galleryService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IProviderGalleryMediaProcessor _galleryMediaProcessor;

    public GalleryController(
        IProviderGalleryService galleryService,
        IFileStorageService fileStorageService,
        IProviderGalleryMediaProcessor galleryMediaProcessor)
    {
        _galleryService = galleryService;
        _fileStorageService = fileStorageService;
        _galleryMediaProcessor = galleryMediaProcessor;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid? albumId, Guid? serviceRequestId, string? category)
    {
        if (!TryGetProviderId(out var providerId))
        {
            return RedirectToAction("Login", "Account");
        }

        var model = await _galleryService.GetOverviewAsync(
            providerId,
            new ProviderGalleryFilterDto(albumId, category, serviceRequestId));

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAlbum(string name, string? category, Guid? serviceRequestId)
    {
        if (!TryGetProviderId(out var providerId))
        {
            return RedirectToAction("Login", "Account");
        }

        try
        {
            await _galleryService.CreateAlbumAsync(
                providerId,
                new CreateProviderGalleryAlbumDto(name, category, serviceRequestId));
            TempData["Success"] = "Album criado com sucesso.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index), new { serviceRequestId, category });
    }

    [HttpPost]
    [RequestSizeLimit(120_000_000)]
    public async Task<IActionResult> Upload(
        IFormFile[] files,
        Guid? albumId,
        Guid? serviceRequestId,
        string? category,
        string? caption,
        Guid? serviceAppointmentId = null,
        string? evidencePhase = null)
    {
        if (!TryGetProviderId(out var providerId))
        {
            return RedirectToAction("Login", "Account");
        }

        if (files == null || files.Length == 0)
        {
            TempData["Error"] = "Selecione ao menos um arquivo.";
            return RedirectToAction(nameof(Index), new { albumId, serviceRequestId, category });
        }

        var uploadedCount = 0;
        var errors = new List<string>();

        foreach (var file in files)
        {
            var validationError = ValidateFile(file);
            if (validationError != null)
            {
                errors.Add($"{file.FileName}: {validationError}");
                continue;
            }

            await using var stream = file.OpenReadStream();
            if (!IsSupportedSignature(stream, file.ContentType))
            {
                errors.Add($"{file.FileName}: assinatura de arquivo invalida.");
                continue;
            }

            stream.Position = 0;
            ProcessedProviderGalleryMediaDto? processedMedia = null;
            try
            {
                processedMedia = await _galleryMediaProcessor.ProcessAndStoreAsync(
                    stream,
                    file.FileName,
                    file.ContentType,
                    file.Length);

                await _galleryService.AddItemAsync(
                    providerId,
                    new CreateProviderGalleryItemDto(
                        albumId,
                        serviceRequestId,
                        category,
                        caption,
                        processedMedia.FileUrl,
                        processedMedia.ThumbnailUrl,
                        processedMedia.PreviewUrl,
                        Path.GetFileName(file.FileName),
                        processedMedia.ContentType,
                        processedMedia.SizeBytes,
                        serviceAppointmentId,
                        evidencePhase));

                uploadedCount++;
            }
            catch (Exception ex)
            {
                if (processedMedia != null)
                {
                    DeleteProcessedMedia(processedMedia);
                }

                errors.Add($"{file.FileName}: {ex.Message}");
            }
        }

        if (uploadedCount > 0)
        {
            TempData["Success"] = uploadedCount == 1
                ? "Arquivo enviado para a galeria."
                : $"{uploadedCount} arquivos enviados para a galeria.";
        }

        if (errors.Count > 0)
        {
            TempData["Error"] = string.Join(" ", errors.Take(3)) + (errors.Count > 3 ? " ..." : string.Empty);
        }

        return RedirectToAction(nameof(Index), new { albumId, serviceRequestId, category });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(Guid itemId, Guid? albumId, Guid? serviceRequestId, string? category)
    {
        if (!TryGetProviderId(out var providerId))
        {
            return RedirectToAction("Login", "Account");
        }

        var deleted = await _galleryService.DeleteItemAsync(providerId, itemId);
        TempData[deleted ? "Success" : "Error"] = deleted
            ? "Midia removida da galeria."
            : "Nao foi possivel remover a midia.";

        return RedirectToAction(nameof(Index), new { albumId, serviceRequestId, category });
    }

    private bool TryGetProviderId(out Guid providerId)
    {
        var raw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(raw, out providerId);
    }

    private static string? ValidateFile(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return "arquivo vazio.";
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return "arquivo acima de 25MB.";
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            return "extensao nao permitida.";
        }

        if (string.IsNullOrWhiteSpace(file.ContentType) || !AllowedContentTypes.Contains(file.ContentType))
        {
            return "content-type nao permitido.";
        }

        return null;
    }

    private static bool IsSupportedSignature(Stream stream, string contentType)
    {
        if (!stream.CanRead || !stream.CanSeek)
        {
            return false;
        }

        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return IsSupportedImageSignature(stream);
        }

        if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        {
            return IsSupportedVideoSignature(stream);
        }

        return false;
    }

    private static bool IsSupportedImageSignature(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[12];
        var bytesRead = stream.Read(buffer);
        if (bytesRead < 12)
        {
            return false;
        }

        var isJpeg = buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF;
        var isPng = buffer[0] == 0x89 &&
                    buffer[1] == 0x50 &&
                    buffer[2] == 0x4E &&
                    buffer[3] == 0x47 &&
                    buffer[4] == 0x0D &&
                    buffer[5] == 0x0A &&
                    buffer[6] == 0x1A &&
                    buffer[7] == 0x0A;
        var isWebp = buffer[0] == 0x52 &&
                     buffer[1] == 0x49 &&
                     buffer[2] == 0x46 &&
                     buffer[3] == 0x46 &&
                     buffer[8] == 0x57 &&
                     buffer[9] == 0x45 &&
                     buffer[10] == 0x42 &&
                     buffer[11] == 0x50;

        return isJpeg || isPng || isWebp;
    }

    private static bool IsSupportedVideoSignature(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[16];
        var bytesRead = stream.Read(buffer);
        if (bytesRead < 12)
        {
            return false;
        }

        var isMp4OrMov = buffer[4] == 0x66 && // f
                         buffer[5] == 0x74 && // t
                         buffer[6] == 0x79 && // y
                         buffer[7] == 0x70;   // p

        var isWebm = buffer[0] == 0x1A &&
                     buffer[1] == 0x45 &&
                     buffer[2] == 0xDF &&
                     buffer[3] == 0xA3;

        return isMp4OrMov || isWebm;
    }

    private void DeleteProcessedMedia(ProcessedProviderGalleryMediaDto processedMedia)
    {
        var fileUrls = new[]
            {
                processedMedia.FileUrl,
                processedMedia.ThumbnailUrl,
                processedMedia.PreviewUrl
            }
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var fileUrl in fileUrls)
        {
            _fileStorageService.DeleteFile(fileUrl);
        }
    }
}
