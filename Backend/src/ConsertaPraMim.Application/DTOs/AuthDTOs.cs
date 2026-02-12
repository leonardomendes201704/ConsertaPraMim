namespace ConsertaPraMim.Application.DTOs;

public record LoginRequest(string Email, string Password);
public record LoginResponse(string Token, string UserName, string Role, string Email);
public record RegisterRequest(string Name, string Email, string Password, string Phone, int Role);
