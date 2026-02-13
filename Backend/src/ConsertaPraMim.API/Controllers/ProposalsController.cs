using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.DTOs;
using System.Security.Claims;

namespace ConsertaPraMim.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ProposalsController : ControllerBase
{
    private readonly IProposalService _proposalService;

    public ProposalsController(IProposalService proposalService)
    {
        _proposalService = proposalService;
    }

    /// <summary>
    /// Envia uma proposta para um pedido de serviço (Apenas Prestadores).
    /// </summary>
    /// <param name="dto">Detalhes da proposta.</param>
    /// <returns>ID da proposta criada.</returns>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProposalDto dto)
    {
        var providerIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(providerIdString)) return Unauthorized();

        var providerId = Guid.Parse(providerIdString);
        var id = await _proposalService.CreateAsync(providerId, dto);
        
        return Ok(new { id });
    }

    /// <summary>
    /// Lista todas as propostas feitas para um pedido específico.
    /// </summary>
    /// <param name="requestId">ID do pedido de serviço.</param>
    [HttpGet("request/{requestId}")]
    public async Task<IActionResult> GetByRequest(Guid requestId)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        var proposals = await _proposalService.GetByRequestAsync(requestId, userId, role);
        return Ok(proposals);
    }

    /// <summary>
    /// Lista as propostas enviadas pelo prestador autenticado.
    /// </summary>
    [HttpGet("my-proposals")]
    public async Task<IActionResult> GetMyProposals()
    {
        var providerIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(providerIdString)) return Unauthorized();

        var providerId = Guid.Parse(providerIdString);
        var proposals = await _proposalService.GetByProviderAsync(providerId);
        return Ok(proposals);
    }

    /// <summary>
    /// Aceita uma proposta de serviço (Apenas o Cliente dono do pedido).
    /// Isso muda o status do pedido para 'Scheduled'.
    /// </summary>
    /// <param name="id">ID da proposta a ser aceita.</param>
    /// <response code="204">Proposta aceita com sucesso.</response>
    /// <response code="400">Falha ao aceitar (não é o dono ou proposta inexistente).</response>
    [HttpPut("{id}/accept")]
    public async Task<IActionResult> Accept(Guid id)
    {
        var clientIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(clientIdString)) return Unauthorized();

        var clientId = Guid.Parse(clientIdString);
        var success = await _proposalService.AcceptAsync(id, clientId);
        
        if (!success) return BadRequest("Could not accept proposal. Check if you are the client of the request or if the proposal exists.");
        
        return NoContent();
    }
}
