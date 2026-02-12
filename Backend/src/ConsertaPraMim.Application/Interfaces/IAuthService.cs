using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Entities;

namespace ConsertaPraMim.Application.Interfaces;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    Task<LoginResponse?> RegisterAsync(RegisterRequest request);
}
