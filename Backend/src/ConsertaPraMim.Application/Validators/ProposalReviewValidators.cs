using FluentValidation;
using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Validators;

public class CreateProposalValidator : AbstractValidator<CreateProposalDto>
{
    public CreateProposalValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.EstimatedValue).GreaterThan(0).When(x => x.EstimatedValue.HasValue);
        RuleFor(x => x.Message).MaximumLength(500);
        RuleFor(x => x.EstimatedLeadTimeHours)
            .InclusiveBetween(1, 720)
            .When(x => x.EstimatedLeadTimeHours.HasValue);
        RuleFor(x => x.WarrantyDays)
            .InclusiveBetween(0, 3650)
            .When(x => x.WarrantyDays.HasValue);
    }
}

public class CreateReviewValidator : AbstractValidator<CreateReviewDto>
{
    public CreateReviewValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Comment).MaximumLength(500);
    }
}
