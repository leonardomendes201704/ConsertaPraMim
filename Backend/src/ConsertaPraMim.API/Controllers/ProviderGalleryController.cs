using System.Security.Claims;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Roles = "Provider")]
[ApiController]
[Route("api/provider-gallery")]
public class ProviderGalleryController : ControllerBase
{
    private readonly IProviderGalleryService _providerGalleryService;
    private readonly IProviderGalleryMediaProcessor _providerGalleryMediaProcessor;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public ProviderGalleryController(
        IProviderGalleryService providerGalleryService,
        IProviderGalleryMediaProcessor providerGalleryMediaProcessor,
        IWebHostEnvironment webHostEnvironment)
    {
        _providerGalleryService = providerGalleryService;
        _providerGalleryMediaProcessor = providerGalleryMediaProcessor;
        _webHostEnvironment = webHostEnvironment;
    }

    [HttpGet]
    public async Task<IActionResult> GetOverview(
        [FromQuery] Guid? albumId,
        [FromQuery] Guid? serviceRequestId,
        [FromQuery] string? category)
    {
        if (!TryGetProviderId(out var providerId))
        {
            return Unauthorized();
        }

        var overview = await _providerGalleryService.GetOverviewAsync(
            providerId,
            new ProviderGalleryFilterDto(albumId, category, serviceRequestId));
        return Ok(overview);
    }

    [HttpPost("albums")]
    public async Task<IActionResult> CreateAlbum([FromBody] CreateProviderGalleryAlbumDto dto)
    {
        if (!TryGetProviderId(out var providerId))
        {
            return Unauthorized();
        }

        var album = await _providerGalleryService.CreateAlbumAsync(providerId, dto);
        return Ok(album);
    }

    [HttpPost("items")]
    public async Task<IActionResult> AddItem([FromBody] CreateProviderGalleryItemDto dto)
    {
        if (!TryGetProviderId(out var providerId))
        {
            return Unauthorized();
        }

        var item = await _providerGalleryService.AddItemAsync(providerId, dto);
        return Ok(item);
    }

    [HttpDelete("items/{itemId:guid}")]
    public async Task<IActionResult> DeleteItem(Guid itemId)
    {
        if (!TryGetProviderId(out var providerId))
        {
            return Unauthorized();
        }

        var deleted = await _providerGalleryService.DeleteItemAsync(providerId, itemId);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("process-media")]
    [RequestSizeLimit(120_000_000)]
    public async Task<IActionResult> ProcessMedia([FromForm] ProviderGalleryProcessMediaRequest request, CancellationToken cancellationToken)
    {
        if (request.File is not { Length: > 0 })
        {
            return BadRequest(new { message = "Arquivo obrigatorio." });
        }

        await using var stream = request.File.OpenReadStream();
        var processed = await _providerGalleryMediaProcessor.ProcessAndStoreAsync(
            stream,
            request.File.FileName,
            request.File.ContentType,
            request.File.Length,
            cancellationToken);

        return Ok(processed);
    }

