using FluentValidation;
using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Validators;

public class CreateProposalValidator : AbstractValidator<CreateProposalDto>
{
    private const int MinScopeMessageLength = 20;

    public CreateProposalValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.EstimatedValue).GreaterThan(0).When(x => x.EstimatedValue.HasValue);

        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Escopo da proposta e obrigatorio.")
            .MinimumLength(MinScopeMessageLength).WithMessage($"Escopo da proposta deve ter ao menos {MinScopeMessageLength} caracteres.")
            .MaximumLength(500).WithMessage("Escopo da proposta deve ter no maximo 500 caracteres.");

        RuleFor(x => x.EstimatedLeadTimeHours)
            .NotNull().WithMessage("Prazo estimado da proposta e obrigatorio.")
            .InclusiveBetween(1, 720)
            .When(x => x.EstimatedLeadTimeHours.HasValue)
            .WithMessage("Prazo estimado deve estar entre 1 e 720 horas.");

        RuleFor(x => x.WarrantyDays)
            .NotNull().WithMessage("Garantia da proposta e obrigatoria.")
            .InclusiveBetween(0, 3650)
            .When(x => x.WarrantyDays.HasValue)
            .WithMessage("Garantia deve estar entre 0 e 3650 dias.");
    }
}

public class CreateReviewValidator : AbstractValidator<CreateReviewDto>
{
    public CreateReviewValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Comment).MaximumLength(500);
        RuleFor(x => x.ServiceQualityRating)
            .InclusiveBetween(1, 5)
            .When(x => x.ServiceQualityRating.HasValue);
        RuleFor(x => x.PunctualityRating)
            .InclusiveBetween(1, 5)
            .When(x => x.PunctualityRating.HasValue);
        RuleFor(x => x.CommunicationRating)
            .InclusiveBetween(1, 5)
            .When(x => x.CommunicationRating.HasValue);
        RuleFor(x => x.CostBenefitRating)
            .InclusiveBetween(1, 5)
            .When(x => x.CostBenefitRating.HasValue);
        RuleFor(x => x.NpsScore)
            .InclusiveBetween(0, 10)
            .When(x => x.NpsScore.HasValue);
    }
}
