using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/no-show-audit")]
public class AdminNoShowAuditController : ControllerBase
{
    private readonly IAdminNoShowAuditService _adminNoShowAuditService;

    public AdminNoShowAuditController(IAdminNoShowAuditService adminNoShowAuditService)
    {
        _adminNoShowAuditService = adminNoShowAuditService;
    }

    /// <summary>
    /// Consulta trilha auditavel de decisoes no-show/cancelamento aplicadas pela politica financeira.
    /// </summary>
    /// <remarks>
    /// Retorna eventos estruturados (`ServiceFinancialPolicyEventGenerated`) com:
    /// - tipo de evento (cancelamento/no-show por ator);
    /// - outcome aplicado (ledger_applied, ledger_failed, no_ledger_impact, etc.);
    /// - impacto financeiro calculado (penalidade/compensacao);
    /// - resultado de lancamento em ledger para governanca operacional.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(AdminNoShowAuditDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAudit([FromQuery] AdminNoShowAuditQueryDto query)
    {
        var result = await _adminNoShowAuditService.GetAuditAsync(query);
        return Ok(result);
    }
}
