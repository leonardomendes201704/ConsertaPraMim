using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.DTOs;
using System.Security.Claims;
using ConsertaPraMim.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace ConsertaPraMim.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ProfileController : ControllerBase
{
    private readonly IProfileService _profileService;
    private readonly IHubContext<ChatHub> _chatHubContext;

    public ProfileController(IProfileService profileService, IHubContext<ChatHub> chatHubContext)
    {
        _profileService = profileService;
        _chatHubContext = chatHubContext;
    }

    /// <summary>
    /// Obtém os dados do perfil do usuário autenticado.
    /// </summary>
    /// <returns>Dados do usuário e do perfil de prestador (se houver).</returns>
    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString)) return Unauthorized();

        var userId = Guid.Parse(userIdString);
        var profile = await _profileService.GetProfileAsync(userId);
        
        if (profile == null) return NotFound();
        return Ok(profile);
    }

    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetProfileByUserId(Guid userId)
    {
        var profile = await _profileService.GetProfileAsync(userId);
        if (profile == null)
        {
            return NotFound();
        }

        return Ok(profile);
    }

    [HttpPut("picture")]
    public async Task<IActionResult> UpdateProfilePicture([FromBody] UpdateProfilePictureDto dto)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        var success = await _profileService.UpdateProfilePictureAsync(userId, dto.ImageUrl?.Trim() ?? string.Empty);
        if (!success)
        {
            return BadRequest("Nao foi possivel atualizar a foto do perfil.");
        }

        return NoContent();
    }

    /// <summary>
    /// Atualiza os dados de atendimento do prestador (Apenas se tiver Role de Provider).
    /// </summary>
    /// <param name="dto">Novas categorias e raio de atendimento.</param>
    /// <response code="204">Perfil atualizado com sucesso.</response>
    /// <response code="400">Falha na atualização (usuário não é um prestador).</response>
    [HttpPut("provider")]
    public async Task<IActionResult> UpdateProviderProfile([FromBody] UpdateProviderProfileDto dto)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString)) return Unauthorized();

        var userId = Guid.Parse(userIdString);
        var success = await _profileService.UpdateProviderProfileAsync(userId, dto);
        
        if (!success) return BadRequest("Could not update provider profile. Ensure you have the provider role.");
        
        return NoContent();
    }

    [Authorize(Roles = "Provider")]
    [HttpPut("provider/status")]
    public async Task<IActionResult> UpdateProviderOperationalStatus([FromBody] UpdateProviderOperationalStatusDto dto)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        var success = await _profileService.UpdateProviderOperationalStatusAsync(userId, dto.OperationalStatus);
        if (!success)
        {
            return BadRequest("Could not update provider status.");
        }

        await _chatHubContext.Clients.Group(ChatHub.BuildProviderStatusGroup(userId)).SendAsync("ReceiveProviderStatus", new
        {
            providerId = userId,
            status = dto.OperationalStatus.ToString(),
            updatedAt = DateTime.UtcNow
        });

        return NoContent();
    }

    [HttpGet("provider/{providerId:guid}/status")]
    public async Task<IActionResult> GetProviderOperationalStatus(Guid providerId)
    {
        var status = await _profileService.GetProviderOperationalStatusAsync(providerId);
        if (!status.HasValue)
        {
            return NotFound();
        }

        return Ok(new
        {
            providerId,
            status = status.Value.ToString()
        });
    }
}
