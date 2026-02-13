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

    [Fact]
    public void CreateServiceRequestValidator_ShouldFail_WhenDescriptionShort()
    {
        var dto = new CreateServiceRequestDto(ServiceCategory.Electrical, "too short", "Street", "City", "123", 0, 0);
        var result = _requestValidator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void CreateServiceRequestValidator_ShouldPass_WhenValid()
    {
        var dto = new CreateServiceRequestDto(ServiceCategory.Electrical, "This is a long enough description", "Street", "City", "11704150", -23.0, -46.0);
        var result = _requestValidator.TestValidate(dto);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void RegisterRequestValidator_ShouldFail_WhenEmailInvalid()
    {
        var dto = new RegisterRequest("Name", "invalid-email", "pass123", "1234567890", 1);
        var result = _registerValidator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void RegisterRequestValidator_ShouldFail_WhenPhoneShort()
    {
        var dto = new RegisterRequest("Name", "test@test.com", "pass123", "123", 1);
        var result = _registerValidator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Phone);
    }

    [Fact]
    public void RegisterRequestValidator_ShouldFail_WhenRoleIsAdmin()
    {
        var dto = new RegisterRequest("Name", "test@test.com", "pass123", "11999999999", (int)UserRole.Admin);
        var result = _registerValidator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Role);
    }
}
