using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.DTOs;
using System.Security.Claims;
using System.Globalization;

namespace ConsertaPraMim.Web.Provider.Controllers;

[Authorize(Roles = "Provider")]
public class ProposalsController : Controller
{
    private readonly IProposalService _proposalService;

    public ProposalsController(IProposalService proposalService)
    {
        _proposalService = proposalService;
    }

    public async Task<IActionResult> Index()
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userId = Guid.Parse(userIdString!);
        
        var proposals = await _proposalService.GetByProviderAsync(userId);
        return View(proposals);
    }

    [HttpPost]
    public async Task<IActionResult> Submit(Guid requestId, string? estimatedValue, string? message)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString)) return RedirectToAction("Login", "Account");

        var userId = Guid.Parse(userIdString);

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
        await _proposalService.CreateAsync(userId, dto);

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

        // Hidden field from UI sends invariant decimal (ex.: 11.11).
        // Parse with InvariantCulture first when there is no comma.
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