    /// <summary>
    /// Retorna todas as fotos de todos os albuns de um prestador em Base64.
    /// </summary>
    /// <remarks>
    /// Regras principais:
    /// - Endpoint publico (nao exige autenticacao).
    /// - Inclui apenas itens de imagem (`image/*`) da galeria do prestador.
    /// - Fotos cujo arquivo fisico nao for encontrado sao contabilizadas em `unavailablePhotosCount`.
    /// </remarks>
    /// <param name="providerId">Identificador do prestador.</param>
    /// <response code="200">Fotos carregadas com sucesso.</response>
    /// <response code="400">Prestador invalido.</response>
    [HttpGet("public/providers/{providerId:guid}/albums/photos/base64")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PublicProviderGalleryPhotosBase64ResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPublicProviderAlbumPhotosBase64(Guid providerId)
    {
        if (providerId == Guid.Empty)
        {
            return BadRequest(new { errorCode = "invalid_provider", message = "Prestador invalido." });
        }

        var overview = await _providerGalleryService.GetOverviewAsync(
            providerId,
            new ProviderGalleryFilterDto(AlbumId: null, Category: null, ServiceRequestId: null));

        var albumsById = overview.Albums
            .ToDictionary(album => album.Id, album => album);

        var imageItems = overview.Items
            .Where(IsImageItem)
            .OrderBy(item => item.CreatedAt)
            .ToList();

        var unavailablePhotosCount = 0;
        var albumResponses = new List<PublicProviderGalleryAlbumPhotosBase64Dto>();

        foreach (var group in imageItems.GroupBy(item => item.AlbumId).OrderBy(group => group.Key))
        {
            albumsById.TryGetValue(group.Key, out var albumMetadata);
            var photos = new List<PublicProviderGalleryPhotoBase64Dto>();

            foreach (var item in group)
            {
                if (!TryResolveGalleryAbsolutePath(item.FileUrl, out var absolutePath))
                {
                    unavailablePhotosCount++;
                    continue;
                }

                byte[] fileBytes;
                try
                {
                    fileBytes = await System.IO.File.ReadAllBytesAsync(absolutePath);
                }
                catch
                {
                    unavailablePhotosCount++;
                    continue;
                }

                photos.Add(new PublicProviderGalleryPhotoBase64Dto(
                    item.Id,
                    item.FileName,
                    item.ContentType,
                    Convert.ToBase64String(fileBytes),
                    item.Caption,
                    item.CreatedAt));
            }

            albumResponses.Add(new PublicProviderGalleryAlbumPhotosBase64Dto(
                group.Key,
                albumMetadata?.Name ?? group.First().AlbumName,
                albumMetadata?.Category,
                photos));
        }

        var response = new PublicProviderGalleryPhotosBase64ResponseDto(
            providerId,
            albumResponses,
            TotalPhotos: albumResponses.Sum(album => album.Photos.Count),
            UnavailablePhotosCount: unavailablePhotosCount,
            GeneratedAtUtc: DateTime.UtcNow);

        return Ok(response);
    }

    private bool TryGetProviderId(out Guid providerId)
    {
        providerId = Guid.Empty;
        var providerRaw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return !string.IsNullOrWhiteSpace(providerRaw) && Guid.TryParse(providerRaw, out providerId);
    }

    private static bool IsImageItem(ProviderGalleryItemDto item)
    {
        if (item == null)
        {
            return false;
        }

        if (item.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return item.MediaKind.Equals("image", StringComparison.OrdinalIgnoreCase);
    }

    private bool TryResolveGalleryAbsolutePath(string fileUrl, out string absolutePath)
    {
        absolutePath = string.Empty;
        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            return false;
        }

        var normalizedPath = NormalizeGalleryRelativePath(fileUrl);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return false;
        }

        var webRootPath = ResolveWebRootPath();
        var fullWebRootPath = Path.GetFullPath(webRootPath);
        if (!fullWebRootPath.EndsWith(Path.DirectorySeparatorChar))
        {
            fullWebRootPath += Path.DirectorySeparatorChar;
        }

        var sanitizedRelative = normalizedPath.TrimStart('/')
            .Replace('/', Path.DirectorySeparatorChar);
        var candidatePath = Path.GetFullPath(Path.Combine(webRootPath, sanitizedRelative));

        if (!candidatePath.StartsWith(fullWebRootPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!System.IO.File.Exists(candidatePath))
        {
            return false;
        }

        absolutePath = candidatePath;
        return true;
    }

    private static string? NormalizeGalleryRelativePath(string rawPath)
    {
        var trimmed = rawPath.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            trimmed = uri.AbsolutePath;
        }

        if (!trimmed.StartsWith("/uploads/provider-gallery/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return trimmed;
    }

    private string ResolveWebRootPath()
    {
        if (!string.IsNullOrWhiteSpace(_webHostEnvironment.WebRootPath))
        {
            return _webHostEnvironment.WebRootPath;
        }

        return Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot");
    }

    public class ProviderGalleryProcessMediaRequest
    {
        public IFormFile? File { get; set; }
    }
}
