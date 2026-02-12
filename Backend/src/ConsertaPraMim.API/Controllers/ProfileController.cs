using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.DTOs;
using System.Security.Claims;

namespace ConsertaPraMim.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ProfileController : ControllerBase
{
    private readonly IProfileService _profileService;

    public ProfileController(IProfileService profileService)
    {
        _profileService = profileService;
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
}
