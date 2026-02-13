using FluentValidation;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8).WithMessage("A senha deve ter no minimo 8 caracteres.")
            .Matches(@"[A-Z]").WithMessage("A senha deve conter pelo menos uma letra maiuscula.")
            .Matches(@"[a-z]").WithMessage("A senha deve conter pelo menos uma letra minuscula.")
            .Matches(@"\d").WithMessage("A senha deve conter pelo menos um numero.")
            .Matches(@"[^a-zA-Z0-9]").WithMessage("A senha deve conter pelo menos um caractere especial.");
        RuleFor(x => x.Phone).NotEmpty().Matches(@"^\d{10,11}$").WithMessage("Phone must be 10 or 11 digits.");
        RuleFor(x => x.Role)
            .Must(role => role == (int)UserRole.Client || role == (int)UserRole.Provider)
            .WithMessage("Role not allowed for public registration.");
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}
