using ConsertaPraMim.Application.Validators;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Enums;
using Xunit;
using FluentValidation.TestHelper;

namespace ConsertaPraMim.Tests.Unit.Validators;

public class ValidatorTests
{
    private readonly CreateServiceRequestValidator _requestValidator = new();
    private readonly RegisterRequestValidator _registerValidator = new();

    /// <summary>
    /// Cenario: cliente tenta abrir pedido com descricao curta demais para detalhar o servico.
    /// Passos: monta CreateServiceRequestDto com descricao abaixo do minimo e executa o validator de criacao.
    /// Resultado esperado: o campo Description recebe erro de validacao e o cadastro do pedido deve ser bloqueado.
    /// </summary>
    [Fact(DisplayName = "Validator | Criar servico requisicao validator | Deve falhar quando description short")]
    public void CreateServiceRequestValidator_ShouldFail_WhenDescriptionShort()
    {
        var dto = new CreateServiceRequestDto(
            CategoryId: null,
            Category: ServiceCategory.Electrical,
            Description: "too short",
            Street: "Street",
            City: "City",
            Zip: "123",
            Lat: 0,
            Lng: 0);
        var result = _requestValidator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    /// <summary>
    /// Cenario: cliente preenche corretamente os dados obrigatorios para abrir uma solicitacao.
    /// Passos: cria DTO com descricao completa, CEP e coordenadas validas e submete ao validator.
    /// Resultado esperado: nenhuma violacao de regra e payload apto para seguir ao fluxo de criacao.
    /// </summary>
    [Fact(DisplayName = "Validator | Criar servico requisicao validator | Deve pass quando valido")]
    public void CreateServiceRequestValidator_ShouldPass_WhenValid()
    {
        var dto = new CreateServiceRequestDto(
            CategoryId: null,
            Category: ServiceCategory.Electrical,
            Description: "This is a long enough description",
            Street: "Street",
            City: "City",
            Zip: "11704150",
            Lat: -23.0,
            Lng: -46.0);
        var result = _requestValidator.TestValidate(dto);
        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>
    /// Cenario: usuario tenta registrar conta com formato de e-mail invalido.
    /// Passos: monta RegisterRequest com e-mail sem padrao RFC e executa o validator de cadastro.
    /// Resultado esperado: erro de validacao no campo Email para impedir registro com contato incorreto.
    /// </summary>
    [Fact(DisplayName = "Validator | Register requisicao validator | Deve falhar quando email invalido")]
    public void RegisterRequestValidator_ShouldFail_WhenEmailInvalid()
    {
        var dto = new RegisterRequest("Name", "invalid-email", "pass123", "1234567890", 1);
        var result = _registerValidator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    /// <summary>
    /// Cenario: usuario informa telefone incompleto no cadastro.
    /// Passos: cria RegisterRequest com telefone abaixo do tamanho minimo aceito e valida o payload.
    /// Resultado esperado: o campo Phone deve ser rejeitado, evitando conta sem numero util para contato.
    /// </summary>
    [Fact(DisplayName = "Validator | Register requisicao validator | Deve falhar quando phone short")]
    public void RegisterRequestValidator_ShouldFail_WhenPhoneShort()
    {
        var dto = new RegisterRequest("Name", "test@test.com", "pass123", "123", 1);
        var result = _registerValidator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Phone);
    }

    /// <summary>
    /// Cenario: tentativa de auto cadastro com perfil administrativo.
    /// Passos: envia RegisterRequest com role Admin para o validator de registro publico.
    /// Resultado esperado: erro no campo Role, garantindo que perfil admin nao seja criado por cadastro aberto.
    /// </summary>
    [Fact(DisplayName = "Validator | Register requisicao validator | Deve falhar quando role admin")]
    public void RegisterRequestValidator_ShouldFail_WhenRoleIsAdmin()
    {
        var dto = new RegisterRequest("Name", "test@test.com", "pass123", "11999999999", (int)UserRole.Admin);
        var result = _registerValidator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Role);
    }
}
