using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/landing-leads")]
public sealed class AdminLandingLeadsController : ControllerBase
{
    private readonly IAdminLandingLeadService _adminLandingLeadService;

    public AdminLandingLeadsController(IAdminLandingLeadService adminLandingLeadService)
    {
        _adminLandingLeadService = adminLandingLeadService;
    }

    /// <summary>
    /// Lista leads publicos captados na landing para triagem operacional do portal admin.
    /// </summary>
    /// <remarks>
    /// Regras principais:
    /// - Endpoint autenticado para role `Admin`.
    /// - Permite filtrar por origem, busca textual, localidade e periodo.
    /// - Retorna totalizadores por origem para leitura rapida no grid administrativo.
    /// </remarks>
    /// <param name="searchTerm">Busca livre por nome, email, telefone, localidade ou interesse.</param>
    /// <param name="origin">Origem do lead (`Client`, `Provider` ou `all`).</param>
    /// <param name="city">Cidade do lead.</param>
    /// <param name="state">UF do lead.</param>
    /// <param name="fromUtc">Data inicial UTC do recorte.</param>
    /// <param name="toUtc">Data final UTC do recorte.</param>
    /// <param name="page">Pagina solicitada.</param>
    /// <param name="pageSize">Quantidade por pagina.</param>
    /// <response code="200">Lista paginada de leads captados.</response>
    [HttpGet]
    [ProducesResponseType(typeof(AdminLandingLeadsListResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? searchTerm,
        [FromQuery] string? origin,
        [FromQuery] string? city,
        [FromQuery] string? state,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new AdminLandingLeadsQueryDto(searchTerm, origin, city, state, fromUtc, toUtc, page, pageSize);
        var response = await _adminLandingLeadService.GetLandingLeadsAsync(query);
        return Ok(response);
    }

    /// <summary>
    /// Retorna o detalhe completo de um lead publico captado na landing.
    /// </summary>
    /// <remarks>
    /// Regras principais:
    /// - Endpoint autenticado para role `Admin`.
    /// - Exibe dados comerciais, localidade e metadados tecnicos da navegacao.
    /// - Usado pela tela de detalhe do modulo `Leads Landing` no portal admin.
    /// </remarks>
    /// <param name="id">Identificador do lead.</param>
    /// <response code="200">Detalhe completo do lead.</response>
    /// <response code="404">Lead nao encontrado.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AdminLandingLeadDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var lead = await _adminLandingLeadService.GetLandingLeadByIdAsync(id);
        return lead == null ? NotFound() : Ok(lead);
    }
}
