using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Roles = "Provider")]
[ApiController]
[Route("api/provider-onboarding")]
public class ProviderOnboardingController : ControllerBase
{
    private const long MaxDocumentBytes = 10_000_000;
    private const int MaxFilenameLength = 120;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".pdf"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "application/pdf"
    };

    private readonly IProviderOnboardingService _onboardingService;
    private readonly IFileStorageService _fileStorageService;

    public ProviderOnboardingController(
        IProviderOnboardingService onboardingService,
        IFileStorageService fileStorageService)
    {
        _onboardingService = onboardingService;
        _fileStorageService = fileStorageService;
    }

    [HttpGet]
    public async Task<IActionResult> GetState()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var state = await _onboardingService.GetStateAsync(userId);
        if (state == null)
        {
            return NotFound();
        }

        return Ok(state);
    }

    [HttpPut("basic-data")]
    public async Task<IActionResult> SaveBasicData([FromBody] UpdateProviderOnboardingBasicDataDto dto)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var success = await _onboardingService.SaveBasicDataAsync(userId, dto);
        if (!success)
        {
            return BadRequest("Dados basicos invalidos.");
        }

        return NoContent();
    }

    [HttpPut("plan")]
    public async Task<IActionResult> SavePlan([FromBody] SaveProviderOnboardingPlanDto dto)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var success = await _onboardingService.SavePlanAsync(userId, dto.Plan);
        if (!success)
        {
            return BadRequest("Plano invalido. Escolha Bronze, Silver ou Gold.");
        }

        return NoContent();
    }

    [HttpPost("documents")]
    [RequestSizeLimit(MaxDocumentBytes + 1_000_000)]
    public async Task<IActionResult> UploadDocument([FromForm] UploadProviderOnboardingDocumentRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        if (request.File == null || request.File.Length == 0)
        {
            return BadRequest("Arquivo obrigatorio.");
        }

        if (request.File.Length > MaxDocumentBytes)
        {
            return BadRequest("Arquivo excede o limite de 10MB.");
        }

        var extension = Path.GetExtension(request.File.FileName);
        if (!AllowedExtensions.Contains(extension))
        {
            return BadRequest("Formato nao permitido. Use JPG, PNG ou PDF.");
        }

        if (string.IsNullOrWhiteSpace(request.File.ContentType) || !AllowedContentTypes.Contains(request.File.ContentType))
        {
            return BadRequest("Tipo MIME do arquivo nao permitido.");
        }

        var sanitizedFileName = SanitizeFileName(request.File.FileName);
        string relativeUrl;
        string hashSha256;

        await using (var hashStream = request.File.OpenReadStream())
        {
            hashSha256 = await ComputeSha256Async(hashStream);
        }

        await using (var uploadStream = request.File.OpenReadStream())
        {
            relativeUrl = await _fileStorageService.SaveFileAsync(uploadStream, sanitizedFileName, "provider-docs");
        }

        var document = await _onboardingService.AddDocumentAsync(userId, new AddProviderOnboardingDocumentDto(
            request.DocumentType,
            sanitizedFileName,
            request.File.ContentType,
            request.File.Length,
            relativeUrl,
            hashSha256));

        if (document == null)
        {
            _fileStorageService.DeleteFile(relativeUrl);
            return BadRequest("Nao foi possivel salvar o documento.");
        }

        return Ok(document);
    }

    [HttpDelete("documents/{documentId:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid documentId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _onboardingService.RemoveDocumentAsync(userId, documentId);
        if (!result.Success)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(result.FileUrl))
        {
            _fileStorageService.DeleteFile(result.FileUrl);
        }

        return NoContent();
    }

    [HttpPost("complete")]
    public async Task<IActionResult> Complete()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _onboardingService.CompleteAsync(userId);
        if (!result.Success)
        {
            return BadRequest(result.ErrorMessage);
        }

        return Ok(result);
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var userIdRaw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdRaw, out userId);
    }

    private static async Task<string> ComputeSha256Async(Stream stream)
    {
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream);
        return Convert.ToHexString(hash);
    }

    private static string SanitizeFileName(string originalName)
    {
        var fileName = Path.GetFileName(originalName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "documento";
        }

        var extension = Path.GetExtension(fileName);
        var withoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var sanitizedBaseName = Regex.Replace(withoutExtension, @"[^a-zA-Z0-9_\- ]", "_").Trim();
        if (string.IsNullOrWhiteSpace(sanitizedBaseName))
        {
            sanitizedBaseName = "documento";
        }

        if (sanitizedBaseName.Length > MaxFilenameLength)
        {
            sanitizedBaseName = sanitizedBaseName[..MaxFilenameLength];
        }

        return $"{sanitizedBaseName}{extension}";
    }

    public class UploadProviderOnboardingDocumentRequest
    {
        public ProviderDocumentType DocumentType { get; set; }
        public IFormFile? File { get; set; }
    }
}
