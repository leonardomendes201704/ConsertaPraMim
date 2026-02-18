using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Web.Client.Services;

public interface IClientAuthApiClient
{
    Task<(LoginResponse? Response, string? ErrorMessage)> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<(LoginResponse? Response, string? ErrorMessage)> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
}

