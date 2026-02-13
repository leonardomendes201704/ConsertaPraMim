using System.Security.Claims;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Roles = "Provider")]
[ApiController]
[Route("api/provider-credits")]
public class ProviderCreditsController : ControllerBase
{
    private readonly IProviderCreditService _providerCreditService;

    public ProviderCreditsController(IProviderCreditService providerCreditService)
    {
        _providerCreditService = providerCreditService;
    }

    [HttpGet("me/balance")]
    public async Task<IActionResult> GetMyBalance(CancellationToken cancellationToken)
    {
        if (!TryGetProviderId(out var providerId))
        {
            return Unauthorized();
        }

        var balance = await _providerCreditService.GetBalanceAsync(providerId, cancellationToken);
        return Ok(balance);
    }

    [HttpGet("me/statement")]
    public async Task<IActionResult> GetMyStatement(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? entryType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetProviderId(out var providerId))
        {
            return Unauthorized();
        }

        if (!TryParseEntryType(entryType, out var parsedEntryType))
        {
            return BadRequest(new { errorMessage = "entryType invalido." });
        }

        var query = new ProviderCreditStatementQueryDto(fromUtc, toUtc, parsedEntryType, page, pageSize);
        var statement = await _providerCreditService.GetStatementAsync(providerId, query, cancellationToken);
        return Ok(statement);
    }

    private bool TryGetProviderId(out Guid providerId)
    {
        providerId = Guid.Empty;
        var providerRaw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return !string.IsNullOrWhiteSpace(providerRaw) && Guid.TryParse(providerRaw, out providerId);
    }

    private static bool TryParseEntryType(string? raw, out ProviderCreditLedgerEntryType? entryType)
    {
        entryType = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (Enum.TryParse<ProviderCreditLedgerEntryType>(raw.Trim(), true, out var parsed))
        {
            entryType = parsed;
            return true;
        }

        return false;
    }
}
