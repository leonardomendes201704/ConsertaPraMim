using FluentValidation;
using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Validators;

public class UpdateProviderProfileValidator : AbstractValidator<UpdateProviderProfileDto>
{
    public UpdateProviderProfileValidator()
    {
        RuleFor(x => x.RadiusKm).InclusiveBetween(1, 100);
        RuleFor(x => x.BaseLatitude).InclusiveBetween(-90, 90).When(x => x.BaseLatitude.HasValue);
        RuleFor(x => x.BaseLongitude).InclusiveBetween(-180, 180).When(x => x.BaseLongitude.HasValue);
        RuleFor(x => x.Categories).NotEmpty().WithMessage("At least one category must be selected.");
    }
}
