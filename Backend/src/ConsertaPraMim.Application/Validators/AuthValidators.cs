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
        RuleFor(x => x.TermsType)
            .NotEmpty().WithMessage("TermsType is required.")
            .Must(IsSupportedTermsType).WithMessage("TermsType must be client or provider.");
        RuleFor(x => x.TermsVersion)
            .GreaterThan(0).WithMessage("TermsVersion must be greater than zero.");
        RuleFor(x => x.TermsAccepted)
            .Equal(true).WithMessage("Terms must be accepted.");
        RuleFor(x => x.TermsAcceptanceSource)
            .MaximumLength(60)
            .When(x => !string.IsNullOrWhiteSpace(x.TermsAcceptanceSource));
    }

    private static bool IsSupportedTermsType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Equals("client", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("provider", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("cliente", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("prestador", StringComparison.OrdinalIgnoreCase);
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
