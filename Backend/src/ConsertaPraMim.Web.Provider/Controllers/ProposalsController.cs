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
    public async Task<IActionResult> Submit(Guid requestId, string? estimatedValue, string? message)
    {
        decimal? parsedEstimatedValue = null;
        if (!string.IsNullOrWhiteSpace(estimatedValue))
        {
            if (!TryParseEstimatedValue(estimatedValue, out var parsed))
            {
                TempData["Error"] = "Valor estimado invalido. Informe no formato R$ 0,00.";
                return RedirectToAction("Details", "ServiceRequests", new { id = requestId });
            }

            parsedEstimatedValue = parsed;
        }

        var dto = new CreateProposalDto(requestId, parsedEstimatedValue, message);
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
