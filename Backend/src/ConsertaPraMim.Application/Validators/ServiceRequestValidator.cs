using FluentValidation;
using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Validators;

public class CreateServiceRequestValidator : AbstractValidator<CreateServiceRequestDto>
{
    public CreateServiceRequestValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MinimumLength(10).MaximumLength(1000);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.City).NotEmpty();
        RuleFor(x => x.Street).NotEmpty();
        RuleFor(x => x.Zip).NotEmpty();
        RuleFor(x => x.Lat).InclusiveBetween(-90, 90);
        RuleFor(x => x.Lng).InclusiveBetween(-180, 180);
    }
}
