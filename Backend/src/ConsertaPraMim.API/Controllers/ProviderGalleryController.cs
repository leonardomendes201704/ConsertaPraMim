using System.Security.Claims;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Roles = "Provider")]
[ApiController]
[Route("api/provider-gallery")]
public class ProviderGalleryController : ControllerBase
{
    private readonly IProviderGalleryService _providerGalleryService;
    private readonly IProviderGalleryMediaProcessor _providerGalleryMediaProcessor;

    public ProviderGalleryController(
        IProviderGalleryService providerGalleryService,
        IProviderGalleryMediaProcessor providerGalleryMediaProcessor)
    {
        _providerGalleryService = providerGalleryService;
        _providerGalleryMediaProcessor = providerGalleryMediaProcessor;
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

    private bool TryGetProviderId(out Guid providerId)
    {
        providerId = Guid.Empty;
        var providerRaw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return !string.IsNullOrWhiteSpace(providerRaw) && Guid.TryParse(providerRaw, out providerId);
    }

    public class ProviderGalleryProcessMediaRequest
    {
        public IFormFile? File { get; set; }
    }
}
