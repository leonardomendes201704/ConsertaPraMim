using ConsertaPraMim.Web.Admin.Models;
using ConsertaPraMim.Web.Admin.Security;
using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminServiceRequestsController : Controller
{
    private readonly IAdminOperationsApiClient _adminOperationsApiClient;

    public AdminServiceRequestsController(IAdminOperationsApiClient adminOperationsApiClient)
    {
        _adminOperationsApiClient = adminOperationsApiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? searchTerm,
        string? status,
        string? category,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page = 1,
        int pageSize = 20)
    {
        var model = new AdminServiceRequestsIndexViewModel
        {
            Filters = NormalizeFilters(searchTerm, status, category, fromUtc, toUtc, page, pageSize)
        };

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            model.ErrorMessage = "Token administrativo nao encontrado. Faca login novamente.";
            return View(model);
        }

        var result = await _adminOperationsApiClient.GetServiceRequestsAsync(model.Filters, token, HttpContext.RequestAborted);
        if (!result.Success || result.Data == null)
        {
            model.ErrorMessage = result.ErrorMessage ?? "Falha ao carregar pedidos.";
            return View(model);
        }

        model.Requests = result.Data;
        model.LastUpdatedUtc = DateTime.UtcNow;
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var model = new AdminServiceRequestDetailsViewModel();
        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            model.ErrorMessage = "Token administrativo nao encontrado. Faca login novamente.";
            return View(model);
        }

        var result = await _adminOperationsApiClient.GetServiceRequestByIdAsync(id, token, HttpContext.RequestAborted);
        if (!result.Success || result.Data == null)
        {
            if (result.StatusCode == StatusCodes.Status404NotFound)
            {
                return NotFound();
            }

            model.ErrorMessage = result.ErrorMessage ?? "Falha ao carregar detalhes do pedido.";
            return View(model);
        }

        model.Request = result.Data;
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus([FromBody] AdminServiceRequestStatusUpdateWebRequest request)
    {
        if (request.RequestId == Guid.Empty || string.IsNullOrWhiteSpace(request.Status))
        {
            return BadRequest(new
            {
                success = false,
                errorMessage = "Pedido ou status invalido."
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

        var result = await _adminOperationsApiClient.UpdateServiceRequestStatusAsync(
            request.RequestId,
            request.Status,
            request.Reason,
            token,
            HttpContext.RequestAborted);

        if (!result.Success)
        {
            var statusCode = result.StatusCode ?? StatusCodes.Status400BadRequest;
            return StatusCode(statusCode, new
            {
                success = false,
                errorMessage = result.ErrorMessage ?? "Nao foi possivel atualizar o status do pedido.",
                errorCode = result.ErrorCode
            });
        }

        return Ok(new
        {
            success = true,
            status = request.Status,
            message = "Status do pedido atualizado com sucesso."
        });
    }

    private string? GetAccessToken()
    {
        return User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
    }

    private static AdminServiceRequestsFilterModel NormalizeFilters(
        string? searchTerm,
        string? status,
        string? category,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize)
    {
        var normalizedStatus = NormalizeStatus(status);
        var normalizedCategory = NormalizeCategory(category);
        var from = fromUtc?.ToUniversalTime();
        var to = toUtc?.ToUniversalTime();

        if (from.HasValue && to.HasValue && from > to)
        {
            (from, to) = (to, from);
        }

        return new AdminServiceRequestsFilterModel
        {
            SearchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim(),
            Status = normalizedStatus,
            Category = normalizedCategory,
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
            "created" => "Created",
            "matching" => "Matching",
            "scheduled" => "Scheduled",
            "inprogress" => "InProgress",
            "completed" => "Completed",
            "validated" => "Validated",
            "canceled" => "Canceled",
            _ => "all"
        };
    }

    private static string NormalizeCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return "all";
        var normalized = category.Trim().ToLowerInvariant();
        return normalized switch
        {
            "electrical" => "Electrical",
            "plumbing" => "Plumbing",
            "electronics" => "Electronics",
            "appliances" => "Appliances",
            "masonry" => "Masonry",
            "cleaning" => "Cleaning",
            "other" => "Other",
            _ => "all"
        };
    }
}
