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
    public async Task<IActionResult> Index(
        Guid? disputeCaseId,
        int take = 100,
        string? status = null,
        string? type = null,
        Guid? operatorAdminId = null,
        string operatorScope = "all",
        string sla = "all")
    {
        var viewModel = new AdminDisputesQueuePageViewModel
        {
            Filters = new AdminDisputesQueueFilterModel
            {
                DisputeCaseId = disputeCaseId,
                Take = Math.Clamp(take, 1, 200),
                Status = string.IsNullOrWhiteSpace(status) || status.Equals("all", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : status.Trim(),
                Type = string.IsNullOrWhiteSpace(type) || type.Equals("all", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : type.Trim(),
                OperatorAdminId = operatorAdminId,
                OperatorScope = string.IsNullOrWhiteSpace(operatorScope) ? "all" : operatorScope.Trim().ToLowerInvariant(),
                Sla = string.IsNullOrWhiteSpace(sla) ? "all" : sla.Trim().ToLowerInvariant()
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

        var observabilityResult = await _adminOperationsApiClient.GetDisputesObservabilityAsync(
            token,
            HttpContext.RequestAborted);
        if (observabilityResult.Success)
        {
            viewModel.Observability = observabilityResult.Data;
        }
        else
        {
            viewModel.ObservabilityErrorMessage = observabilityResult.ErrorMessage ?? "Falha ao carregar alertas de anomalia.";
        }

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Export(
        Guid? disputeCaseId,
        int take = 200,
        string? status = null,
        string? type = null,
        Guid? operatorAdminId = null,
        string operatorScope = "all",
        string sla = "all")
    {
        var token = User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["ErrorMessage"] = "Sessao expirada. Faca login novamente.";
            return RedirectToAction(nameof(Index));
        }

        var filters = new AdminDisputesQueueFilterModel
        {
            DisputeCaseId = disputeCaseId,
            Take = Math.Clamp(take, 1, 200),
            Status = string.IsNullOrWhiteSpace(status) || status.Equals("all", StringComparison.OrdinalIgnoreCase) ? null : status.Trim(),
            Type = string.IsNullOrWhiteSpace(type) || type.Equals("all", StringComparison.OrdinalIgnoreCase) ? null : type.Trim(),
            OperatorAdminId = operatorAdminId,
            OperatorScope = string.IsNullOrWhiteSpace(operatorScope) ? "all" : operatorScope.Trim().ToLowerInvariant(),
            Sla = string.IsNullOrWhiteSpace(sla) ? "all" : sla.Trim().ToLowerInvariant()
        };

        var csvResult = await _adminOperationsApiClient.ExportDisputesQueueCsvAsync(
            filters,
            token,
            HttpContext.RequestAborted);

        if (!csvResult.Success || string.IsNullOrWhiteSpace(csvResult.Data))
        {
            TempData["ErrorMessage"] = csvResult.ErrorMessage ?? "Falha ao exportar disputas para auditoria.";
            return RedirectToAction(nameof(Index), new
            {
                disputeCaseId,
                take,
                status,
                type,
                operatorAdminId,
                operatorScope,
                sla
            });
        }

        var fileName = $"admin-disputes-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        return File(System.Text.Encoding.UTF8.GetBytes(csvResult.Data), "text/csv; charset=utf-8", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var viewModel = new AdminDisputeCaseDetailsPageViewModel
        {
            DisputeCaseId = id
        };

        var token = User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
        if (string.IsNullOrWhiteSpace(token))
        {
            viewModel.ErrorMessage = "Sessao expirada. Faca login novamente.";
            return View(viewModel);
        }

        var detailsResult = await _adminOperationsApiClient.GetDisputeByIdAsync(
            id,
            token,
            HttpContext.RequestAborted);

        if (!detailsResult.Success || detailsResult.Data == null)
        {
            if (detailsResult.StatusCode == StatusCodes.Status404NotFound)
            {
                return NotFound();
            }

            viewModel.ErrorMessage = detailsResult.ErrorMessage ?? "Falha ao carregar detalhes da disputa.";
            return View(viewModel);
        }

        viewModel.Case = detailsResult.Data;
        viewModel.LastUpdatedUtc = DateTime.UtcNow;
        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateWorkflow([FromBody] AdminDisputeWorkflowUpdateWebRequest request)
    {
        if (request.DisputeCaseId == Guid.Empty || string.IsNullOrWhiteSpace(request.Status))
        {
            return BadRequest(new
            {
                success = false,
                errorMessage = "Disputa ou status invalido para atualizacao de workflow."
            });
        }

        var token = User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new
            {
                success = false,
                errorMessage = "Sessao expirada. Faca login novamente."
            });
        }

        var result = await _adminOperationsApiClient.UpdateDisputeWorkflowAsync(
            request.DisputeCaseId,
            new ConsertaPraMim.Application.DTOs.AdminUpdateDisputeWorkflowRequestDto(
                request.Status,
                request.WaitingForRole,
                request.Note,
                request.ClaimOwnership),
            token,
            HttpContext.RequestAborted);

        if (!result.Success || result.Data == null || !result.Data.Success)
        {
            var statusCode = result.StatusCode ?? StatusCodes.Status400BadRequest;
            return StatusCode(statusCode, new
            {
                success = false,
                errorMessage = result.Data?.ErrorMessage ?? result.ErrorMessage ?? "Falha ao atualizar workflow da disputa.",
                errorCode = result.Data?.ErrorCode ?? result.ErrorCode
            });
        }

        return Ok(new
        {
            success = true,
            message = "Workflow da disputa atualizado com sucesso.",
            disputeCase = result.Data.Case
        });
    }

    [HttpPost]
    public async Task<IActionResult> RegisterDecision([FromBody] AdminDisputeDecisionWebRequest request)
    {
        if (request.DisputeCaseId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.Outcome) ||
            string.IsNullOrWhiteSpace(request.Justification))
        {
            return BadRequest(new
            {
                success = false,
                errorMessage = "Disputa, outcome e justificativa sao obrigatorios."
            });
        }

        var token = User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new
            {
                success = false,
                errorMessage = "Sessao expirada. Faca login novamente."
            });
        }

        var result = await _adminOperationsApiClient.RegisterDisputeDecisionAsync(
            request.DisputeCaseId,
            new ConsertaPraMim.Application.DTOs.AdminRegisterDisputeDecisionRequestDto(
                request.Outcome,
                request.Justification,
                request.ResolutionSummary,
                string.IsNullOrWhiteSpace(request.FinancialAction) || request.FinancialAction.Equals("none", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : new ConsertaPraMim.Application.DTOs.AdminDisputeFinancialDecisionRequestDto(
                        request.FinancialAction,
                        request.FinancialAmount,
                        request.FinancialReason)),
            token,
            HttpContext.RequestAborted);

        if (!result.Success || result.Data == null || !result.Data.Success)
        {
            var statusCode = result.StatusCode ?? StatusCodes.Status400BadRequest;
            return StatusCode(statusCode, new
            {
                success = false,
                errorMessage = result.Data?.ErrorMessage ?? result.ErrorMessage ?? "Falha ao registrar decisao da disputa.",
                errorCode = result.Data?.ErrorCode ?? result.ErrorCode
            });
        }

        return Ok(new
        {
            success = true,
            message = "Decisao registrada com sucesso.",
            disputeCase = result.Data.Case
        });
    }
}
