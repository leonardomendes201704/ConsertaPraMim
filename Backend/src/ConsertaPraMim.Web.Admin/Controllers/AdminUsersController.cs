using ConsertaPraMim.Web.Admin.Models;
using ConsertaPraMim.Web.Admin.Security;
using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminUsersController : Controller
{
    private readonly IAdminUsersApiClient _adminUsersApiClient;

    public AdminUsersController(IAdminUsersApiClient adminUsersApiClient)
    {
        _adminUsersApiClient = adminUsersApiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? searchTerm,
        string? role,
        bool? isActive,
        int page = 1,
        int pageSize = 20)
    {
        var viewModel = new AdminUsersIndexViewModel
        {
            Filters = NormalizeFilters(searchTerm, role, isActive, page, pageSize)
        };

        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            viewModel.ErrorMessage = "Token administrativo nao encontrado. Faca login novamente.";
            return View(viewModel);
        }

        var response = await _adminUsersApiClient.GetUsersAsync(viewModel.Filters, token, HttpContext.RequestAborted);
        if (!response.Success || response.Data == null)
        {
            viewModel.ErrorMessage = response.ErrorMessage ?? "Falha ao carregar usuarios.";
            return View(viewModel);
        }

        viewModel.Users = response.Data;
        viewModel.LastUpdatedUtc = DateTime.UtcNow;
        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var viewModel = new AdminUserDetailsViewModel();
        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            viewModel.ErrorMessage = "Token administrativo nao encontrado. Faca login novamente.";
            return View(viewModel);
        }

        var response = await _adminUsersApiClient.GetUserByIdAsync(id, token, HttpContext.RequestAborted);
        if (!response.Success || response.Data == null)
        {
            if (response.StatusCode == StatusCodes.Status404NotFound)
            {
                return NotFound();
            }

            viewModel.ErrorMessage = response.ErrorMessage ?? "Falha ao carregar detalhes do usuario.";
            return View(viewModel);
        }

        viewModel.User = response.Data;
        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus([FromBody] AdminUpdateUserStatusWebRequest request)
    {
        if (request.UserId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                errorMessage = "Usuario invalido."
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

        var response = await _adminUsersApiClient.UpdateUserStatusAsync(
            request.UserId,
            request.IsActive,
            request.Reason,
            token,
            HttpContext.RequestAborted);

        if (!response.Success)
        {
            var statusCode = response.StatusCode ?? StatusCodes.Status400BadRequest;
            return StatusCode(statusCode, new
            {
                success = false,
                errorMessage = response.ErrorMessage ?? "Nao foi possivel atualizar o usuario.",
                errorCode = response.ErrorCode
            });
        }

        return Ok(new
        {
            success = true,
            isActive = request.IsActive,
            message = request.IsActive ? "Usuario ativado com sucesso." : "Usuario desativado com sucesso."
        });
    }

    private string? GetAccessToken()
    {
        return User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
    }

    private static AdminUsersFilterModel NormalizeFilters(
        string? searchTerm,
        string? role,
        bool? isActive,
        int page,
        int pageSize)
    {
        var normalizedRole = NormalizeRole(role);

        return new AdminUsersFilterModel
        {
            SearchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim(),
            Role = normalizedRole,
            IsActive = isActive,
            Page = Math.Max(1, page),
            PageSize = Math.Clamp(pageSize, 1, 100)
        };
    }

    private static string NormalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return "all";
        }

        var normalized = role.Trim().ToLowerInvariant();
        return normalized switch
        {
            "client" => "client",
            "provider" => "provider",
            "admin" => "admin",
            _ => "all"
        };
    }
}
