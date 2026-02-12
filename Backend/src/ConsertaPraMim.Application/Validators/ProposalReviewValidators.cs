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
