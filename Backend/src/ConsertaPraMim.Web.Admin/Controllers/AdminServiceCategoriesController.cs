using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Admin.Models;
using ConsertaPraMim.Web.Admin.Security;
using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminServiceCategoriesController : Controller
{
    private readonly IAdminOperationsApiClient _adminOperationsApiClient;

    public AdminServiceCategoriesController(IAdminOperationsApiClient adminOperationsApiClient)
    {
        _adminOperationsApiClient = adminOperationsApiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index(bool includeInactive = true)
    {
        var model = new AdminServiceCategoriesIndexViewModel
        {
            IncludeInactive = includeInactive
        };

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            model.ErrorMessage = "Token administrativo nao encontrado. Faca login novamente.";
            return View(model);
        }

        var result = await _adminOperationsApiClient.GetServiceCategoriesAsync(includeInactive, token, HttpContext.RequestAborted);
        if (!result.Success || result.Data == null)
        {
            model.ErrorMessage = result.ErrorMessage ?? "Falha ao carregar categorias.";
            return View(model);
        }

        model.Categories = result.Data;
        model.LastUpdatedUtc = DateTime.UtcNow;
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AdminCreateServiceCategoryWebRequest request)
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

        var apiRequest = new AdminCreateServiceCategoryRequestDto(request.Name, request.Slug, request.LegacyCategory, request.Icon);
        var result = await _adminOperationsApiClient.CreateServiceCategoryAsync(apiRequest, token, HttpContext.RequestAborted);
        if (!result.Success || result.Data == null || !result.Data.Success || result.Data.Category == null)
        {
            return StatusCode(
                result.StatusCode ?? StatusCodes.Status400BadRequest,
                new
                {
                    success = false,
                    errorMessage = result.Data?.ErrorMessage ?? result.ErrorMessage ?? "Nao foi possivel criar a categoria.",
                    errorCode = result.Data?.ErrorCode ?? result.ErrorCode
                });
        }

        return Ok(new
        {
            success = true,
            category = result.Data.Category
        });
    }

    [HttpPost]
    public async Task<IActionResult> Update([FromBody] AdminUpdateServiceCategoryWebRequest request)
    {
        if (request == null || request.CategoryId == Guid.Empty)
        {
            return BadRequest(new { success = false, errorMessage = "Categoria invalida." });
        }

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { success = false, errorMessage = "Token administrativo ausente. Faca login novamente." });
        }

        var apiRequest = new AdminUpdateServiceCategoryRequestDto(request.Name, request.Slug, request.LegacyCategory, request.Icon);
        var result = await _adminOperationsApiClient.UpdateServiceCategoryAsync(request.CategoryId, apiRequest, token, HttpContext.RequestAborted);
        if (!result.Success || result.Data == null || !result.Data.Success || result.Data.Category == null)
        {
            return StatusCode(
                result.StatusCode ?? StatusCodes.Status400BadRequest,
                new
                {
                    success = false,
                    errorMessage = result.Data?.ErrorMessage ?? result.ErrorMessage ?? "Nao foi possivel atualizar a categoria.",
                    errorCode = result.Data?.ErrorCode ?? result.ErrorCode
                });
        }

        return Ok(new
        {
            success = true,
            category = result.Data.Category
        });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus([FromBody] AdminUpdateServiceCategoryStatusWebRequest request)
    {
        if (request == null || request.CategoryId == Guid.Empty)
        {
            return BadRequest(new { success = false, errorMessage = "Categoria invalida." });
        }

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { success = false, errorMessage = "Token administrativo ausente. Faca login novamente." });
        }

        var apiRequest = new AdminUpdateServiceCategoryStatusRequestDto(request.IsActive, request.Reason);
        var result = await _adminOperationsApiClient.UpdateServiceCategoryStatusAsync(request.CategoryId, apiRequest, token, HttpContext.RequestAborted);
        if (!result.Success)
        {
            return StatusCode(
                result.StatusCode ?? StatusCodes.Status400BadRequest,
                new
                {
                    success = false,
                    errorMessage = result.ErrorMessage ?? "Nao foi possivel alterar o status da categoria.",
                    errorCode = result.ErrorCode
                });
        }

        return Ok(new
        {
            success = true,
            isActive = request.IsActive,
            message = request.IsActive ? "Categoria ativada com sucesso." : "Categoria inativada com sucesso."
        });
    }

    private string? GetAccessToken()
    {
        return User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
    }
}
