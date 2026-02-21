using Moq;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _configurationMock = new Mock<IConfiguration>();
        
        // Mocking SecretKey
        var jwtSection = new Mock<IConfigurationSection>();
        jwtSection.Setup(s => s["SecretKey"]).Returns("ConsertaPraMimSuperSecretKeyForTestingOnly123!");
        _configurationMock.Setup(c => c.GetSection("JwtSettings")).Returns(jwtSection.Object);

        _authService = new AuthService(_userRepositoryMock.Object, _configurationMock.Object);
    }

    /// <summary>
    /// Cenario: novo cliente realiza cadastro com dados validos e e-mail ainda nao utilizado.
    /// Passos: mocka repositorio sem usuario existente e executa RegisterAsync com payload consistente.
    /// Resultado esperado: servico retorna resposta de cadastro e persiste o novo usuario.
    /// </summary>
    [Fact(DisplayName = "Autenticacao servico | Register | Deve retornar resposta quando valido requisicao")]
    public async Task RegisterAsync_ShouldReturnResponse_WhenValidRequest()
    {
        // Arrange
        var request = new RegisterRequest("Test User", "test@test.com", "password123", "1234567890", (int)UserRole.Client);
        _userRepositoryMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
        _userRepositoryMock.Setup(r => r.AddAsync(It.IsAny<User>())).ReturnsAsync((User u) => u);

        // Act
        var result = await _authService.RegisterAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(request.Email, result.Email);
        _userRepositoryMock.Verify(r => r.AddAsync(It.Is<User>(u => u.Email == request.Email && u.Name == request.Name)), Times.Once);
    }

    /// <summary>
    /// Cenario: tentativa de cadastro com e-mail ja registrado anteriormente.
    /// Passos: repositorio devolve usuario existente para o mesmo e-mail e RegisterAsync e chamado.
    /// Resultado esperado: retorno nulo e nenhuma operacao de insercao e executada.
    /// </summary>
    [Fact(DisplayName = "Autenticacao servico | Register | Deve retornar nulo quando email already existe")]
    public async Task RegisterAsync_ShouldReturnNull_WhenEmailAlreadyExists()
    {
        // Arrange
        var request = new RegisterRequest("Test User", "test@test.com", "password123", "1234567890", (int)UserRole.Client);
        _userRepositoryMock.Setup(r => r.GetByEmailAsync(request.Email)).ReturnsAsync(new User { Email = request.Email });

        // Act
        var result = await _authService.RegisterAsync(request);

        // Assert
        Assert.Null(result);
        _userRepositoryMock.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
    }

    /// <summary>
    /// Cenario: auto cadastro com role administrativa deve ser bloqueado por regra de seguranca.
    /// Passos: envia RegisterRequest com role Admin e e-mail inedito.
    /// Resultado esperado: servico recusa cadastro retornando nulo sem persistir usuario.
    /// </summary>
    [Fact(DisplayName = "Autenticacao servico | Register | Deve retornar nulo quando trying para self register como admin")]
    public async Task RegisterAsync_ShouldReturnNull_WhenTryingToSelfRegisterAsAdmin()
    {
        // Arrange
        var request = new RegisterRequest("Admin User", "admin@test.com", "password123", "1234567890", (int)UserRole.Admin);
        _userRepositoryMock.Setup(r => r.GetByEmailAsync(request.Email)).ReturnsAsync((User?)null);

        // Act
        var result = await _authService.RegisterAsync(request);

        // Assert
        Assert.Null(result);
        _userRepositoryMock.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
    }

    /// <summary>
    /// Cenario: payload de cadastro informa role fora do dominio conhecido da aplicacao.
    /// Passos: chama RegisterAsync com valor numerico invalido para role.
    /// Resultado esperado: cadastro e rejeitado com retorno nulo e sem gravação.
    /// </summary>
    [Fact(DisplayName = "Autenticacao servico | Register | Deve retornar nulo quando role invalido")]
    public async Task RegisterAsync_ShouldReturnNull_WhenRoleIsInvalid()
    {
        // Arrange
        var request = new RegisterRequest("Unknown Role", "unknown@test.com", "password123", "1234567890", 12345);
        _userRepositoryMock.Setup(r => r.GetByEmailAsync(request.Email)).ReturnsAsync((User?)null);

        // Act
        var result = await _authService.RegisterAsync(request);

        // Assert
        Assert.Null(result);
        _userRepositoryMock.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
    }

    /// <summary>
    /// Cenario: usuario autenticado informa credenciais corretas no login.
    /// Passos: repositorio devolve usuario com hash compatível e LoginAsync e executado.
    /// Resultado esperado: resposta contem dados do usuario e token JWT nao vazio.
    /// </summary>
    [Fact(DisplayName = "Autenticacao servico | Login | Deve retornar resposta quando credentials valido")]
    public async Task LoginAsync_ShouldReturnResponse_WhenCredentialsAreValid()
    {
        // Arrange
        var email = "test@test.com";
        var password = "password123";
        var user = new User 
        { 
            Id = Guid.NewGuid(), 
            Email = email, 
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Name = "Test User",
            Role = UserRole.Client 
        };

        _userRepositoryMock.Setup(r => r.GetByEmailAsync(email)).ReturnsAsync(user);

        // Act
        var response = await _authService.LoginAsync(new LoginRequest(email, password));

        // Assert
        Assert.NotNull(response);
        Assert.Equal(user.Email, response.Email);
        Assert.False(string.IsNullOrEmpty(response.Token));
    }

    /// <summary>
    /// Cenario: senha informada no login nao corresponde ao hash armazenado.
    /// Passos: usuario existe no repositorio, mas LoginAsync recebe senha incorreta.
    /// Resultado esperado: autenticacao falha com retorno nulo.
    /// </summary>
    [Fact(DisplayName = "Autenticacao servico | Login | Deve retornar nulo quando password incorrect")]
    public async Task LoginAsync_ShouldReturnNull_WhenPasswordIsIncorrect()
    {
        // Arrange
        var email = "test@test.com";
        var user = new User { Email = email, PasswordHash = BCrypt.Net.BCrypt.HashPassword("correctPassword") };
        _userRepositoryMock.Setup(r => r.GetByEmailAsync(email)).ReturnsAsync(user);

        // Act
        var response = await _authService.LoginAsync(new LoginRequest(email, "wrongPassword"));

        // Assert
        Assert.Null(response);
    }
}
