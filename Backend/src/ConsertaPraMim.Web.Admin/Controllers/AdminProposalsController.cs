using ConsertaPraMim.Web.Admin.Models;
using ConsertaPraMim.Web.Admin.Security;
using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminProposalsController : Controller
{
    private readonly IAdminOperationsApiClient _adminOperationsApiClient;

    public AdminProposalsController(IAdminOperationsApiClient adminOperationsApiClient)
    {
        _adminOperationsApiClient = adminOperationsApiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        Guid? requestId,
        Guid? providerId,
        string? status,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page = 1,
        int pageSize = 20)
    {
        var model = new AdminProposalsIndexViewModel
        {
            Filters = NormalizeFilters(requestId, providerId, status, fromUtc, toUtc, page, pageSize)
        };

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            model.ErrorMessage = "Token administrativo nao encontrado. Faca login novamente.";
            return View(model);
        }

        var result = await _adminOperationsApiClient.GetProposalsAsync(model.Filters, token, HttpContext.RequestAborted);
        if (!result.Success || result.Data == null)
        {
            model.ErrorMessage = result.ErrorMessage ?? "Falha ao carregar propostas.";
            return View(model);
        }

        model.Proposals = result.Data;
        model.LastUpdatedUtc = DateTime.UtcNow;
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Invalidate([FromBody] AdminProposalInvalidateWebRequest request)
    {
        if (request.ProposalId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                errorMessage = "Proposta invalida."
            });
        }

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new
            {
                success = false,
                errorMessage = "Token administrativo ausente. Faca login novamente."
            });
        }

        var result = await _adminOperationsApiClient.InvalidateProposalAsync(
            request.ProposalId,
            request.Reason,
            token,
            HttpContext.RequestAborted);

        if (!result.Success)
        {
            var statusCode = result.StatusCode ?? StatusCodes.Status400BadRequest;
            return StatusCode(statusCode, new
            {
                success = false,
                errorMessage = result.ErrorMessage ?? "Nao foi possivel invalidar a proposta.",
                errorCode = result.ErrorCode
            });
        }

        return Ok(new
        {
            success = true,
            message = "Proposta invalidada com sucesso."
        });
    }

    private string? GetAccessToken()
    {
        return User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
    }

    private static AdminProposalsFilterModel NormalizeFilters(
        Guid? requestId,
        Guid? providerId,
        string? status,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize)
    {
        var normalizedStatus = NormalizeStatus(status);
        var from = fromUtc?.ToUniversalTime();
        var to = toUtc?.ToUniversalTime();

        if (from.HasValue && to.HasValue && from > to)
        {
            (from, to) = (to, from);
        }

        return new AdminProposalsFilterModel
        {
            RequestId = requestId,
            ProviderId = providerId,
            Status = normalizedStatus,
            FromUtc = from,
            ToUtc = to,
            Page = Math.Max(1, page),
            PageSize = Math.Clamp(pageSize, 1, 100)
        };
    }

    private static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return "all";
        var normalized = status.Trim().ToLowerInvariant();
        return normalized switch
        {
            "accepted" => "accepted",
            "pending" => "pending",
            "invalidated" => "invalidated",
            _ => "all"
        };
    }
}
