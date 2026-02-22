using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Admin.Models;
using ConsertaPraMim.Web.Admin.Security;
using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminLegalTermsController : Controller
{
    private readonly IAdminOperationsApiClient _adminOperationsApiClient;

    public AdminLegalTermsController(IAdminOperationsApiClient adminOperationsApiClient)
    {
        _adminOperationsApiClient = adminOperationsApiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string audience = "client")
    {
        var normalizedAudience = NormalizeAudience(audience);
        var model = new AdminLegalTermsPageViewModel
        {
            SelectedAudience = normalizedAudience,
            SelectedAudienceLabel = GetAudienceLabel(normalizedAudience),
            SuccessMessage = TempData["AdminLegalTermsSuccessMessage"]?.ToString(),
            ErrorMessage = TempData["AdminLegalTermsErrorMessage"]?.ToString(),
            PublishRequest = new AdminLegalTermsPublishWebRequest
            {
                Audience = normalizedAudience
            }
        };

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            model.ErrorMessage ??= "Token administrativo nao encontrado. Faca login novamente.";
            return View(model);
        }

        await PopulateModelAsync(model, token, HttpContext.RequestAborted);
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Publish(AdminLegalTermsPublishWebRequest request)
    {
        var normalizedAudience = NormalizeAudience(request.Audience);
        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["AdminLegalTermsErrorMessage"] = "Token administrativo nao encontrado. Faca login novamente.";
            return RedirectToAction(nameof(Index), new { audience = normalizedAudience });
        }

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.HtmlContent))
        {
            var model = new AdminLegalTermsPageViewModel
            {
                SelectedAudience = normalizedAudience,
                SelectedAudienceLabel = GetAudienceLabel(normalizedAudience),
                ErrorMessage = "Titulo e conteudo HTML sao obrigatorios para publicar uma nova versao.",
                PublishRequest = new AdminLegalTermsPublishWebRequest
                {
                    Audience = normalizedAudience,
                    Title = request.Title?.Trim() ?? string.Empty,
                    HtmlContent = request.HtmlContent ?? string.Empty,
                    ChangeSummary = request.ChangeSummary?.Trim()
                }
            };

            await PopulateModelAsync(model, token, HttpContext.RequestAborted);
            return View(nameof(Index), model);
        }

        var payload = new LegalTermsPublishPayloadDto(
            Title: request.Title.Trim(),
            HtmlContent: request.HtmlContent,
            ChangeSummary: string.IsNullOrWhiteSpace(request.ChangeSummary) ? null : request.ChangeSummary.Trim());

        var publishResult = await _adminOperationsApiClient.PublishLegalTermsAsync(
            normalizedAudience,
            payload,
            token,
            HttpContext.RequestAborted);

        if (!publishResult.Success || publishResult.Data == null)
        {
            var model = new AdminLegalTermsPageViewModel
            {
                SelectedAudience = normalizedAudience,
                SelectedAudienceLabel = GetAudienceLabel(normalizedAudience),
                ErrorMessage = publishResult.ErrorMessage ?? "Nao foi possivel publicar a nova versao do termo legal.",
                PublishRequest = new AdminLegalTermsPublishWebRequest
                {
                    Audience = normalizedAudience,
                    Title = request.Title.Trim(),
                    HtmlContent = request.HtmlContent,
                    ChangeSummary = request.ChangeSummary?.Trim()
                }
            };

            await PopulateModelAsync(model, token, HttpContext.RequestAborted);
            return View(nameof(Index), model);
        }

        TempData["AdminLegalTermsSuccessMessage"] =
            $"Nova versao publicada com sucesso para {GetAudienceLabel(normalizedAudience)} (v{publishResult.Data.Version}).";

        return RedirectToAction(nameof(Index), new { audience = normalizedAudience });
    }

    private async Task PopulateModelAsync(
        AdminLegalTermsPageViewModel model,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var activeResult = await _adminOperationsApiClient.GetLegalTermsActiveAsync(
            model.SelectedAudience,
            accessToken,
            cancellationToken);

        if (activeResult.Success && activeResult.Data != null)
        {
            model.ActiveDocument = activeResult.Data;
        }
        else
        {
            model.ErrorMessage ??= activeResult.ErrorMessage ?? "Falha ao carregar termo ativo.";
        }

        var versionsResult = await _adminOperationsApiClient.GetLegalTermsVersionsAsync(
            model.SelectedAudience,
            accessToken,
            cancellationToken);

        if (versionsResult.Success && versionsResult.Data != null)
        {
            model.Versions = versionsResult.Data
                .OrderByDescending(x => x.Version)
                .ToArray();
        }
        else
        {
            model.ErrorMessage ??= versionsResult.ErrorMessage ?? "Falha ao carregar historico de versoes.";
        }

        model.PublishRequest.Audience = model.SelectedAudience;
        if (string.IsNullOrWhiteSpace(model.PublishRequest.Title))
        {
            model.PublishRequest.Title = model.ActiveDocument?.Title ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(model.PublishRequest.HtmlContent))
        {
            model.PublishRequest.HtmlContent = model.ActiveDocument?.HtmlContent ?? string.Empty;
        }
    }

    private string? GetAccessToken()
    {
        return User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
    }

    private static string NormalizeAudience(string? audience)
    {
        return string.Equals(audience?.Trim(), "provider", StringComparison.OrdinalIgnoreCase)
            ? "provider"
            : "client";
    }

    private static string GetAudienceLabel(string audience)
    {
        return audience.Equals("provider", StringComparison.OrdinalIgnoreCase)
            ? "Prestador"
            : "Cliente";
    }
}
