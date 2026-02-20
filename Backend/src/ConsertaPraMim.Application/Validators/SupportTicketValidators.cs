using ConsertaPraMim.Application.DTOs;
using FluentValidation;

namespace ConsertaPraMim.Application.Validators;

public class CreateSupportTicketRequestValidator : AbstractValidator<MobileProviderCreateSupportTicketRequestDto>
{
    public CreateSupportTicketRequestValidator()
    {
        RuleFor(x => x.Subject)
            .NotEmpty()
            .WithMessage("Assunto do chamado e obrigatorio.")
            .MaximumLength(220)
            .WithMessage("Assunto deve ter no maximo 220 caracteres.");

        RuleFor(x => x.Category)
            .MaximumLength(80)
            .When(x => !string.IsNullOrWhiteSpace(x.Category))
            .WithMessage("Categoria deve ter no maximo 80 caracteres.");

        RuleFor(x => x.Priority)
            .InclusiveBetween(1, 4)
            .When(x => x.Priority.HasValue)
            .WithMessage("Prioridade invalida.");

        RuleFor(x => x.InitialMessage)
            .NotEmpty()
            .WithMessage("Mensagem inicial do chamado e obrigatoria.")
            .MaximumLength(3000)
            .WithMessage("Mensagem deve ter no maximo 3000 caracteres.");
    }
}

public class SupportTicketMessageRequestValidator : AbstractValidator<MobileProviderSupportTicketMessageRequestDto>
{
    public SupportTicketMessageRequestValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty()
            .WithMessage("Mensagem do chamado e obrigatoria.")
            .MaximumLength(3000)
            .WithMessage("Mensagem deve ter no maximo 3000 caracteres.");
    }
}
