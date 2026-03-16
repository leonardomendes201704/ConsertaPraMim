using AppMobileCPM.Services;
using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyProviderOpportunityService : IJourneyProviderOpportunityService
{
    private readonly IAdminKanbanService _kanbanService;
    private readonly IJourneyProviderDispatchLinkService _linkService;
    private readonly IJourneyProviderDispatchService _dispatchService;
    private readonly IJourneyProviderConnectionService _connectionService;
    private readonly JourneyProviderNotificationOptions _options;

    public JourneyProviderOpportunityService(
        IAdminKanbanService kanbanService,
        IJourneyProviderDispatchLinkService linkService,
        IJourneyProviderDispatchService dispatchService,
        IJourneyProviderConnectionService connectionService,
        IOptions<JourneyProviderNotificationOptions> options)
    {
        _kanbanService = kanbanService;
        _linkService = linkService;
        _dispatchService = dispatchService;
        _connectionService = connectionService;
        _options = options.Value;
    }

    public JourneyProviderOpportunityContext GetOpportunityContext(string token, string action, DateTime nowUtc)
    {
        var normalizedAction = JourneyProviderOpportunityActions.Normalize(action);
        var validation = _linkService.ValidateToken(token, JourneyProviderDispatchLinkPurposes.ResponsePage, nowUtc);
        if (!validation.Success)
        {
            return new JourneyProviderOpportunityContext
            {
                Success = false,
                TokenExpired = validation.Expired,
                Message = validation.Message,
                NormalizedAction = normalizedAction,
                ResponseHeadline = validation.Expired ? "Link expirado" : "Link invalido",
                ResponseDescription = validation.Message
            };
        }

        _ = _kanbanService.ApplyJourneyDispatchTargetInteraction(new AdminKanbanJourneyDispatchTargetInteractionRequest
        {
            LeadId = validation.Payload.LeadId,
            ProviderId = validation.Payload.ProviderId,
            TargetKey = validation.Payload.TargetKey,
            InteractionType = AdminKanbanJourneyDispatchInteractionTypes.Clicked,
            OccurredAtUtc = nowUtc,
            SourceChannel = "email_signed_link"
        });

        return BuildContext(validation.Payload, normalizedAction);
    }

    public async Task<JourneyProviderOpportunityActionResult> ConfirmActionAsync(
        string token,
        string action,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var normalizedAction = JourneyProviderOpportunityActions.Normalize(action);
        var validation = _linkService.ValidateToken(token, JourneyProviderDispatchLinkPurposes.ResponsePage, nowUtc);
        if (!validation.Success)
        {
            return BuildInvalidResult(validation, normalizedAction);
        }

        _ = _kanbanService.ApplyJourneyDispatchTargetInteraction(new AdminKanbanJourneyDispatchTargetInteractionRequest
        {
            LeadId = validation.Payload.LeadId,
            ProviderId = validation.Payload.ProviderId,
            TargetKey = validation.Payload.TargetKey,
            InteractionType = AdminKanbanJourneyDispatchInteractionTypes.Clicked,
            OccurredAtUtc = nowUtc,
            SourceChannel = "email_signed_link"
        });

        if (normalizedAction == JourneyProviderOpportunityActions.Decline)
        {
            var declineResult = _kanbanService.ApplyJourneyDispatchTargetInteraction(new AdminKanbanJourneyDispatchTargetInteractionRequest
            {
                LeadId = validation.Payload.LeadId,
                ProviderId = validation.Payload.ProviderId,
                TargetKey = validation.Payload.TargetKey,
                InteractionType = AdminKanbanJourneyDispatchInteractionTypes.Declined,
                OccurredAtUtc = nowUtc,
                SourceChannel = "email_signed_link"
            });

            if (declineResult?.Succeeded == true)
            {
                _ = _dispatchService.RunOnceAsync(nowUtc);
            }

            var context = BuildContext(validation.Payload, normalizedAction);
            return new JourneyProviderOpportunityActionResult
            {
                Success = declineResult?.Succeeded == true,
                AlreadyReserved = declineResult?.AlreadyReserved == true,
                AlreadyResponded = declineResult?.AlreadyResponded == true,
                TargetUnavailable = declineResult is { Succeeded: false },
                Action = normalizedAction,
                Message = declineResult?.Message ?? "Nao foi possivel registrar a recusa da oportunidade.",
                Context = context with
                {
                    ResponseHeadline = declineResult?.Succeeded == true ? "Recusa registrada" : context.ResponseHeadline,
                    ResponseDescription = declineResult?.Succeeded == true
                        ? "A recusa foi registrada com sucesso. O sistema ja pode seguir para a proxima onda elegivel."
                        : context.ResponseDescription
                }
            };
        }

        var reservationResult = _kanbanService.TryReserveJourneyDispatchTarget(new AdminKanbanJourneyDispatchReservationRequest
        {
            LeadId = validation.Payload.LeadId,
            ProviderId = validation.Payload.ProviderId,
            TargetKey = validation.Payload.TargetKey,
            ReservedAtUtc = nowUtc,
            SourceChannel = "email_signed_link",
            MetadataJson = $$"""
{"action":"aceitar","source":"email_signed_link","providerId":"{{validation.Payload.ProviderId}}","targetKey":"{{validation.Payload.TargetKey}}"}
"""
        });

        var acceptedContext = BuildContext(validation.Payload, normalizedAction);
        var finalLead = reservationResult?.Succeeded == true
            ? _kanbanService.GetLeadDetails(validation.Payload.LeadId)
            : null;
        JourneyProviderConnectionResult? connectionResult = null;
        if (reservationResult?.Succeeded == true && finalLead is not null)
        {
            var reservedTarget = finalLead.Journey.Dispatch.Targets.FirstOrDefault(item =>
                item.ProviderId == validation.Payload.ProviderId &&
                string.Equals(item.TargetKey, validation.Payload.TargetKey, StringComparison.Ordinal));
            if (reservedTarget is not null)
            {
                connectionResult = await _connectionService.ConnectAsync(
                    new JourneyProviderConnectionRequest
                    {
                        Lead = finalLead,
                        Target = reservedTarget,
                        ReservedAtUtc = nowUtc
                    },
                    cancellationToken);
            }
        }

        var finalContext = reservationResult?.Succeeded == true
            ? BuildContext(validation.Payload, normalizedAction)
            : acceptedContext;

        return new JourneyProviderOpportunityActionResult
        {
            Success = reservationResult?.Succeeded == true,
            AlreadyReserved = reservationResult?.AlreadyReserved == true,
            AlreadyResponded = reservationResult is { Succeeded: false, AlreadyReserved: false },
            TargetUnavailable = reservationResult is null,
            Action = normalizedAction,
            Message = reservationResult?.Succeeded == true
                ? BuildAcceptanceMessage(connectionResult)
                : reservationResult?.AlreadyReserved == true
                    ? "A oportunidade ja foi reservada por outro prestador."
                    : "Nao foi possivel confirmar o aceite desta oportunidade.",
            Context = finalContext with
            {
                ResponseHeadline = reservationResult?.Succeeded == true ? "Aceite confirmado" : finalContext.ResponseHeadline,
                ResponseDescription = reservationResult?.Succeeded == true
                    ? BuildAcceptanceDescription(connectionResult)
                    : reservationResult?.AlreadyReserved == true
                        ? "Outro prestador confirmou primeiro. Esta oportunidade nao esta mais disponivel."
                        : finalContext.ResponseDescription
            }
        };
    }

    public bool TrackOpen(string token, DateTime nowUtc)
    {
        var validation = _linkService.ValidateToken(token, JourneyProviderDispatchLinkPurposes.OpenTracking, nowUtc);
        if (!validation.Success)
        {
            return false;
        }

        var result = _kanbanService.ApplyJourneyDispatchTargetInteraction(new AdminKanbanJourneyDispatchTargetInteractionRequest
        {
            LeadId = validation.Payload.LeadId,
            ProviderId = validation.Payload.ProviderId,
            TargetKey = validation.Payload.TargetKey,
            InteractionType = AdminKanbanJourneyDispatchInteractionTypes.Opened,
            OccurredAtUtc = nowUtc,
            SourceChannel = "email_pixel"
        });

        return result?.Succeeded == true;
    }

    private JourneyProviderOpportunityActionResult BuildInvalidResult(
        JourneyProviderDispatchTokenValidationResult validation,
        string normalizedAction)
    {
        var context = new JourneyProviderOpportunityContext
        {
            Success = false,
            TokenExpired = validation.Expired,
            Message = validation.Message,
            NormalizedAction = normalizedAction,
            ResponseHeadline = validation.Expired ? "Link expirado" : "Link invalido",
            ResponseDescription = validation.Message
        };

        return new JourneyProviderOpportunityActionResult
        {
            Success = false,
            TokenExpired = validation.Expired,
            Action = normalizedAction,
            Message = validation.Message,
            Context = context
        };
    }

    private JourneyProviderOpportunityContext BuildContext(JourneyProviderDispatchSignedTokenPayload payload, string normalizedAction)
    {
        var lead = _kanbanService.GetLeadDetails(payload.LeadId);
        if (lead is null)
        {
            return new JourneyProviderOpportunityContext
            {
                Success = false,
                NotFound = true,
                Message = "A oportunidade nao foi localizada.",
                NormalizedAction = normalizedAction,
                ResponseHeadline = "Oportunidade indisponivel",
                ResponseDescription = "Nao foi possivel localizar os dados desta oportunidade."
            };
        }

        var target = lead.Journey.Dispatch.Targets.FirstOrDefault(item =>
            string.Equals(item.TargetKey, payload.TargetKey, StringComparison.Ordinal) &&
            item.ProviderId == payload.ProviderId);
        if (target is null)
        {
            return new JourneyProviderOpportunityContext
            {
                Success = false,
                NotFound = true,
                Message = "O alvo da oportunidade nao esta mais disponivel.",
                NormalizedAction = normalizedAction,
                LeadName = lead.Name,
                ResponseHeadline = "Oportunidade indisponivel",
                ResponseDescription = "O registro desta oportunidade nao esta mais ativo para resposta."
            };
        }

        var alreadyReserved = lead.Journey.Dispatch.ReservedProviderId.HasValue &&
            lead.Journey.Dispatch.ReservedProviderId.Value != payload.ProviderId;
        var alreadyResponded = string.Equals(target.Status, AdminKanbanJourneyDispatchTargetStatuses.Accepted, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(target.Status, AdminKanbanJourneyDispatchTargetStatuses.Declined, StringComparison.OrdinalIgnoreCase);
        var canRespond = !alreadyReserved &&
            !alreadyResponded &&
            !string.Equals(target.Status, AdminKanbanJourneyDispatchTargetStatuses.Expired, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(target.Status, AdminKanbanJourneyDispatchTargetStatuses.Dispensed, StringComparison.OrdinalIgnoreCase);

        return new JourneyProviderOpportunityContext
        {
            Success = true,
            NormalizedAction = normalizedAction,
            ResponseToken = _linkService.GenerateToken(
                JourneyProviderDispatchLinkPurposes.ResponsePage,
                lead.Id,
                lead.Journey.JourneyId,
                target.ProviderId,
                target.TargetKey,
                ResolveTokenExpiration()),
            LeadName = lead.Name,
            ProviderName = target.ProviderName,
            RequestedCategory = string.IsNullOrWhiteSpace(lead.Journey.Matching.RequestedCategory) ? lead.ServiceCategory : lead.Journey.Matching.RequestedCategory,
            RequestedSubcategory = lead.Journey.Matching.RequestedSubcategory,
            QualificationSummary = string.IsNullOrWhiteSpace(lead.Journey.Qualification.ProblemContext) ? lead.StatusNote : lead.Journey.Qualification.ProblemContext,
            AddressSummary = BuildAddressSummary(lead),
            ScheduledWindowLabel = BuildSchedulingWindowLabel(lead),
            DispatchStatusLabel = string.IsNullOrWhiteSpace(lead.Journey.Dispatch.Status) ? "-" : AdminKanbanJourneyDispatchStatuses.GetLabel(lead.Journey.Dispatch.Status),
            TargetStatusLabel = string.IsNullOrWhiteSpace(target.Status) ? "-" : AdminKanbanJourneyDispatchTargetStatuses.GetLabel(target.Status),
            PortalUrl = _options.ProviderPortalBaseUrl,
            CanRespond = canRespond,
            AlreadyResponded = alreadyResponded,
            AlreadyReserved = alreadyReserved,
            ClientContactReleased = lead.Journey.Dispatch.ReservedProviderId.HasValue && lead.Journey.Dispatch.ReservedProviderId.Value == payload.ProviderId,
            ClientDisplayName = lead.Name,
            ClientPhone = lead.Journey.Dispatch.ReservedProviderId.HasValue && lead.Journey.Dispatch.ReservedProviderId.Value == payload.ProviderId
                ? ResolveClientPhone(lead)
                : string.Empty,
            ClientEmail = lead.Journey.Dispatch.ReservedProviderId.HasValue && lead.Journey.Dispatch.ReservedProviderId.Value == payload.ProviderId
                ? ResolveClientEmail(lead)
                : string.Empty,
            ReservedProviderPhone = lead.Journey.Dispatch.ReservedProviderId.HasValue && lead.Journey.Dispatch.ReservedProviderId.Value == payload.ProviderId
                ? lead.Journey.Dispatch.ReservedProviderPhone
                : string.Empty,
            ReservedProviderEmail = lead.Journey.Dispatch.ReservedProviderId.HasValue && lead.Journey.Dispatch.ReservedProviderId.Value == payload.ProviderId
                ? lead.Journey.Dispatch.ReservedProviderEmail
                : string.Empty,
            ResponseHeadline = canRespond
                ? JourneyProviderOpportunityActions.GetLabel(normalizedAction)
                : alreadyReserved
                    ? "Oportunidade ja reservada"
                    : "Oportunidade indisponivel",
            ResponseDescription = canRespond
                ? "Confirme abaixo sua decisao oficial sobre esta oportunidade. O sistema so considera valida a confirmacao feita nesta pagina."
                : alreadyReserved
                    ? "Outro prestador confirmou primeiro esta oportunidade."
                    : "Esta oportunidade nao aceita mais respostas."
        };
    }

    private static string BuildAcceptanceMessage(JourneyProviderConnectionResult? connectionResult)
    {
        if (connectionResult is null)
        {
            return "O aceite foi confirmado com sucesso.";
        }

        return string.IsNullOrWhiteSpace(connectionResult.Message)
            ? "O aceite foi confirmado com sucesso."
            : $"O aceite foi confirmado com sucesso. {connectionResult.Message}";
    }

    private static string BuildAcceptanceDescription(JourneyProviderConnectionResult? connectionResult)
    {
        if (connectionResult is null)
        {
            return "Sua reserva foi registrada. A conexao operacional com o cliente foi liberada.";
        }

        return connectionResult.Success
            ? "Sua reserva foi registrada e os dados do cliente foram liberados para contato direto."
            : "Sua reserva foi registrada. A conexao com o cliente foi liberada, mas houve alertas operacionais complementares.";
    }

    private DateTime ResolveTokenExpiration()
    {
        return DateTime.UtcNow.AddMinutes(_options.LinkExpirationMinutes);
    }

    private static string BuildAddressSummary(AdminKanbanLeadDetailsRecord lead)
    {
        var parts = new[]
        {
            lead.Journey.Qualification.Street,
            lead.Journey.Qualification.Neighborhood,
            lead.Journey.Qualification.City,
            lead.Journey.Qualification.State,
            lead.Journey.Qualification.PostalCode
        }.Where(part => !string.IsNullOrWhiteSpace(part)).ToList();

        return parts.Count == 0
            ? "Endereco validado na jornada e liberado conforme a etapa operacional."
            : string.Join(", ", parts);
    }

    private static string ResolveClientPhone(AdminKanbanLeadDetailsRecord lead)
    {
        if (!string.IsNullOrWhiteSpace(lead.Journey.PrimaryPhone))
        {
            return lead.Journey.PrimaryPhone;
        }

        if (!string.IsNullOrWhiteSpace(lead.Phone))
        {
            return lead.Phone;
        }

        return lead.Telegram.ClientPhone ?? string.Empty;
    }

    private static string ResolveClientEmail(AdminKanbanLeadDetailsRecord lead)
    {
        if (!string.IsNullOrWhiteSpace(lead.Journey.PrimaryEmail))
        {
            return lead.Journey.PrimaryEmail;
        }

        if (!string.IsNullOrWhiteSpace(lead.Email))
        {
            return lead.Email;
        }

        return lead.Telegram.ClientEmail ?? string.Empty;
    }

    private static string BuildSchedulingWindowLabel(AdminKanbanLeadDetailsRecord lead)
    {
        var start = NormalizeUtc(lead.Journey.Scheduling.ScheduledStartAtUtc);
        var end = NormalizeUtc(lead.Journey.Scheduling.ScheduledEndAtUtc);
        if (!start.HasValue || !end.HasValue)
        {
            return "Janela em confirmacao operacional";
        }

        var timezone = ResolveBusinessTimeZone();
        var localStart = TimeZoneInfo.ConvertTimeFromUtc(start.Value, timezone);
        var localEnd = TimeZoneInfo.ConvertTimeFromUtc(end.Value, timezone);
        return $"{localStart:dd/MM/yyyy HH:mm} - {localEnd:HH:mm} (America/Sao_Paulo)";
    }

    private static DateTime? NormalizeUtc(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }

    private static TimeZoneInfo ResolveBusinessTimeZone()
    {
        foreach (var timeZoneId in new[] { "America/Sao_Paulo", "E. South America Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }
}
