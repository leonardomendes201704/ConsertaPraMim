using System.Security.Claims;
using System.Text;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize]
[ApiController]
[Route("api/service-appointments/{appointmentId:guid}/evidences")]
public class ServiceAppointmentEvidencesController : ControllerBase
{
    private const long MaxFileSizeBytes = 25_000_000;
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "video/mp4",
        "video/webm",
        "video/quicktime"
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".mp4",
        ".webm",
        ".mov"
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> ExtensionsByContentType =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg" },
            ["image/png"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png" },
            ["image/webp"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".webp" },
            ["video/mp4"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4" },
            ["video/webm"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".webm" },
            ["video/quicktime"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mov" }
        };

    private readonly IServiceAppointmentService _serviceAppointmentService;
    private readonly IProviderGalleryService _providerGalleryService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IProviderGalleryMediaProcessor _galleryMediaProcessor;

    public ServiceAppointmentEvidencesController(
        IServiceAppointmentService serviceAppointmentService,
        IProviderGalleryService providerGalleryService,
        IFileStorageService fileStorageService,
        IProviderGalleryMediaProcessor galleryMediaProcessor)
    {
        _serviceAppointmentService = serviceAppointmentService;
        _providerGalleryService = providerGalleryService;
        _fileStorageService = fileStorageService;
        _galleryMediaProcessor = galleryMediaProcessor;
    }

    [HttpPost]
    [RequestSizeLimit(120_000_000)]
    [Authorize(Roles = "Provider,Admin")]
    public async Task<IActionResult> Upload(Guid appointmentId, [FromForm] UploadServiceAppointmentEvidenceRequest request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        if (request.File is not { Length: > 0 })
        {
            return BadRequest(new { errorCode = "invalid_file", message = "Arquivo obrigatorio." });
        }

        var validationError = ValidateFile(request.File);
        if (validationError != null)
        {
            return BadRequest(new { errorCode = "invalid_file", message = validationError });
        }

        var phase = NormalizePhase(request.Phase);
        if (phase == null)
        {
            return BadRequest(new { errorCode = "invalid_phase", message = "Fase invalida. Use ANTES, DURANTE ou DEPOIS." });
        }

        var appointmentResult = await _serviceAppointmentService.GetByIdAsync(actorUserId, actorRole, appointmentId);
        if (!appointmentResult.Success || appointmentResult.Appointment == null)
        {
            return appointmentResult.ErrorCode switch
            {
                "appointment_not_found" => NotFound(new { errorCode = appointmentResult.ErrorCode, message = appointmentResult.ErrorMessage }),
                "forbidden" => Forbid(),
                _ => BadRequest(new { errorCode = appointmentResult.ErrorCode, message = appointmentResult.ErrorMessage })
            };
        }

        var appointment = appointmentResult.Appointment;
        var providerId = IsAdminRole(actorRole) ? appointment.ProviderId : actorUserId;
        var file = request.File;

        await using var stream = file.OpenReadStream();
        if (!IsSupportedSignature(stream, file.ContentType))
        {
            return BadRequest(new { errorCode = "invalid_file_signature", message = "Assinatura do arquivo invalida para o tipo informado." });
        }

        var scanError = RunBasicSecurityScan(stream);
        if (scanError != null)
        {
            return BadRequest(new { errorCode = "malicious_content_detected", message = scanError });
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

            var created = await _providerGalleryService.AddItemAsync(providerId, new CreateProviderGalleryItemDto(
                AlbumId: request.AlbumId,
                ServiceRequestId: appointment.ServiceRequestId,
                Category: request.Category,
                Caption: request.Caption,
                FileUrl: processedMedia.FileUrl,
                ThumbnailUrl: processedMedia.ThumbnailUrl,
                PreviewUrl: processedMedia.PreviewUrl,
                FileName: Path.GetFileName(file.FileName),
                ContentType: processedMedia.ContentType,
                SizeBytes: processedMedia.SizeBytes,
                ServiceAppointmentId: appointmentId,
                EvidencePhase: phase));

            return Ok(new
            {
                success = true,
                item = created
            });
        }
        catch (Exception ex)
        {
            if (processedMedia != null)
            {
                DeleteProcessedMedia(processedMedia);
            }

            return BadRequest(new { errorCode = "upload_failed", message = ex.Message });
        }
    }

    private bool TryGetActor(out Guid actorUserId, out string actorRole)
    {
        actorUserId = Guid.Empty;
        actorRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

        var actorRaw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return !string.IsNullOrWhiteSpace(actorRaw) && Guid.TryParse(actorRaw, out actorUserId);
    }

    private static bool IsAdminRole(string actorRole)
    {
        return string.Equals(actorRole, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizePhase(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().ToUpperInvariant();
        return normalized switch
        {
            "ANTES" or "BEFORE" => ServiceExecutionEvidencePhase.Before.ToString(),
            "DURANTE" or "DURING" => ServiceExecutionEvidencePhase.During.ToString(),
            "DEPOIS" or "AFTER" => ServiceExecutionEvidencePhase.After.ToString(),
            _ => null
        };
    }

    private static string? ValidateFile(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return "Arquivo vazio.";
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return "Arquivo excede o limite de 25MB.";
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            return "Extensao nao permitida.";
        }

        if (string.IsNullOrWhiteSpace(file.ContentType) || !AllowedContentTypes.Contains(file.ContentType))
        {
            return "Tipo de arquivo nao permitido.";
        }

        if (!ExtensionsByContentType.TryGetValue(file.ContentType, out var expectedExtensions) ||
            !expectedExtensions.Contains(extension))
        {
            return "Extensao nao corresponde ao tipo do arquivo.";
        }

        return null;
    }

    private static string? RunBasicSecurityScan(Stream stream)
    {
        if (!stream.CanRead)
        {
            return "Arquivo ilegivel.";
        }

        var currentPosition = stream.Position;
        try
        {
            const int maxScanBytes = 64 * 1024;
            var buffer = new byte[maxScanBytes];
            var bytesRead = stream.Read(buffer, 0, buffer.Length);
            if (bytesRead <= 0)
            {
                return "Arquivo vazio.";
            }

            if (LooksLikeExecutableOrArchive(buffer, bytesRead))
            {
                return "Conteudo potencialmente malicioso detectado pelo scan basico.";
            }

            var text = Encoding.UTF8.GetString(buffer, 0, bytesRead).ToLowerInvariant();
            if (text.Contains("<script", StringComparison.Ordinal) ||
                text.Contains("<?php", StringComparison.Ordinal) ||
                text.Contains("powershell", StringComparison.Ordinal) ||
                text.Contains("cmd.exe", StringComparison.Ordinal) ||
                text.Contains("javascript:", StringComparison.Ordinal))
            {
                return "Conteudo suspeito detectado pelo scan basico.";
            }

            return null;
        }
        finally
        {
            stream.Position = currentPosition;
        }
    }

    private static bool LooksLikeExecutableOrArchive(byte[] bytes, int length)
    {
        if (length >= 2 && bytes[0] == 0x4D && bytes[1] == 0x5A) // MZ (EXE/DLL)
        {
            return true;
        }

        if (length >= 4 && bytes[0] == 0x7F && bytes[1] == 0x45 && bytes[2] == 0x4C && bytes[3] == 0x46) // ELF
        {
            return true;
        }

        if (length >= 4 && bytes[0] == 0x50 && bytes[1] == 0x4B && bytes[2] == 0x03 && bytes[3] == 0x04) // ZIP
        {
            return true;
        }

        if (length >= 4 && bytes[0] == 0x52 && bytes[1] == 0x61 && bytes[2] == 0x72 && bytes[3] == 0x21) // RAR
        {
            return true;
        }

        return false;
    }

    private static bool IsSupportedSignature(Stream stream, string contentType)
    {
        if (!stream.CanRead)
        {
            return false;
        }

        var buffer = new byte[16];
        var bytesRead = stream.Read(buffer, 0, buffer.Length);
        stream.Position = 0;

        if (bytesRead < 4)
        {
            return false;
        }

        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            if (contentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase))
            {
                return bytesRead >= 2 && buffer[0] == 0xFF && buffer[1] == 0xD8;
            }

            if (contentType.Equals("image/png", StringComparison.OrdinalIgnoreCase))
            {
                return bytesRead >= 8 &&
                       buffer[0] == 0x89 &&
                       buffer[1] == 0x50 &&
                       buffer[2] == 0x4E &&
                       buffer[3] == 0x47 &&
                       buffer[4] == 0x0D &&
                       buffer[5] == 0x0A &&
                       buffer[6] == 0x1A &&
                       buffer[7] == 0x0A;
            }

            if (contentType.Equals("image/webp", StringComparison.OrdinalIgnoreCase))
            {
                return bytesRead >= 12 &&
                       buffer[0] == 0x52 &&
                       buffer[1] == 0x49 &&
                       buffer[2] == 0x46 &&
                       buffer[3] == 0x46 &&
                       buffer[8] == 0x57 &&
                       buffer[9] == 0x45 &&
                       buffer[10] == 0x42 &&
                       buffer[11] == 0x50;
            }
        }

        if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        {
            if (contentType.Equals("video/mp4", StringComparison.OrdinalIgnoreCase) ||
                contentType.Equals("video/quicktime", StringComparison.OrdinalIgnoreCase))
            {
                return bytesRead >= 8 &&
                       buffer[4] == 0x66 &&
                       buffer[5] == 0x74 &&
                       buffer[6] == 0x79 &&
                       buffer[7] == 0x70;
            }

            if (contentType.Equals("video/webm", StringComparison.OrdinalIgnoreCase))
            {
                return bytesRead >= 4 &&
                       buffer[0] == 0x1A &&
                       buffer[1] == 0x45 &&
                       buffer[2] == 0xDF &&
                       buffer[3] == 0xA3;
            }
        }

        return false;
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

    public sealed class UploadServiceAppointmentEvidenceRequest
    {
        public IFormFile? File { get; set; }
        public Guid? AlbumId { get; set; }
        public string? Phase { get; set; }
        public string? Category { get; set; }
        public string? Caption { get; set; }
    }
}
