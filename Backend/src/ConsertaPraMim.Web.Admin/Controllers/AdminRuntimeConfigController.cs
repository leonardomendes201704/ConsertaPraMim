using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Admin.Models;
using ConsertaPraMim.Web.Admin.Security;
using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminRuntimeConfigController : Controller
{
    private readonly IAdminOperationsApiClient _adminOperationsApiClient;

    public AdminRuntimeConfigController(IAdminOperationsApiClient adminOperationsApiClient)
    {
        _adminOperationsApiClient = adminOperationsApiClient;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Sections()
    {
        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { success = false, errorMessage = "Sessao expirada. Faca login novamente." });
        }

        var result = await _adminOperationsApiClient.GetMonitoringConfigSectionsAsync(
            token,
            HttpContext.RequestAborted);

        return BuildApiResponse(result, "Falha ao carregar secoes de configuracao runtime.");
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] SaveRuntimeConfigSectionWebRequest request)
    {
        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { success = false, errorMessage = "Sessao expirada. Faca login novamente." });
        }

        var result = await _adminOperationsApiClient.SetMonitoringConfigSectionAsync(
            request.SectionPath,
            request.JsonValue,
            token,
            HttpContext.RequestAborted);

        return BuildApiResponse(result, "Falha ao salvar secao de configuracao runtime.");
    }

    [HttpPost]
    public async Task<IActionResult> RestartApi()
    {
        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { success = false, errorMessage = "Sessao expirada. Faca login novamente." });
        }

        var result = await _adminOperationsApiClient.RestartMonitoringApiAsync(
            token,
            HttpContext.RequestAborted);

        return BuildApiResponse(result, "Falha ao solicitar reinicio da API.");
    }

    private string? GetAccessToken()
    {
        return User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
    }

    private static IActionResult BuildApiResponse<T>(AdminApiResult<T> result, string fallbackError)
    {
        if (!result.Success || result.Data == null)
        {
            return new JsonResult(new
            {
                success = false,
                errorMessage = result.ErrorMessage ?? fallbackError,
                errorCode = result.ErrorCode
            })
            {
                StatusCode = result.StatusCode ?? StatusCodes.Status400BadRequest
            };
        }

        return new JsonResult(new
        {
            success = true,
            data = result.Data
        });
    }

    public sealed record SaveRuntimeConfigSectionWebRequest(
        string SectionPath,
        string JsonValue);
}
