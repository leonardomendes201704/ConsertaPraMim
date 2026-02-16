using ConsertaPraMim.Web.Admin.Models;
using ConsertaPraMim.Web.Admin.Security;
using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminDisputesController : Controller
{
    private readonly IAdminOperationsApiClient _adminOperationsApiClient;

    public AdminDisputesController(IAdminOperationsApiClient adminOperationsApiClient)
    {
        _adminOperationsApiClient = adminOperationsApiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid? disputeCaseId, int take = 100)
    {
        var viewModel = new AdminDisputesQueuePageViewModel
        {
            Filters = new AdminDisputesQueueFilterModel
            {
                DisputeCaseId = disputeCaseId,
                Take = Math.Clamp(take, 1, 200)
            }
        };

        var token = User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
        if (string.IsNullOrWhiteSpace(token))
        {
            viewModel.ErrorMessage = "Sessao expirada. Faca login novamente.";
            return View(viewModel);
        }

        var queueResult = await _adminOperationsApiClient.GetDisputesQueueAsync(
            viewModel.Filters,
            token,
            HttpContext.RequestAborted);

        if (queueResult.Success)
        {
            viewModel.Queue = queueResult.Data;
        }
        else
        {
            viewModel.ErrorMessage = queueResult.ErrorMessage ?? "Falha ao carregar fila de disputas.";
        }

        return View(viewModel);
    }
}
