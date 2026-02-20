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

public class AdminSupportTicketMessageRequestValidator : AbstractValidator<AdminSupportTicketMessageRequestDto>
{
    public AdminSupportTicketMessageRequestValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty()
            .WithMessage("Mensagem do chamado e obrigatoria.")
            .MaximumLength(3000)
            .WithMessage("Mensagem deve ter no maximo 3000 caracteres.");

        RuleFor(x => x.MessageType)
            .MaximumLength(40)
            .When(x => !string.IsNullOrWhiteSpace(x.MessageType))
            .WithMessage("Tipo de mensagem deve ter no maximo 40 caracteres.");

        RuleFor(x => x.MetadataJson)
            .MaximumLength(4000)
            .When(x => !string.IsNullOrWhiteSpace(x.MetadataJson))
            .WithMessage("Metadados devem ter no maximo 4000 caracteres.");
    }
}

public class AdminSupportTicketStatusUpdateRequestValidator : AbstractValidator<AdminSupportTicketStatusUpdateRequestDto>
{
    public AdminSupportTicketStatusUpdateRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty()
            .WithMessage("Status do chamado e obrigatorio.");

        RuleFor(x => x.Note)
            .MaximumLength(3000)
            .When(x => !string.IsNullOrWhiteSpace(x.Note))
            .WithMessage("Nota deve ter no maximo 3000 caracteres.");
    }
}

public class AdminSupportTicketAssignRequestValidator : AbstractValidator<AdminSupportTicketAssignRequestDto>
{
    public AdminSupportTicketAssignRequestValidator()
    {
        RuleFor(x => x.Note)
            .MaximumLength(3000)
            .When(x => !string.IsNullOrWhiteSpace(x.Note))
            .WithMessage("Nota deve ter no maximo 3000 caracteres.");
    }
}
