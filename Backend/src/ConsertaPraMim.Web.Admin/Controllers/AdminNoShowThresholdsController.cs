using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Admin.Models;
using ConsertaPraMim.Web.Admin.Security;
using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminNoShowThresholdsController : Controller
{
    private readonly IAdminDashboardApiClient _adminDashboardApiClient;

    public AdminNoShowThresholdsController(IAdminDashboardApiClient adminDashboardApiClient)
    {
        _adminDashboardApiClient = adminDashboardApiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var viewModel = new AdminNoShowThresholdsViewModel();
        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            viewModel.ErrorMessage = "Token administrativo nao encontrado. Faca login novamente.";
            return View(viewModel);
        }

        var result = await _adminDashboardApiClient.GetNoShowAlertThresholdsAsync(token, HttpContext.RequestAborted);
        if (!result.Success || result.Configuration == null)
        {
            viewModel.ErrorMessage = result.ErrorMessage ?? "Falha ao carregar thresholds de no-show.";
            return View(viewModel);
        }

        viewModel.Configuration = result.Configuration;
        viewModel.LastUpdatedUtc = DateTime.UtcNow;
        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] AdminUpdateNoShowAlertThresholdWebRequest request)
    {
        if (request == null)
        {
            return BadRequest(new { success = false, errorMessage = "Payload invalido." });
        }

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { success = false, errorMessage = "Sessao expirada. Faca login novamente." });
        }

        var apiRequest = new AdminUpdateNoShowAlertThresholdRequestDto(
            request.NoShowRateWarningPercent,
            request.NoShowRateCriticalPercent,
            request.HighRiskQueueWarningCount,
            request.HighRiskQueueCriticalCount,
            request.ReminderSendSuccessWarningPercent,
            request.ReminderSendSuccessCriticalPercent,
            request.Notes);

        var result = await _adminDashboardApiClient.UpdateNoShowAlertThresholdsAsync(
            apiRequest,
            token,
            HttpContext.RequestAborted);
        if (!result.Success || result.Configuration == null)
        {
            return StatusCode(
                result.StatusCode ?? StatusCodes.Status502BadGateway,
                new
                {
                    success = false,
                    errorMessage = result.ErrorMessage ?? "Falha ao atualizar thresholds de no-show."
                });
        }

        return Ok(new
        {
            success = true,
            configuration = result.Configuration,
            updatedAtUtc = DateTime.UtcNow
        });
    }

    private string? GetAccessToken()
    {
        return User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
    }
}

