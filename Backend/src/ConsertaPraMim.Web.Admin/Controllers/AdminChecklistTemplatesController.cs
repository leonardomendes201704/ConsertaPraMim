using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Admin.Models;
using ConsertaPraMim.Web.Admin.Security;
using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminChecklistTemplatesController : Controller
{
    private readonly IAdminOperationsApiClient _adminOperationsApiClient;

    public AdminChecklistTemplatesController(IAdminOperationsApiClient adminOperationsApiClient)
    {
        _adminOperationsApiClient = adminOperationsApiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index(bool includeInactive = true)
    {
        var model = new AdminChecklistTemplatesIndexViewModel
        {
            IncludeInactive = includeInactive
        };

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            model.ErrorMessage = "Token administrativo nao encontrado. Faca login novamente.";
            return View(model);
        }

        var categoriesResult = await _adminOperationsApiClient.GetServiceCategoriesAsync(
            includeInactive: false,
            token,
            HttpContext.RequestAborted);
        if (!categoriesResult.Success || categoriesResult.Data == null)
        {
            model.ErrorMessage = categoriesResult.ErrorMessage ?? "Falha ao carregar categorias ativas.";
            return View(model);
        }

        var templatesResult = await _adminOperationsApiClient.GetChecklistTemplatesAsync(
            includeInactive,
            token,
            HttpContext.RequestAborted);
        if (!templatesResult.Success || templatesResult.Data == null)
        {
            model.ErrorMessage = templatesResult.ErrorMessage ?? "Falha ao carregar templates de checklist.";
            model.Categories = categoriesResult.Data;
            return View(model);
        }

        model.Categories = categoriesResult.Data;
        model.Templates = templatesResult.Data;
        model.LastUpdatedUtc = DateTime.UtcNow;
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AdminCreateChecklistTemplateWebRequest request)
    {
        if (request == null)
        {
            return BadRequest(new { success = false, errorMessage = "Payload invalido." });
        }

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { success = false, errorMessage = "Token administrativo ausente. Faca login novamente." });
        }

        var apiRequest = new AdminCreateChecklistTemplateRequestDto(
            request.CategoryDefinitionId,
            request.Name,
            request.Description,
            request.Items.Select(MapItemUpsert).ToList());

        var result = await _adminOperationsApiClient.CreateChecklistTemplateAsync(apiRequest, token, HttpContext.RequestAborted);
        if (!result.Success || result.Data == null || !result.Data.Success || result.Data.Template == null)
        {
            return StatusCode(
                result.StatusCode ?? StatusCodes.Status400BadRequest,
                new
                {
                    success = false,
                    errorMessage = result.Data?.ErrorMessage ?? result.ErrorMessage ?? "Nao foi possivel criar template de checklist.",
                    errorCode = result.Data?.ErrorCode ?? result.ErrorCode
                });
        }

        return Ok(new
        {
            success = true,
            template = result.Data.Template
        });
    }

    [HttpPost]
    public async Task<IActionResult> Update([FromBody] AdminUpdateChecklistTemplateWebRequest request)
    {
        if (request == null || request.TemplateId == Guid.Empty)
        {
            return BadRequest(new { success = false, errorMessage = "Template invalido." });
        }

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { success = false, errorMessage = "Token administrativo ausente. Faca login novamente." });
        }

        var apiRequest = new AdminUpdateChecklistTemplateRequestDto(
            request.Name,
            request.Description,
            request.Items.Select(MapItemUpsert).ToList());

        var result = await _adminOperationsApiClient.UpdateChecklistTemplateAsync(request.TemplateId, apiRequest, token, HttpContext.RequestAborted);
        if (!result.Success || result.Data == null || !result.Data.Success || result.Data.Template == null)
        {
            return StatusCode(
                result.StatusCode ?? StatusCodes.Status400BadRequest,
                new
                {
                    success = false,
                    errorMessage = result.Data?.ErrorMessage ?? result.ErrorMessage ?? "Nao foi possivel atualizar template de checklist.",
                    errorCode = result.Data?.ErrorCode ?? result.ErrorCode
                });
        }

        return Ok(new
        {
            success = true,
            template = result.Data.Template
        });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus([FromBody] AdminUpdateChecklistTemplateStatusWebRequest request)
    {
        if (request == null || request.TemplateId == Guid.Empty)
        {
            return BadRequest(new { success = false, errorMessage = "Template invalido." });
        }

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { success = false, errorMessage = "Token administrativo ausente. Faca login novamente." });
        }

        var apiRequest = new AdminUpdateChecklistTemplateStatusRequestDto(request.IsActive, request.Reason);
        var result = await _adminOperationsApiClient.UpdateChecklistTemplateStatusAsync(request.TemplateId, apiRequest, token, HttpContext.RequestAborted);
        if (!result.Success)
        {
            return StatusCode(
                result.StatusCode ?? StatusCodes.Status400BadRequest,
                new
                {
                    success = false,
                    errorMessage = result.ErrorMessage ?? "Nao foi possivel alterar o status do template de checklist.",
                    errorCode = result.ErrorCode
                });
        }

        return Ok(new
        {
            success = true,
            isActive = request.IsActive,
            message = request.IsActive ? "Template ativado com sucesso." : "Template inativado com sucesso."
        });
    }

    private string? GetAccessToken()
    {
        return User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
    }

    private static AdminChecklistTemplateItemUpsertDto MapItemUpsert(AdminChecklistTemplateItemWebRequest item)
    {
        return new AdminChecklistTemplateItemUpsertDto(
            item.Id,
            item.Title,
            item.HelpText,
            item.IsRequired,
            item.RequiresEvidence,
            item.AllowNote,
            item.IsActive,
            item.SortOrder);
    }
}
