using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.DTOs;
using System.Security.Claims;

namespace ConsertaPraMim.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ServiceRequestsController : ControllerBase
{
    private readonly IServiceRequestService _service;

    public ServiceRequestsController(IServiceRequestService service)
    {
        _service = service;
    }

    /// <summary>
    /// Cria um novo pedido de serviço (Apenas Clientes).
    /// </summary>
    /// <param name="dto">Dados do serviço solicitado.</param>
    /// <returns>ID do pedido criado.</returns>
    /// <response code="201">Pedido criado com sucesso.</response>
    /// <response code="401">Usuário não autenticado.</response>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateServiceRequestDto dto)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
        
        var userId = Guid.Parse(userIdString);
        Guid id;
        try
        {
            id = await _service.CreateAsync(userId, dto);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    /// <summary>
    /// Lista todos os pedidos relevantes para o usuário conectado.
    /// Clientes vêem seus próprios pedidos. Prestadores vêem pedidos próximos ao seu raio e categorias.
    /// </summary>
    /// <returns>Lista de pedidos de serviço.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "Client";
        
        if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
        
        var userId = Guid.Parse(userIdString);
        var requests = await _service.GetAllAsync(userId, role);
        
        return Ok(requests);
    }

    /// <summary>
    /// Obtém detalhes de um pedido específico pelo ID.
    /// </summary>
    /// <param name="id">ID único do pedido.</param>
    /// <returns>Detalhes do pedido.</returns>
    /// <response code="200">Pedido encontrado.</response>
    /// <response code="404">Pedido não encontrado.</response>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        var request = await _service.GetByIdAsync(id, userId, role);
        if (request == null) return NotFound();
        return Ok(request);
    }
}
