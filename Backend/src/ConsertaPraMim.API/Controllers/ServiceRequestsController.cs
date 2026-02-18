using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.DTOs;
using System.Security.Claims;

namespace ConsertaPraMim.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Route("api/service-requests")]
public class ServiceRequestsController : ControllerBase
{
    private readonly IServiceRequestService _service;
    private readonly IProviderGalleryService _providerGalleryService;
    private readonly IZipGeocodingService _zipGeocodingService;

    public ServiceRequestsController(
        IServiceRequestService service,
        IProviderGalleryService providerGalleryService,
        IZipGeocodingService zipGeocodingService)
    {
        _service = service;
        _providerGalleryService = providerGalleryService;
        _zipGeocodingService = zipGeocodingService;
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

    /// <summary>
    /// Resolve CEP para coordenadas e endereco de apoio no fluxo de criacao de pedidos.
    /// </summary>
    [HttpGet("zip-resolution")]
    public async Task<IActionResult> ResolveZip([FromQuery] string zipCode)
    {
        if (string.IsNullOrWhiteSpace(zipCode))
        {
            return BadRequest(new { message = "Informe um CEP valido." });
        }

        var resolved = await _zipGeocodingService.ResolveCoordinatesAsync(zipCode);
        if (!resolved.HasValue)
        {
            return NotFound(new { message = "Nao foi possivel localizar esse CEP." });
        }

        return Ok(new
        {
            zipCode = resolved.Value.NormalizedZip,
            latitude = resolved.Value.Latitude,
            longitude = resolved.Value.Longitude,
            street = resolved.Value.Street,
            city = resolved.Value.City
        });
    }

    /// <summary>
    /// Retorna linha do tempo de evidencias anexadas ao pedido.
    /// </summary>
    [HttpGet("{id}/evidences")]
    public async Task<IActionResult> GetEvidences(Guid id)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        var evidences = await _providerGalleryService.GetEvidenceTimelineByServiceRequestAsync(id, userId, role);
        return Ok(evidences);
    }

    /// <summary>
    /// Lista pedidos com proposta aceita/conversao para o prestador autenticado.
    /// </summary>
    [Authorize(Roles = "Provider")]
    [HttpGet("provider/history")]
    public async Task<IActionResult> GetProviderHistory()
    {
        var providerIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(providerIdString) || !Guid.TryParse(providerIdString, out var providerId))
        {
            return Unauthorized();
        }

        var requests = await _service.GetHistoryByProviderAsync(providerId);
        return Ok(requests);
    }

    /// <summary>
    /// Lista pedidos com proposta aceita aguardando/rodando agenda para o prestador autenticado.
    /// </summary>
    [Authorize(Roles = "Provider")]
    [HttpGet("provider/scheduled")]
    public async Task<IActionResult> GetProviderScheduled()
    {
        var providerIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(providerIdString) || !Guid.TryParse(providerIdString, out var providerId))
        {
            return Unauthorized();
        }

        var requests = await _service.GetScheduledByProviderAsync(providerId);
        return Ok(requests);
    }

    /// <summary>
    /// Retorna pins de mapa de pedidos proximos para o prestador autenticado.
    /// </summary>
    [Authorize(Roles = "Provider")]
    [HttpGet("provider/map-pins")]
    public async Task<IActionResult> GetProviderMapPins(
        [FromQuery] double? maxDistanceKm = null,
        [FromQuery] int take = 200)
    {
        var providerIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(providerIdString) || !Guid.TryParse(providerIdString, out var providerId))
        {
            return Unauthorized();
        }

        var pins = await _service.GetMapPinsForProviderAsync(providerId, maxDistanceKm, take);
        return Ok(pins);
    }
}
