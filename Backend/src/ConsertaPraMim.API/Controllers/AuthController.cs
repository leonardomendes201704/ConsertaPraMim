using Microsoft.AspNetCore.Mvc;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Realiza o login do usuário e retorna um token JWT.
    /// </summary>
    /// <param name="request">Credenciais de acesso.</param>
    /// <returns>Token JWT e dados básicos do usuário.</returns>
    /// <response code="200">Login realizado com sucesso.</response>
    /// <response code="401">Credenciais inválidas.</response>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        if (result == null) return Unauthorized("Invalid credentials");
        return Ok(result);
    }

    /// <summary>
    /// Registra um novo usuário (Cliente ou Prestador).
    /// </summary>
    /// <param name="request">Dados do novo usuário.</param>
    /// <returns>Token JWT de acesso imediato.</returns>
    /// <response code="200">Usuário criado com sucesso.</response>
    /// <response code="400">Dados inválidos ou e-mail já cadastrado.</response>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);
        if (result == null) return BadRequest("User already exists");
        return Ok(result);
    }
}
