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
