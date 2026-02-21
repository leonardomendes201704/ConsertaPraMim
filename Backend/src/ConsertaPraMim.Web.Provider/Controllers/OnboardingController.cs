using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Provider.Controllers;

[Authorize(Roles = "Provider")]
public class OnboardingController : Controller
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
    private readonly ILogger<OnboardingController> _logger;

    public OnboardingController(
        IProviderOnboardingService onboardingService,
        IFileStorageService fileStorageService,
        ILogger<OnboardingController> logger)
    {
        _onboardingService = onboardingService;
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int step = 1)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        var state = await _onboardingService.GetStateAsync(userId);
        if (state == null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        ViewBag.Step = Math.Clamp(step, 1, 4);
        return View(state);
    }

    [HttpPost]
    public async Task<IActionResult> SaveBasicData(string name, string phone)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return RedirectToAction("Login", "Account");
        }

        var normalizedPhone = NormalizePhone(phone);
        var success = await _onboardingService.SaveBasicDataAsync(userId, new UpdateProviderOnboardingBasicDataDto(name, normalizedPhone));
        if (!success)
        {
            TempData["Error"] = "Preencha nome e telefone para continuar.";
            return RedirectToAction("Index", new { step = 1 });
        }

        TempData["Success"] = "Dados basicos salvos.";
        return RedirectToAction("Index", new { step = 2 });
    }

    [HttpPost]
    public async Task<IActionResult> SavePlan(ProviderPlan plan)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return RedirectToAction("Login", "Account");
        }

        var success = await _onboardingService.SavePlanAsync(userId, plan);
        if (!success)
        {
            TempData["Error"] = "Escolha um plano valido para continuar.";
            return RedirectToAction("Index", new { step = 2 });
        }

        TempData["Success"] = "Plano salvo com sucesso.";
        return RedirectToAction("Index", new { step = 3 });
    }

    [HttpPost]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxDocumentBytes + 1_000_000)]
    [RequestSizeLimit(MaxDocumentBytes + 1_000_000)]
    public async Task<IActionResult> UploadDocument(ProviderDocumentType documentType, IFormFile? file)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return RedirectToAction("Login", "Account");
        }

        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Selecione um documento para upload.";
            return RedirectToAction("Index", new { step = 3 });
        }

        if (file.Length > MaxDocumentBytes)
        {
            TempData["Error"] = "Arquivo excede o limite de 10MB.";
            return RedirectToAction("Index", new { step = 3 });
        }

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
        {
            TempData["Error"] = "Formato nao permitido. Use JPG, PNG ou PDF.";
            return RedirectToAction("Index", new { step = 3 });
        }

        if (string.IsNullOrWhiteSpace(file.ContentType) || !AllowedContentTypes.Contains(file.ContentType))
        {
            TempData["Error"] = "Tipo de arquivo nao permitido.";
            return RedirectToAction("Index", new { step = 3 });
        }

        string? relativeUrl = null;

        try
        {
            var sanitizedFileName = SanitizeFileName(file.FileName);
            string hashSha256;

            await using (var hashStream = file.OpenReadStream())
            {
                hashSha256 = await ComputeSha256Async(hashStream);
            }

            await using (var uploadStream = file.OpenReadStream())
            {
                relativeUrl = await _fileStorageService.SaveFileAsync(uploadStream, sanitizedFileName, "provider-docs");
            }

            var savedDocument = await _onboardingService.AddDocumentAsync(userId, new AddProviderOnboardingDocumentDto(
                documentType,
                sanitizedFileName,
                file.ContentType,
                file.Length,
                relativeUrl,
                hashSha256));

            if (savedDocument == null)
            {
                if (!string.IsNullOrWhiteSpace(relativeUrl))
                {
                    _fileStorageService.DeleteFile(relativeUrl);
                }

                TempData["Error"] = "Nao foi possivel salvar o documento.";
                return RedirectToAction("Index", new { step = 3 });
            }

            TempData["Success"] = "Documento enviado com sucesso.";
            return RedirectToAction("Index", new { step = 3 });
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(relativeUrl))
            {
                _fileStorageService.DeleteFile(relativeUrl);
            }

            _logger.LogError(ex, "Falha ao enviar documento de onboarding. UserId={UserId}, DocumentType={DocumentType}", userId, documentType);
            TempData["Error"] = BuildUploadErrorMessage(ex);
            return RedirectToAction("Index", new { step = 3 });
        }
    }

    [HttpPost]
    public async Task<IActionResult> RemoveDocument(Guid documentId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return RedirectToAction("Login", "Account");
        }

        var result = await _onboardingService.RemoveDocumentAsync(userId, documentId);
        if (!result.Success)
        {
            TempData["Error"] = "Documento nao encontrado.";
            return RedirectToAction("Index", new { step = 3 });
        }

        if (!string.IsNullOrWhiteSpace(result.FileUrl))
        {
            _fileStorageService.DeleteFile(result.FileUrl);
        }

        TempData["Success"] = "Documento removido.";
        return RedirectToAction("Index", new { step = 3 });
    }

    [HttpPost]
    public async Task<IActionResult> Complete()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return RedirectToAction("Login", "Account");
        }

        var result = await _onboardingService.CompleteAsync(userId);
        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage ?? "Nao foi possivel concluir o onboarding.";
            return RedirectToAction("Index", new { step = 4 });
        }

        TempData["Success"] = "Onboarding concluido com sucesso!";
        return RedirectToAction("Index", "Home");
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

    private static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return string.Empty;
        }

        return Regex.Replace(phone, @"\D", string.Empty);
    }

    private static string BuildUploadErrorMessage(Exception exception)
    {
        var message = exception.Message ?? string.Empty;
        if (message.Contains("413", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("too large", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("payload", StringComparison.OrdinalIgnoreCase))
        {
            return "Arquivo muito grande para o servidor. Envie um arquivo de ate 10MB.";
        }

        return "Nao foi possivel enviar o arquivo agora. Tente novamente em instantes.";
    }
}
