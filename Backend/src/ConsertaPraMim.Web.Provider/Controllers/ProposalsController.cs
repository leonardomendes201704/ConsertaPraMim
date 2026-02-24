using System.Globalization;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Provider.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Provider.Controllers;

[Authorize(Roles = "Provider")]
public class ProposalsController : Controller
{
    private readonly IProviderBackendApiClient _backendApiClient;

    public ProposalsController(IProviderBackendApiClient backendApiClient)
    {
        _backendApiClient = backendApiClient;
    }

    public async Task<IActionResult> Index()
    {
        var (proposals, errorMessage) = await _backendApiClient.GetMyProposalsAsync(HttpContext.RequestAborted);
        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            TempData["Error"] = errorMessage;
        }

        return View(proposals);
    }

    [HttpPost]
    public async Task<IActionResult> Submit(
        Guid requestId,
        string? estimatedValue,
        string? estimatedLeadTimeHours,
        string? warrantyDays,
        string? message)
    {
        decimal? parsedEstimatedValue = null;
        int? parsedEstimatedLeadTimeHours = null;
        int? parsedWarrantyDays = null;
        if (!string.IsNullOrWhiteSpace(estimatedValue))
        {
            if (!TryParseEstimatedValue(estimatedValue, out var parsed))
            {
                TempData["Error"] = "Valor estimado invalido. Informe no formato R$ 0,00.";
                return RedirectToAction("Details", "ServiceRequests", new { id = requestId });
            }

            parsedEstimatedValue = parsed;
        }

        if (!string.IsNullOrWhiteSpace(estimatedLeadTimeHours))
        {
            if (!int.TryParse(estimatedLeadTimeHours, out var parsedLeadTimeHours) ||
                parsedLeadTimeHours <= 0 ||
                parsedLeadTimeHours > 720)
            {
                TempData["Error"] = "Prazo estimado invalido. Informe entre 1 e 720 horas.";
                return RedirectToAction("Details", "ServiceRequests", new { id = requestId });
            }

            parsedEstimatedLeadTimeHours = parsedLeadTimeHours;
        }

        if (!string.IsNullOrWhiteSpace(warrantyDays))
        {
            if (!int.TryParse(warrantyDays, out var parsedWarranty) || parsedWarranty < 0 || parsedWarranty > 3650)
            {
                TempData["Error"] = "Garantia invalida. Informe entre 0 e 3650 dias.";
                return RedirectToAction("Details", "ServiceRequests", new { id = requestId });
            }

            parsedWarrantyDays = parsedWarranty;
        }

        var dto = new CreateProposalDto(
            requestId,
            parsedEstimatedValue,
            message,
            parsedEstimatedLeadTimeHours,
            parsedWarrantyDays);
        var (success, errorMessage) = await _backendApiClient.SubmitProposalAsync(dto, HttpContext.RequestAborted);
        if (!success)
        {
            TempData["Error"] = errorMessage ?? "Nao foi possivel enviar a proposta.";
            return RedirectToAction("Details", "ServiceRequests", new { id = requestId });
        }

        TempData["Success"] = "Proposta enviada com sucesso! Aguarde o retorno do cliente.";
        return RedirectToAction("Details", "ServiceRequests", new { id = requestId });
    }

    private static bool TryParseEstimatedValue(string rawValue, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var normalized = rawValue
            .Replace("R$", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty)
            .Replace("\u00A0", string.Empty)
            .Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (!normalized.Contains(',') &&
            decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        if (decimal.TryParse(normalized, NumberStyles.Number, new CultureInfo("pt-BR"), out value))
        {
            return true;
        }

        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
