using System.Security.Claims;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize]
[ApiController]
[Route("api/files")]
public class FilesController : ControllerBase
{
    private static readonly HashSet<string> AllowedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "profiles",
        "provider-docs",
        "service-checklists",
        "chat",
        "gallery"
    };

    private readonly IFileStorageService _fileStorageService;

    public FilesController(IFileStorageService fileStorageService)
    {
        _fileStorageService = fileStorageService;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(120_000_000)]
    public async Task<IActionResult> Upload([FromForm] FileUploadRequest request)
    {
        if (request.File is not { Length: > 0 })
        {
            return BadRequest(new { message = "Arquivo obrigatorio." });
        }

        var folder = NormalizeFolder(request.Folder);
        if (folder == null)
        {
            return BadRequest(new { message = "Pasta de destino invalida." });
        }

        await using var stream = request.File.OpenReadStream();
        var relativeUrl = await _fileStorageService.SaveFileAsync(stream, request.File.FileName, folder);
        var absoluteUrl = $"{Request.Scheme}://{Request.Host}{relativeUrl}";

        return Ok(new
        {
            relativeUrl,
            absoluteUrl,
            fileName = request.File.FileName,
            contentType = request.File.ContentType,
            sizeBytes = request.File.Length
        });
    }

    [HttpDelete]
    public IActionResult Delete([FromQuery] string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return BadRequest(new { message = "filePath obrigatorio." });
        }

        _fileStorageService.DeleteFile(filePath.Trim());
        return NoContent();
    }

    private static string? NormalizeFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return null;
        }

        var normalized = folder.Trim().Replace('\\', '/');
        return AllowedFolders.Contains(normalized) ? normalized : null;
    }

    public class FileUploadRequest
    {
        public string Folder { get; set; } = string.Empty;
        public IFormFile? File { get; set; }
    }
}
