using FluentValidation;
using ConsertaPraMim.Application.DTOs;
using System;

namespace ConsertaPraMim.Application.Validators;

public class CreateServiceRequestValidator : AbstractValidator<CreateServiceRequestDto>
{
    public CreateServiceRequestValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MinimumLength(10).MaximumLength(1000);
        RuleFor(x => x)
            .Must(x => x.CategoryId.HasValue || x.Category.HasValue)
            .WithMessage("Selecione uma categoria valida.");

        RuleFor(x => x.CategoryId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("Selecione uma categoria valida.");

        RuleFor(x => x.Category)
            .IsInEnum()
            .When(x => x.Category.HasValue);

        RuleFor(x => x.Zip)
            .NotEmpty()
            .Matches(@"^\d{5}-?\d{3}$")
            .WithMessage("Informe um CEP valido.");

        RuleFor(x => x.ProblemAnalysisSummary)
            .MaximumLength(1000)
            .When(x => !string.IsNullOrWhiteSpace(x.ProblemAnalysisSummary))
            .WithMessage("Resumo da analise deve ter no maximo 1000 caracteres.");

        RuleFor(x => x.ProblemAnalysisHighlightsJson)
            .MaximumLength(4000)
            .When(x => !string.IsNullOrWhiteSpace(x.ProblemAnalysisHighlightsJson))
            .WithMessage("Highlights da analise excedem o limite permitido.");

        RuleFor(x => x.Neighborhood)
            .MaximumLength(120)
            .When(x => !string.IsNullOrWhiteSpace(x.Neighborhood))
            .WithMessage("Bairro deve ter no maximo 120 caracteres.");
    }
}
