using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;

namespace ConsertaPraMim.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IConfiguration _configuration;

    public AuthService(IUserRepository userRepository, IConfiguration configuration)
    {
        _userRepository = userRepository;
        _configuration = configuration;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user == null) return null;
        
        // MVP: Plain password comparison (TODO: Use BCrypt)
        if (user.PasswordHash != request.Password) return null;

        var token = GenerateJwtToken(user);
        return new LoginResponse(token, user.Name, user.Role.ToString(), user.Email);
    }

    public async Task<LoginResponse?> RegisterAsync(RegisterRequest request)
    {
        if (await _userRepository.GetByEmailAsync(request.Email) != null)
            return null; // User exists
            
        var user = new User
        {
            Name = request.Name,
            Email = request.Email,
            PasswordHash = request.Password, // MVP: Warning
            Phone = request.Phone,
            Role = (UserRole)request.Role,
            IsActive = true
        };

        await _userRepository.AddAsync(user);
        var token = GenerateJwtToken(user);
        return new LoginResponse(token, user.Name, user.Role.ToString(), user.Email);
    }

    private string GenerateJwtToken(User user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] ?? "ConsertaPraMimSuperSecretKeyForDevelopmentOnly123!";
        var key = Encoding.ASCII.GetBytes(secretKey);

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
