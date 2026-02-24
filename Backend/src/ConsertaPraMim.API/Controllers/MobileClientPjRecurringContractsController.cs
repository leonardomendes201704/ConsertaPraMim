using System.Security.Claims;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

/// <summary>
/// Fluxo do app cliente para contratacao e renovacao de pacotes PJ recorrentes.
/// </summary>
/// <remarks>
/// Disponibiliza o ciclo basico de carteira PJ:
/// - listar contratos recorrentes do cliente autenticado;
/// - contratar novo pacote com SLA e janela operacional;
/// - registrar renovacao de ciclo.
/// </remarks>
[Authorize(Roles = "Client")]
[ApiController]
[Route("api/mobile/client/pj-recurring-contracts")]
public class MobileClientPjRecurringContractsController : ControllerBase
{
    private readonly IPjRecurringContractService _pjRecurringContractService;

    public MobileClientPjRecurringContractsController(IPjRecurringContractService pjRecurringContractService)
    {
        _pjRecurringContractService = pjRecurringContractService;
    }

    /// <summary>
    /// Lista os contratos PJ recorrentes do cliente autenticado.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PjRecurringContractDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(CancellationToken cancellationToken = default)
    {
        if (!TryGetClientUserId(out var clientUserId))
        {
            return Unauthorized(new
            {
                errorCode = "pj_recurring_invalid_user_claim",
                message = "Nao foi possivel identificar o cliente autenticado."
            });
        }

        var contracts = await _pjRecurringContractService.GetClientContractsAsync(clientUserId, cancellationToken);
        return Ok(contracts);
    }

    /// <summary>
    /// Contrata um novo pacote PJ recorrente para o cliente autenticado.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PjRecurringContractDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePjRecurringContractRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetClientUserId(out var clientUserId))
        {
            return Unauthorized(new
            {
                errorCode = "pj_recurring_invalid_user_claim",
                message = "Nao foi possivel identificar o cliente autenticado."
            });
        }

        try
        {
            var created = await _pjRecurringContractService.CreateAsync(clientUserId, request, cancellationToken);
            return CreatedAtAction(nameof(List), new { }, created);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { errorCode = "pj_recurring_unauthorized_actor", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { errorCode = "pj_recurring_invalid_operation", message = ex.Message });
        }
    }

    /// <summary>
    /// Registra renovacao de ciclo de um contrato PJ recorrente.
    /// </summary>
    [HttpPost("{contractId:guid}/renew")]
    [ProducesResponseType(typeof(PjRecurringContractDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Renew(
        Guid contractId,
        [FromBody] RenewPjRecurringContractRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetClientUserId(out var clientUserId))
        {
            return Unauthorized(new
            {
                errorCode = "pj_recurring_invalid_user_claim",
                message = "Nao foi possivel identificar o cliente autenticado."
            });
        }

        try
        {
            var updated = await _pjRecurringContractService.RenewAsync(
                clientUserId,
                contractId,
                request,
                cancellationToken);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { errorCode = "pj_recurring_not_found", message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { errorCode = "pj_recurring_unauthorized_actor", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { errorCode = "pj_recurring_invalid_operation", message = ex.Message });
        }
    }

    private bool TryGetClientUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var userIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdRaw, out userId);
    }
}
