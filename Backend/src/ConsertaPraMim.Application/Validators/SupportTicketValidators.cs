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
    private const int MaxAttachmentsPerMessage = 10;
    private const long MaxAttachmentSizeBytes = 25_000_000;

    public SupportTicketMessageRequestValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty()
            .WithMessage("Mensagem do chamado e obrigatoria.")
            .MaximumLength(3000)
            .WithMessage("Mensagem deve ter no maximo 3000 caracteres.");

        RuleFor(x => x.Attachments)
            .Must(attachments => attachments == null || attachments.Count <= MaxAttachmentsPerMessage)
            .WithMessage($"Mensagem suporta no maximo {MaxAttachmentsPerMessage} anexos.");

        RuleForEach(x => x.Attachments)
            .ChildRules(attachment =>
            {
                attachment.RuleFor(x => x.FileUrl)
                    .NotEmpty()
                    .WithMessage("Url do anexo e obrigatoria.")
                    .MaximumLength(700)
                    .WithMessage("Url do anexo deve ter no maximo 700 caracteres.");

                attachment.RuleFor(x => x.FileName)
                    .NotEmpty()
                    .WithMessage("Nome do arquivo e obrigatorio.")
                    .MaximumLength(255)
                    .WithMessage("Nome do arquivo deve ter no maximo 255 caracteres.");

                attachment.RuleFor(x => x.ContentType)
                    .NotEmpty()
                    .WithMessage("ContentType do arquivo e obrigatorio.")
                    .MaximumLength(120)
                    .WithMessage("ContentType deve ter no maximo 120 caracteres.");

                attachment.RuleFor(x => x.SizeBytes)
                    .GreaterThan(0)
                    .WithMessage("Tamanho do anexo deve ser maior que zero.")
                    .LessThanOrEqualTo(MaxAttachmentSizeBytes)
                    .WithMessage($"Arquivo excede o limite de {MaxAttachmentSizeBytes / 1_000_000}MB.");
            });
    }
}

public class AdminSupportTicketMessageRequestValidator : AbstractValidator<AdminSupportTicketMessageRequestDto>
{
    private const int MaxAttachmentsPerMessage = 10;
    private const long MaxAttachmentSizeBytes = 25_000_000;

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

        RuleFor(x => x.Attachments)
            .Must(attachments => attachments == null || attachments.Count <= MaxAttachmentsPerMessage)
            .WithMessage($"Mensagem suporta no maximo {MaxAttachmentsPerMessage} anexos.");

        RuleForEach(x => x.Attachments)
            .ChildRules(attachment =>
            {
                attachment.RuleFor(x => x.FileUrl)
                    .NotEmpty()
                    .WithMessage("Url do anexo e obrigatoria.")
                    .MaximumLength(700)
                    .WithMessage("Url do anexo deve ter no maximo 700 caracteres.");

                attachment.RuleFor(x => x.FileName)
                    .NotEmpty()
                    .WithMessage("Nome do arquivo e obrigatorio.")
                    .MaximumLength(255)
                    .WithMessage("Nome do arquivo deve ter no maximo 255 caracteres.");

                attachment.RuleFor(x => x.ContentType)
                    .NotEmpty()
                    .WithMessage("ContentType do arquivo e obrigatorio.")
                    .MaximumLength(120)
                    .WithMessage("ContentType deve ter no maximo 120 caracteres.");

                attachment.RuleFor(x => x.SizeBytes)
                    .GreaterThan(0)
                    .WithMessage("Tamanho do anexo deve ser maior que zero.")
                    .LessThanOrEqualTo(MaxAttachmentSizeBytes)
                    .WithMessage($"Arquivo excede o limite de {MaxAttachmentSizeBytes / 1_000_000}MB.");
            });
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
