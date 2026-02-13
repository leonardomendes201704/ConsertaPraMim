using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/provider-credits")]
public class AdminProviderCreditsController : ControllerBase
{
    private readonly IProviderCreditService _providerCreditService;

    public AdminProviderCreditsController(IProviderCreditService providerCreditService)
    {
        _providerCreditService = providerCreditService;
    }

    [HttpGet("{providerId:guid}/balance")]
    public async Task<IActionResult> GetBalance(Guid providerId, CancellationToken cancellationToken)
    {
        var balance = await _providerCreditService.GetBalanceAsync(providerId, cancellationToken);
        return Ok(balance);
    }

    [HttpGet("{providerId:guid}/statement")]
    public async Task<IActionResult> GetStatement(
        Guid providerId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? entryType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseEntryType(entryType, out var parsedEntryType))
        {
            return BadRequest(new { errorMessage = "entryType invalido." });
        }

        var query = new ProviderCreditStatementQueryDto(fromUtc, toUtc, parsedEntryType, page, pageSize);
        var statement = await _providerCreditService.GetStatementAsync(providerId, query, cancellationToken);
        return Ok(statement);
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
