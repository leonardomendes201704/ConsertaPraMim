using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Admin.Models;
using ConsertaPraMim.Web.Admin.Security;
using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminMonitoringController : Controller
{
    private readonly IAdminOperationsApiClient _adminOperationsApiClient;

    public AdminMonitoringController(IAdminOperationsApiClient adminOperationsApiClient)
    {
        _adminOperationsApiClient = adminOperationsApiClient;
    }

    [HttpGet]
    public IActionResult Index(
        string range = "1h",
        string? endpoint = null,
        int? statusCode = null,
        Guid? userId = null,
        string? tenantId = null,
        string? severity = null,
        string groupBy = "type",
        string? search = null,
        int take = 20,
        int page = 1,
        int pageSize = 50)
    {
        var model = new AdminMonitoringIndexViewModel
        {
            Filters = new AdminMonitoringFilterModel
            {
                Range = NormalizeRange(range),
                Endpoint = string.IsNullOrWhiteSpace(endpoint) ? null : endpoint.Trim(),
                StatusCode = statusCode,
                UserId = userId,
                TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId.Trim(),
                Severity = NormalizeSeverity(severity),
                GroupBy = NormalizeGroupBy(groupBy),
                Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                Take = Math.Clamp(take, 1, 100),
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
    public async Task<IActionResult> Config()
    {
        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { success = false, errorMessage = "Sessao expirada. Faca login novamente." });
        }

        var result = await _adminOperationsApiClient.GetMonitoringRuntimeConfigAsync(
            token,
            HttpContext.RequestAborted);

        return BuildApiResponse(result, "Falha ao carregar configuracao de monitoramento.");
    }

    [HttpPost]
    public async Task<IActionResult> ToggleTelemetry([FromBody] AdminMonitoringToggleTelemetryWebRequest request)
    {
        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { success = false, errorMessage = "Sessao expirada. Faca login novamente." });
        }

        var result = await _adminOperationsApiClient.SetMonitoringTelemetryEnabledAsync(
            request.Enabled,
            token,
            HttpContext.RequestAborted);

        return BuildApiResponse(result, "Falha ao atualizar configuracao de monitoramento.");
    }

    [HttpGet]
    public async Task<IActionResult> Overview(
        string range = "1h",
        string? endpoint = null,
        int? statusCode = null,
        Guid? userId = null,
        string? tenantId = null,
        string? severity = null)
    {
        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { success = false, errorMessage = "Sessao expirada. Faca login novamente." });
        }

        var result = await _adminOperationsApiClient.GetMonitoringOverviewAsync(
            new AdminMonitoringOverviewQueryDto(
                NormalizeRange(range),
                endpoint,
                statusCode,
                userId,
                tenantId,
                NormalizeSeverity(severity)),
            token,
            HttpContext.RequestAborted);

        return BuildApiResponse(result, "Falha ao carregar overview de monitoramento.");
    }

    [HttpGet]
    public async Task<IActionResult> TopEndpoints(
        string range = "1h",
        int take = 20,
        string? endpoint = null,
        int? statusCode = null,
        Guid? userId = null,
        string? tenantId = null,
        string? severity = null)
    {
        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { success = false, errorMessage = "Sessao expirada. Faca login novamente." });
        }

        var result = await _adminOperationsApiClient.GetMonitoringTopEndpointsAsync(
            new AdminMonitoringTopEndpointsQueryDto(
                NormalizeRange(range),
                Math.Clamp(take, 1, 100),
                endpoint,
                statusCode,
                userId,
                tenantId,
                NormalizeSeverity(severity)),
            token,
            HttpContext.RequestAborted);

        return BuildApiResponse(result, "Falha ao carregar top endpoints.");
    }

    [HttpGet]
    public async Task<IActionResult> Latency(
        string? endpoint = null,
        string range = "1h",
        int? statusCode = null,
        Guid? userId = null,
        string? tenantId = null,
        string? severity = null)
    {
        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { success = false, errorMessage = "Sessao expirada. Faca login novamente." });
        }

        var result = await _adminOperationsApiClient.GetMonitoringLatencyAsync(
            new AdminMonitoringLatencyQueryDto(
                endpoint,
                NormalizeRange(range),
                statusCode,
                userId,
                tenantId,
                NormalizeSeverity(severity)),
            token,
            HttpContext.RequestAborted);

        return BuildApiResponse(result, "Falha ao carregar analise de latencia.");
    }

    [HttpGet]
    public async Task<IActionResult> Errors(
        string range = "1h",
        string groupBy = "type",
        string? endpoint = null,
        int? statusCode = null,
        Guid? userId = null,
        string? tenantId = null,
        string? severity = null)
    {
        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { success = false, errorMessage = "Sessao expirada. Faca login novamente." });
        }

        var result = await _adminOperationsApiClient.GetMonitoringErrorsAsync(
            new AdminMonitoringErrorsQueryDto(
                NormalizeRange(range),
                NormalizeGroupBy(groupBy),
                endpoint,
                statusCode,
                userId,
                tenantId,
                NormalizeSeverity(severity)),
            token,
            HttpContext.RequestAborted);

        return BuildApiResponse(result, "Falha ao carregar analise de erros.");
    }

    [HttpGet]
    public async Task<IActionResult> ErrorDetails(
        string errorKey,
        string range = "1h",
        string groupBy = "type",
        string? endpoint = null,
        int? statusCode = null,
        Guid? userId = null,
        string? tenantId = null,
        string? severity = null,
        int take = 10)
    {
        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { success = false, errorMessage = "Sessao expirada. Faca login novamente." });
        }

        var result = await _adminOperationsApiClient.GetMonitoringErrorDetailsAsync(
            new AdminMonitoringErrorDetailsQueryDto(
                ErrorKey: errorKey,
                Range: NormalizeRange(range),
                GroupBy: NormalizeGroupBy(groupBy),
                Endpoint: endpoint,
                StatusCode: statusCode,
                UserId: userId,
                TenantId: tenantId,
                Severity: NormalizeSeverity(severity),
                Take: Math.Clamp(take, 1, 25)),
            token,
            HttpContext.RequestAborted);

        return BuildApiResponse(result, "Falha ao carregar detalhe do erro.");
    }

    [HttpGet]
    public async Task<IActionResult> Requests(
        string range = "1h",
        string? endpoint = null,
        int? statusCode = null,
        Guid? userId = null,
        string? tenantId = null,
        string? severity = null,
        string? search = null,
        int page = 1,
        int pageSize = 50)
    {
        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { success = false, errorMessage = "Sessao expirada. Faca login novamente." });
        }

        var result = await _adminOperationsApiClient.GetMonitoringRequestsAsync(
            new AdminMonitoringRequestsQueryDto(
                NormalizeRange(range),
                endpoint,
                statusCode,
                userId,
                tenantId,
                NormalizeSeverity(severity),
                search,
                Math.Max(1, page),
                Math.Clamp(pageSize, 1, 200)),
            token,
            HttpContext.RequestAborted);

        return BuildApiResponse(result, "Falha ao carregar requests monitorados.");
    }

    [HttpGet]
    public async Task<IActionResult> ExportRequestsCsv(
        string range = "1h",
        string? endpoint = null,
        int? statusCode = null,
        Guid? userId = null,
        string? tenantId = null,
        string? severity = null,
        string? search = null)
    {
        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { success = false, errorMessage = "Sessao expirada. Faca login novamente." });
        }

        var result = await _adminOperationsApiClient.ExportMonitoringRequestsCsvAsync(
            new AdminMonitoringRequestsQueryDto(
                NormalizeRange(range),
                endpoint,
                statusCode,
                userId,
                tenantId,
                NormalizeSeverity(severity),
                search,
                Page: 1,
                PageSize: 1),
            token,
            HttpContext.RequestAborted);

        return BuildApiResponse(result, "Falha ao exportar requests monitorados.");
    }

    [HttpGet]
    public async Task<IActionResult> RequestDetails(string correlationId)
    {
        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { success = false, errorMessage = "Sessao expirada. Faca login novamente." });
        }

        var result = await _adminOperationsApiClient.GetMonitoringRequestDetailsAsync(
            correlationId,
            token,
            HttpContext.RequestAborted);

        return BuildApiResponse(result, "Falha ao carregar detalhe do request.");
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

    private static string NormalizeRange(string? range)
    {
        var normalized = string.IsNullOrWhiteSpace(range)
            ? "1h"
            : range.Trim().ToLowerInvariant();
        return normalized is "1h" or "2h" or "4h" or "6h" or "8h" or "12h" or "24h" or "7d" or "30d"
            ? normalized
            : "1h";
    }

    private static string NormalizeGroupBy(string? groupBy)
    {
        var normalized = string.IsNullOrWhiteSpace(groupBy)
            ? "type"
            : groupBy.Trim().ToLowerInvariant();
        return normalized is "type" or "endpoint" or "status" ? normalized : "type";
    }

    private static string? NormalizeSeverity(string? severity)
    {
        if (string.IsNullOrWhiteSpace(severity))
        {
            return null;
        }

        var normalized = severity.Trim().ToLowerInvariant();
        return normalized is "info" or "warn" or "error" ? normalized : null;
    }

    public sealed record AdminMonitoringToggleTelemetryWebRequest(bool Enabled);

}
