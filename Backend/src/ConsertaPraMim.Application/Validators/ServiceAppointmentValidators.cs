using ConsertaPraMim.Application.DTOs;
using FluentValidation;

namespace ConsertaPraMim.Application.Validators;

public class GetServiceAppointmentSlotsQueryValidator : AbstractValidator<GetServiceAppointmentSlotsQueryDto>
{
    public GetServiceAppointmentSlotsQueryValidator()
    {
        RuleFor(x => x.ProviderId)
            .NotEmpty()
            .WithMessage("Prestador invalido.");

        RuleFor(x => x.FromUtc)
            .LessThan(x => x.ToUtc)
            .WithMessage("Periodo de consulta invalido.");

        RuleFor(x => x)
            .Must(x => (x.ToUtc - x.FromUtc).TotalDays <= 31)
            .WithMessage("A consulta de slots permite no maximo 31 dias.");

        RuleFor(x => x.SlotDurationMinutes)
            .InclusiveBetween(15, 240)
            .When(x => x.SlotDurationMinutes.HasValue)
            .WithMessage("Duracao de slot deve estar entre 15 e 240 minutos.");
    }
}

public class CreateServiceAppointmentRequestValidator : AbstractValidator<CreateServiceAppointmentRequestDto>
{
    public CreateServiceAppointmentRequestValidator()
    {
        RuleFor(x => x.ServiceRequestId)
            .NotEmpty()
            .WithMessage("Pedido invalido.");

        RuleFor(x => x.ProviderId)
            .NotEmpty()
            .WithMessage("Prestador invalido.");

        RuleFor(x => x.WindowStartUtc)
            .LessThan(x => x.WindowEndUtc)
            .WithMessage("Janela de horario invalida.");

        RuleFor(x => x)
            .Must(x => (x.WindowEndUtc - x.WindowStartUtc).TotalMinutes >= 15)
            .WithMessage("Janela minima deve ser de 15 minutos.");

        RuleFor(x => x)
            .Must(x => (x.WindowEndUtc - x.WindowStartUtc).TotalMinutes <= 480)
            .WithMessage("Janela maxima permitida e de 8 horas.");

        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.Reason))
            .WithMessage("Motivo deve ter no maximo 500 caracteres.");
    }
}

public class RejectServiceAppointmentRequestValidator : AbstractValidator<RejectServiceAppointmentRequestDto>
{
    public RejectServiceAppointmentRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Motivo da recusa e obrigatorio.")
            .MaximumLength(500)
            .WithMessage("Motivo deve ter no maximo 500 caracteres.");
    }
}

public class RequestServiceAppointmentRescheduleValidator : AbstractValidator<RequestServiceAppointmentRescheduleDto>
{
    public RequestServiceAppointmentRescheduleValidator()
    {
        RuleFor(x => x.ProposedWindowStartUtc)
            .LessThan(x => x.ProposedWindowEndUtc)
            .WithMessage("Janela proposta invalida.");

        RuleFor(x => x)
            .Must(x => (x.ProposedWindowEndUtc - x.ProposedWindowStartUtc).TotalMinutes >= 15)
            .WithMessage("Janela minima deve ser de 15 minutos.");

        RuleFor(x => x)
            .Must(x => (x.ProposedWindowEndUtc - x.ProposedWindowStartUtc).TotalMinutes <= 480)
            .WithMessage("Janela maxima permitida e de 8 horas.");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Motivo do reagendamento e obrigatorio.")
            .MaximumLength(500)
            .WithMessage("Motivo deve ter no maximo 500 caracteres.");
    }
}

public class RespondServiceAppointmentRescheduleRequestValidator : AbstractValidator<RespondServiceAppointmentRescheduleRequestDto>
{
    public RespondServiceAppointmentRescheduleRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .When(x => !x.Accept)
            .WithMessage("Motivo da recusa e obrigatorio.")
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.Reason))
            .WithMessage("Motivo deve ter no maximo 500 caracteres.");
    }
}

public class CancelServiceAppointmentRequestValidator : AbstractValidator<CancelServiceAppointmentRequestDto>
{
    public CancelServiceAppointmentRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Motivo do cancelamento e obrigatorio.")
            .MaximumLength(500)
            .WithMessage("Motivo deve ter no maximo 500 caracteres.");
    }
}
