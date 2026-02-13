using FluentValidation;
using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Validators;

public class CreateServiceRequestValidator : AbstractValidator<CreateServiceRequestDto>
{
    public CreateServiceRequestValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MinimumLength(10).MaximumLength(1000);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Zip)
            .NotEmpty()
            .Matches(@"^\d{5}-?\d{3}$")
            .WithMessage("Informe um CEP valido.");
    }
}
