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

public class MarkServiceAppointmentArrivalRequestValidator : AbstractValidator<MarkServiceAppointmentArrivalRequestDto>
{
    public MarkServiceAppointmentArrivalRequestValidator()
    {
        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90)
            .When(x => x.Latitude.HasValue)
            .WithMessage("Latitude invalida.");

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180)
            .When(x => x.Longitude.HasValue)
            .WithMessage("Longitude invalida.");

        RuleFor(x => x.AccuracyMeters)
            .GreaterThanOrEqualTo(0)
            .When(x => x.AccuracyMeters.HasValue)
            .WithMessage("Precisao do GPS invalida.");

        RuleFor(x => x)
            .Must(x =>
            {
                var hasLatitude = x.Latitude.HasValue;
                var hasLongitude = x.Longitude.HasValue;
                return hasLatitude == hasLongitude;
            })
            .WithMessage("Latitude e longitude devem ser informadas juntas.");

        RuleFor(x => x)
            .Must(x =>
            {
                var hasCoordinates = x.Latitude.HasValue && x.Longitude.HasValue;
                return hasCoordinates || !string.IsNullOrWhiteSpace(x.ManualReason);
            })
            .WithMessage("Informe o motivo do check-in manual quando o GPS nao estiver disponivel.");

        RuleFor(x => x.ManualReason)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.ManualReason))
            .WithMessage("Motivo deve ter no maximo 500 caracteres.");
    }
}

public class StartServiceAppointmentExecutionRequestValidator : AbstractValidator<StartServiceAppointmentExecutionRequestDto>
{
    public StartServiceAppointmentExecutionRequestValidator()
    {
        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.Reason))
            .WithMessage("Observacao deve ter no maximo 500 caracteres.");
    }
}

public class UpdateServiceAppointmentOperationalStatusRequestValidator : AbstractValidator<UpdateServiceAppointmentOperationalStatusRequestDto>
{
    public UpdateServiceAppointmentOperationalStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty()
            .WithMessage("Status operacional e obrigatorio.")
            .MaximumLength(50)
            .WithMessage("Status operacional invalido.");

        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.Reason))
            .WithMessage("Motivo deve ter no maximo 500 caracteres.");
    }
}

public class CreateServiceScopeChangeRequestValidator : AbstractValidator<CreateServiceScopeChangeRequestDto>
{
    public CreateServiceScopeChangeRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Motivo do aditivo e obrigatorio.")
            .MaximumLength(500)
            .WithMessage("Motivo deve ter no maximo 500 caracteres.");

        RuleFor(x => x.AdditionalScopeDescription)
            .NotEmpty()
            .WithMessage("Descricao do escopo adicional e obrigatoria.")
            .MaximumLength(3000)
            .WithMessage("Descricao do escopo adicional deve ter no maximo 3000 caracteres.");

        RuleFor(x => x.IncrementalValue)
            .GreaterThan(0m)
            .WithMessage("Valor incremental deve ser maior que zero.")
            .LessThanOrEqualTo(1000000m)
            .WithMessage("Valor incremental excede o limite permitido.");
    }
}

public class CreateServiceWarrantyClaimRequestValidator : AbstractValidator<CreateServiceWarrantyClaimRequestDto>
{
    public CreateServiceWarrantyClaimRequestValidator()
    {
        RuleFor(x => x.IssueDescription)
            .NotEmpty()
            .WithMessage("Descricao do problema e obrigatoria.")
            .MaximumLength(3000)
            .WithMessage("Descricao do problema deve ter no maximo 3000 caracteres.");
    }
}

public class RejectServiceScopeChangeRequestValidator : AbstractValidator<RejectServiceScopeChangeRequestDto>
{
    public RejectServiceScopeChangeRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Motivo da rejeicao e obrigatorio.")
            .MaximumLength(500)
            .WithMessage("Motivo deve ter no maximo 500 caracteres.");
    }
}
