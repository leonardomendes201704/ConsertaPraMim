using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Admin.Models;
using ConsertaPraMim.Web.Admin.Security;
using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminLoadTestsController : Controller
{
    private readonly IAdminOperationsApiClient _adminOperationsApiClient;

    public AdminLoadTestsController(IAdminOperationsApiClient adminOperationsApiClient)
    {
        _adminOperationsApiClient = adminOperationsApiClient;
    }

    [HttpGet]
    public IActionResult Index(
        string? scenario = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? search = null,
        int page = 1,
        int pageSize = 20)
    {
        var model = new AdminLoadTestsIndexViewModel
        {
            Filters = new AdminLoadTestsFilterModel
            {
                Scenario = string.IsNullOrWhiteSpace(scenario) ? null : scenario.Trim(),
                FromUtc = fromUtc?.ToUniversalTime(),
                ToUtc = toUtc?.ToUniversalTime(),
                Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                Page = Math.Max(1, page),
                PageSize = Math.Clamp(pageSize, 1, 200)
            }
        };

        if (string.IsNullOrWhiteSpace(GetAccessToken()))
        {
            model.ErrorMessage = "Sessao expirada. Faca login novamente.";
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Runs(
        string? scenario = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? search = null,
        int page = 1,
        int pageSize = 20)
    {
        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { success = false, errorMessage = "Sessao expirada. Faca login novamente." });
        }

        var result = await _adminOperationsApiClient.GetLoadTestRunsAsync(
            new AdminLoadTestRunsQueryDto(
                scenario,
                fromUtc?.ToUniversalTime(),
                toUtc?.ToUniversalTime(),
                search,
                Math.Max(1, page),
                Math.Clamp(pageSize, 1, 200)),
            token,
            HttpContext.RequestAborted);

        return BuildApiResponse(result, "Falha ao carregar runs de carga.");
    }

    [HttpGet]
    public async Task<IActionResult> RunDetails([FromQuery] Guid id)
    {
        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { success = false, errorMessage = "Sessao expirada. Faca login novamente." });
        }

        if (id == Guid.Empty)
        {
            return BadRequest(new { success = false, errorMessage = "RunId invalido." });
        }

        var result = await _adminOperationsApiClient.GetLoadTestRunByIdAsync(
            id,
            token,
            HttpContext.RequestAborted);

        return BuildApiResponse(result, "Falha ao carregar detalhe do run.");
    }

    private string? GetAccessToken()
    {
        return User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
    }

    private static IActionResult BuildApiResponse<T>(
        AdminApiResult<T> result,
        string fallbackError)
    {
        if (!result.Success || result.Data == null)
        {
            var statusCode = result.StatusCode ?? StatusCodes.Status400BadRequest;
            return new JsonResult(new
            {
                success = false,
                errorMessage = result.ErrorMessage ?? fallbackError,
                errorCode = result.ErrorCode
            })
            {
                StatusCode = statusCode
            };
        }

        return new JsonResult(new
        {
            success = true,
            data = result.Data
        });
    }
}

