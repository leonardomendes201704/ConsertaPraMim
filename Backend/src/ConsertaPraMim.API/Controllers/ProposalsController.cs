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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProposalDto dto)
    {
        var providerIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(providerIdString)) return Unauthorized();

        var providerId = Guid.Parse(providerIdString);
        var id = await _proposalService.CreateAsync(providerId, dto);
        
        return Ok(new { id });
    }

    [HttpGet("request/{requestId}")]
    public async Task<IActionResult> GetByRequest(Guid requestId)
    {
        var proposals = await _proposalService.GetByRequestAsync(requestId);
        return Ok(proposals);
    }

    [HttpGet("my-proposals")]
    public async Task<IActionResult> GetMyProposals()
    {
        var providerIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(providerIdString)) return Unauthorized();

        var providerId = Guid.Parse(providerIdString);
        var proposals = await _proposalService.GetByProviderAsync(providerId);
        return Ok(proposals);
    }

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
