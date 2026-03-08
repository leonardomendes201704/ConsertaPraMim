using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Enums;
using FluentValidation;

namespace ConsertaPraMim.Application.Validators;

public sealed class CaptureLandingLeadRequestValidator : AbstractValidator<CaptureLandingLeadRequestDto>
{
    public CaptureLandingLeadRequestValidator()
    {
        RuleFor(x => x.Origin)
            .IsInEnum();

        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(160);

        RuleFor(x => x.Phone)
            .NotEmpty()
            .Must(HasMinimumPhoneDigits)
            .WithMessage("Informe um telefone valido com DDD.")
            .MaximumLength(40);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(200);

        RuleFor(x => x.City)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(x => x.State)
            .NotEmpty()
            .Length(2)
            .WithMessage("Informe a UF com 2 letras.");

        RuleFor(x => x.Neighborhood)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(x => x.ServiceCategory)
            .MaximumLength(120);

        RuleFor(x => x.RequestedService)
            .MaximumLength(220);

        RuleFor(x => x.CompanyName)
            .MaximumLength(180);

        RuleFor(x => x.CompanyDocument)
            .MaximumLength(32);

        RuleFor(x => x.Message)
            .MaximumLength(1600);

        RuleFor(x => x.CurrentPageUrl)
            .MaximumLength(500);

        RuleFor(x => x.ReferrerUrl)
            .MaximumLength(500);

        RuleFor(x => x.QueryString)
            .MaximumLength(2000);

        RuleFor(x => x.UtmSource)
            .MaximumLength(180);
        RuleFor(x => x.UtmMedium)
            .MaximumLength(180);
        RuleFor(x => x.UtmCampaign)
            .MaximumLength(180);
        RuleFor(x => x.UtmTerm)
            .MaximumLength(180);
        RuleFor(x => x.UtmContent)
            .MaximumLength(180);
        RuleFor(x => x.BrowserLanguage)
            .MaximumLength(128);
        RuleFor(x => x.ScreenResolution)
            .MaximumLength(32);
        RuleFor(x => x.DevicePlatform)
            .MaximumLength(80);
        RuleFor(x => x.TimeZone)
            .MaximumLength(128);

        When(x => x.Origin == LandingLeadOrigin.Client, () =>
        {
            RuleFor(x => x)
                .Must(x => !string.IsNullOrWhiteSpace(x.ServiceCategory) || !string.IsNullOrWhiteSpace(x.RequestedService) || !string.IsNullOrWhiteSpace(x.Message))
                .WithMessage("Informe pelo menos a categoria, o servico desejado ou uma descricao do problema.");
        });

        When(x => x.Origin == LandingLeadOrigin.Provider, () =>
        {
            RuleFor(x => x.ServiceCategory)
                .NotEmpty()
                .WithMessage("Informe a especialidade principal do prestador.");

            RuleFor(x => x.YearsOfExperience)
                .InclusiveBetween(0, 60)
                .When(x => x.YearsOfExperience.HasValue);
        });
    }

    private static bool HasMinimumPhoneDigits(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return false;
        }

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length >= 10;
    }
}
