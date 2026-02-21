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

    [Fact(DisplayName = "Validator | Register requisicao validator | Deve falhar quando email invalido")]
    public void RegisterRequestValidator_ShouldFail_WhenEmailInvalid()
    {
        var dto = new RegisterRequest("Name", "invalid-email", "pass123", "1234567890", 1);
        var result = _registerValidator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact(DisplayName = "Validator | Register requisicao validator | Deve falhar quando phone short")]
    public void RegisterRequestValidator_ShouldFail_WhenPhoneShort()
    {
        var dto = new RegisterRequest("Name", "test@test.com", "pass123", "123", 1);
        var result = _registerValidator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Phone);
    }

    [Fact(DisplayName = "Validator | Register requisicao validator | Deve falhar quando role admin")]
    public void RegisterRequestValidator_ShouldFail_WhenRoleIsAdmin()
    {
        var dto = new RegisterRequest("Name", "test@test.com", "pass123", "11999999999", (int)UserRole.Admin);
        var result = _registerValidator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Role);
    }
}
