using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Web.Client.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Client.Controllers;

[Authorize(Roles = "Client")]
public class ProfileController : Controller
{
    private const long MaxProfilePictureSizeBytes = 5_000_000;
    private static readonly HashSet<string> AllowedProfileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };
    private static readonly HashSet<string> AllowedProfileContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private readonly IProfileService _profileService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        IProfileService profileService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ProfileController> logger)
    {
        _profileService = profileService;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var profile = await _profileService.GetProfileAsync(userId);
        if (profile == null)
        {
            TempData["Error"] = "Nao foi possivel carregar seu perfil.";
            return RedirectToAction("Index", "Home");
        }

        return View(profile);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePicture(IFormFile? profilePicture)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        if (profilePicture == null || profilePicture.Length == 0)
        {
            TempData["Error"] = "Selecione uma imagem valida para continuar.";
            return RedirectToAction(nameof(Index));
        }

        if (profilePicture.Length > MaxProfilePictureSizeBytes)
        {
            TempData["Error"] = "A imagem deve ter no maximo 5MB.";
            return RedirectToAction(nameof(Index));
        }

        var extension = Path.GetExtension(profilePicture.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedProfileExtensions.Contains(extension))
        {
            TempData["Error"] = "Formato de arquivo nao permitido. Use JPG, PNG ou WEBP.";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(profilePicture.ContentType) || !AllowedProfileContentTypes.Contains(profilePicture.ContentType))
        {
            TempData["Error"] = "Tipo de imagem nao permitido.";
            return RedirectToAction(nameof(Index));
        }

        await using (var signatureStream = profilePicture.OpenReadStream())
        {
            if (!IsSupportedImageSignature(signatureStream))
            {
                TempData["Error"] = "Arquivo invalido. Envie uma imagem JPG, PNG ou WEBP.";
                return RedirectToAction(nameof(Index));
            }
        }

        var uploadResult = await UploadProfilePictureAsync(profilePicture, HttpContext.RequestAborted);
        if (!uploadResult.Success || string.IsNullOrWhiteSpace(uploadResult.ImageUrl))
        {
            TempData["Error"] = uploadResult.ErrorMessage ?? "Nao foi possivel enviar a imagem.";
            return RedirectToAction(nameof(Index));
        }

        var success = await _profileService.UpdateProfilePictureAsync(userId, uploadResult.ImageUrl);
        TempData[success ? "Success" : "Error"] = success
            ? "Foto de perfil atualizada com sucesso."
            : "Nao foi possivel atualizar a foto de perfil.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemovePicture()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var success = await _profileService.UpdateProfilePictureAsync(userId, string.Empty);
        TempData[success ? "Success" : "Error"] = success
            ? "Foto de perfil removida. Exibindo avatar com iniciais."
            : "Nao foi possivel remover a foto de perfil.";

        return RedirectToAction(nameof(Index));
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var userIdRaw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdRaw, out userId);
    }

    private async Task<UploadResult> UploadProfilePictureAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var apiToken = User.FindFirst(WebClientClaimTypes.ApiToken)?.Value;
        if (string.IsNullOrWhiteSpace(apiToken))
        {
            return UploadResult.Fail("Sessao expirada. Faca login novamente.");
        }

        var apiBaseUrl = _configuration["ApiBaseUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            return UploadResult.Fail("ApiBaseUrl nao configurada no portal cliente.");
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{apiBaseUrl.TrimEnd('/')}/api/files/upload");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

            using var form = new MultipartFormDataContent();
            form.Add(new StringContent("profiles"), "Folder");

            await using var stream = file.OpenReadStream();
            using var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            form.Add(fileContent, "File", Path.GetFileName(file.FileName));
            request.Content = form;

            using var response = await client.SendAsync(request, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return UploadResult.Fail(string.IsNullOrWhiteSpace(raw)
                    ? "Falha ao enviar imagem para a API."
                    : raw);
            }

            var payload = JsonSerializer.Deserialize<FileUploadResponse>(raw, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var uploadedUrl = payload?.AbsoluteUrl ?? payload?.RelativeUrl;
            if (string.IsNullOrWhiteSpace(uploadedUrl))
            {
                return UploadResult.Fail("A API nao retornou a URL da imagem.");
            }

            return UploadResult.Ok(uploadedUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao enviar foto de perfil no portal cliente.");
            return UploadResult.Fail("Falha de comunicacao ao enviar a imagem.");
        }
    }

    private static bool IsSupportedImageSignature(Stream stream)
    {
        if (!stream.CanRead || !stream.CanSeek)
        {
            return false;
        }

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

    private sealed record FileUploadResponse(string? RelativeUrl, string? AbsoluteUrl);

    private sealed record UploadResult(bool Success, string? ImageUrl, string? ErrorMessage)
    {
        public static UploadResult Ok(string imageUrl) => new(true, imageUrl, null);
        public static UploadResult Fail(string errorMessage) => new(false, null, errorMessage);
    }
}
