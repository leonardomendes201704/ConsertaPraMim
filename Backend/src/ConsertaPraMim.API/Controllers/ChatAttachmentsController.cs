using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ConsertaPraMim.API.Controllers;

[Authorize]
[ApiController]
[Route("api/chat-attachments")]
public class ChatAttachmentsController : ControllerBase
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".mp4", ".webm", ".mov"
    };

    private readonly IFileStorageService _fileStorageService;
    private readonly IChatService _chatService;

    public ChatAttachmentsController(IFileStorageService fileStorageService, IChatService chatService)
    {
        _fileStorageService = fileStorageService;
        _chatService = chatService;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> Upload([FromForm] ChatAttachmentUploadRequest request)
    {
        if (request.File == null || request.File.Length == 0)
        {
            return BadRequest("Arquivo obrigatorio.");
        }

        if (!AllowedExtensions.Contains(Path.GetExtension(request.File.FileName)))
        {
            return BadRequest("Tipo de arquivo nao suportado.");
        }

        if (request.File.Length > 20_000_000)
        {
            return BadRequest("Arquivo excede o limite de 20MB.");
        }

        var senderIdRaw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var senderRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!Guid.TryParse(senderIdRaw, out var senderId) || string.IsNullOrWhiteSpace(senderRole))
        {
            return Unauthorized();
        }

        var allowed = await _chatService.CanAccessConversationAsync(
            request.RequestId,
            request.ProviderId,
            senderId,
            senderRole);

        if (!allowed)
        {
            return Forbid();
        }

        await using var stream = request.File.OpenReadStream();
        var relativeUrl = await _fileStorageService.SaveFileAsync(stream, request.File.FileName, "chat");
        var absoluteUrl = $"{Request.Scheme}://{Request.Host}{relativeUrl}";

        return Ok(new
        {
            fileUrl = absoluteUrl,
            fileName = request.File.FileName,
            contentType = request.File.ContentType,
            sizeBytes = request.File.Length
        });
    }

    public class ChatAttachmentUploadRequest
    {
        public Guid RequestId { get; set; }
        public Guid ProviderId { get; set; }
        public IFormFile? File { get; set; }
    }
}
