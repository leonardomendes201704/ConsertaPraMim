using System.Net;
using System.Net.Mail;
using System.Text.Encodings.Web;
using AppMobileCPM.Integrations.Telegram;
using AppMobileCPM.Services;
using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyProviderConnectionService : IJourneyProviderConnectionService
{
    private readonly IJourneyCalendarGateway _calendarGateway;
    private readonly ITelegramBridgeDeliveryClient _telegramBridgeDeliveryClient;
    private readonly IAdminKanbanService _kanbanService;
    private readonly IJourneyServiceClosureService _closureService;
    private readonly JourneyProviderNotificationOptions _options;
    private readonly ILogger<JourneyProviderConnectionService> _logger;

    public JourneyProviderConnectionService(
        IJourneyCalendarGateway calendarGateway,
        ITelegramBridgeDeliveryClient telegramBridgeDeliveryClient,
        IAdminKanbanService kanbanService,
        IJourneyServiceClosureService closureService,
        IOptions<JourneyProviderNotificationOptions> options,
        ILogger<JourneyProviderConnectionService> logger)
    {
        _calendarGateway = calendarGateway;
        _telegramBridgeDeliveryClient = telegramBridgeDeliveryClient;
        _kanbanService = kanbanService;
        _closureService = closureService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<JourneyProviderConnectionResult> ConnectAsync(
        JourneyProviderConnectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var warnings = new List<string>();
        var calendarUpdated = false;
        var clientNotified = false;
        var providerNotified = false;

        if (CanUpdateCalendar(request.Lead))
        {
            var calendarResult = await TryUpdateCalendarAsync(request, cancellationToken);
            calendarUpdated = calendarResult.Success;
            if (!calendarResult.Success && !string.IsNullOrWhiteSpace(calendarResult.ErrorMessage))
            {
                warnings.Add($"Agenda: {calendarResult.ErrorMessage}");
            }
        }

        var clientNotification = await TryNotifyClientAsync(request, cancellationToken);
        clientNotified = clientNotification.Success;
        if (!clientNotification.Success && !string.IsNullOrWhiteSpace(clientNotification.ErrorMessage))
        {
            warnings.Add($"Cliente: {clientNotification.ErrorMessage}");
        }

        var providerNotification = await TryNotifyProviderAsync(request, cancellationToken);
        providerNotified = providerNotification.Success;
        if (!providerNotification.Success && !string.IsNullOrWhiteSpace(providerNotification.ErrorMessage))
        {
            warnings.Add($"Prestador: {providerNotification.ErrorMessage}");
        }

        var closureResult = await _closureService.StartServiceAsync(request.Lead.Id, request.ReservedAtUtc, cancellationToken);
        if (!closureResult.Success && !string.IsNullOrWhiteSpace(closureResult.Message))
        {
            warnings.Add($"Encerramento: {closureResult.Message}");
        }

        var historyDescription = BuildHistoryDescription(calendarUpdated, clientNotified, providerNotified, warnings);
        _ = _kanbanService.AddHistoryEvent(request.Lead.Id, "jornada_conexao_direta_liberada", historyDescription);

        return new JourneyProviderConnectionResult
        {
            Success = warnings.Count == 0,
            CalendarUpdated = calendarUpdated,
            ClientNotified = clientNotified,
            ProviderNotified = providerNotified,
            Message = warnings.Count == 0
                ? "Conexao direta liberada para cliente e prestador."
                : $"Conexao direta liberada com alertas operacionais. {string.Join(" | ", warnings)}"
        };
    }

    private bool CanUpdateCalendar(AdminKanbanLeadDetailsRecord lead)
    {
        return !string.IsNullOrWhiteSpace(lead.Journey.Scheduling.GoogleCalendarEventId) &&
               lead.Journey.Scheduling.ScheduledStartAtUtc.HasValue &&
               lead.Journey.Scheduling.ScheduledEndAtUtc.HasValue;
    }

    private async Task<(bool Success, string ErrorMessage)> TryUpdateCalendarAsync(
        JourneyProviderConnectionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var lead = request.Lead;
            var scheduling = lead.Journey.Scheduling;
            var eventRequest = new JourneyCalendarEventUpsertRequest
            {
                Title = $"ConsertaPraMim - Prestador conectado #{lead.Journey.JourneyPublicId.ToString("N")[..8]}",
                StartsAtUtc = scheduling.ScheduledStartAtUtc!.Value,
                EndsAtUtc = scheduling.ScheduledEndAtUtc!.Value,
                Description = BuildCalendarDescription(request),
                Location = BuildAddressSummary(lead),
                Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["lead_id"] = lead.Id.ToString(),
                    ["journey_id"] = lead.Journey.JourneyId.ToString(),
                    ["journey_public_id"] = lead.Journey.JourneyPublicId.ToString("N"),
                    ["reserved_provider_id"] = request.Target.ProviderId.ToString("D"),
                    ["reserved_provider_name"] = request.Target.ProviderName
                },
                IdempotencyKey = $"cpm-jour-reserva-{lead.Journey.JourneyPublicId:N}"
            };

            var result = await _calendarGateway.UpdateEventAsync(
                scheduling.GoogleCalendarEventId,
                eventRequest,
                cancellationToken);

            if (!result.Success)
            {
                _logger.LogWarning(
                    "Falha ao atualizar evento do Google Calendar apos reserva da jornada. LeadId={LeadId} EventId={EventId} Error={Error}",
                    lead.Id,
                    scheduling.GoogleCalendarEventId,
                    result.ErrorMessage);

                return (false, string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "nao foi possivel atualizar o evento do Google Calendar."
                    : result.ErrorMessage);
            }

            return (true, string.Empty);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Falha inesperada ao atualizar agenda da jornada apos reserva. LeadId={LeadId}.",
                request.Lead.Id);
            return (false, "falha inesperada ao atualizar o evento do Google Calendar.");
        }
    }

    private async Task<(bool Success, string ErrorMessage)> TryNotifyClientAsync(
        JourneyProviderConnectionRequest request,
        CancellationToken cancellationToken)
    {
        var lead = request.Lead;
        var clientMessage = BuildClientMessage(request);

        if (lead.Telegram.TelegramChatId.HasValue && lead.Telegram.TelegramChatId.Value > 0)
        {
            var telegramResult = await _telegramBridgeDeliveryClient.SendHumanReplyAsync(
                new TelegramBridgeHumanReplyRequest
                {
                    LeadId = lead.Id,
                    TelegramChatId = lead.Telegram.TelegramChatId.Value,
                    MessageText = clientMessage,
                    SenderName = "ConsertaPraMim",
                    ActivateHumanHandoff = true,
                    HandoffReasonCode = "provider_reserved",
                    HandoffReasonLabel = "Prestador conectado ao cliente",
                    HandoffSource = "journey_provider_connection",
                    HandoffActivatedAtUtc = request.ReservedAtUtc
                },
                cancellationToken);

            if (telegramResult.Success)
            {
                return (true, string.Empty);
            }

            return (false, string.IsNullOrWhiteSpace(telegramResult.Message)
                ? "falha ao avisar o cliente no Telegram."
                : telegramResult.Message);
        }

        var clientEmail = ResolveClientEmail(lead);
        if (string.IsNullOrWhiteSpace(clientEmail))
        {
            return (false, "cliente sem Telegram ativo e sem e-mail valido para receber a conexao.");
        }

        if (!_options.Enabled || !_options.EmailEnabled)
        {
            return (false, "notificacao por e-mail desabilitada no ambiente atual.");
        }

        var subject = $"Prestador conectado para o seu atendimento em {ResolveClientCity(lead)}";
        var body = BuildClientEmailBody(request);

        return await TrySendEmailAsync(clientEmail, subject, body, lead.Id, request.Target.ProviderId, "cliente", cancellationToken);
    }

    private async Task<(bool Success, string ErrorMessage)> TryNotifyProviderAsync(
        JourneyProviderConnectionRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Target.ProviderEmail))
        {
            return (false, "prestador sem e-mail valido para receber os dados liberados do cliente.");
        }

        if (!_options.Enabled || !_options.EmailEnabled)
        {
            return (false, "notificacao por e-mail desabilitada no ambiente atual.");
        }

        var subject = $"Cliente liberado para contato direto - {ResolveClientCity(request.Lead)}";
        var body = BuildProviderEmailBody(request);
        return await TrySendEmailAsync(
            request.Target.ProviderEmail,
            subject,
            body,
            request.Lead.Id,
            request.Target.ProviderId,
            "prestador",
            cancellationToken);
    }

    private async Task<(bool Success, string ErrorMessage)> TrySendEmailAsync(
        string recipientEmail,
        string subject,
        string htmlBody,
        int leadId,
        Guid providerId,
        string audience,
        CancellationToken cancellationToken)
    {
        try
        {
            if (NormalizeTransport(_options.EmailTransport) == "smtp")
            {
                using var message = new MailMessage
                {
                    From = new MailAddress(_options.SenderEmail.Trim(), _options.SenderDisplayName.Trim()),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };
                message.To.Add(recipientEmail.Trim());

                using var client = new SmtpClient(_options.SmtpHost.Trim(), _options.SmtpPort)
                {
                    EnableSsl = _options.SmtpUseSsl,
                    Credentials = new NetworkCredential(_options.SmtpUsername.Trim(), _options.SmtpPassword)
                };

                cancellationToken.ThrowIfCancellationRequested();
                await client.SendMailAsync(message);
            }
            else
            {
                _logger.LogInformation(
                    "JOURNEY CONNECTION EMAIL [LOG] Audience={Audience} To={To} LeadId={LeadId} ProviderId={ProviderId} Subject={Subject} Body={Body}",
                    audience,
                    recipientEmail,
                    leadId,
                    providerId,
                    subject,
                    htmlBody);
            }

            return (true, string.Empty);
        }
        catch (SmtpException ex)
        {
            _logger.LogError(
                ex,
                "Falha SMTP ao notificar {Audience} da conexao da jornada. LeadId={LeadId} ProviderId={ProviderId}.",
                audience,
                leadId,
                providerId);
            return (false, $"falha SMTP ao notificar o {audience}: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falha ao notificar {Audience} da conexao da jornada. LeadId={LeadId} ProviderId={ProviderId}.",
                audience,
                leadId,
                providerId);
            return (false, $"falha ao notificar o {audience}: {ex.Message}");
        }
    }

    private string BuildCalendarDescription(JourneyProviderConnectionRequest request)
    {
        var lead = request.Lead;
        var parts = new List<string>
        {
            $"Lead: {lead.Id}",
            $"Journey: {lead.Journey.JourneyPublicId:N}",
            $"Canal: {AdminKanbanJourneySourceChannels.GetLabel(lead.Journey.SourceChannel)}",
            $"Endereco: {BuildAddressSummary(lead)}",
            $"Prestador reservado: {request.Target.ProviderName}"
        };

        var clientPhone = ResolveClientPhone(lead);
        var clientEmail = ResolveClientEmail(lead);
        if (!string.IsNullOrWhiteSpace(clientPhone))
        {
            parts.Add($"Telefone cliente: {clientPhone}");
        }

        if (!string.IsNullOrWhiteSpace(clientEmail))
        {
            parts.Add($"E-mail cliente: {clientEmail}");
        }

        if (!string.IsNullOrWhiteSpace(request.Target.ProviderPhone))
        {
            parts.Add($"Telefone prestador: {request.Target.ProviderPhone}");
        }

        if (!string.IsNullOrWhiteSpace(request.Target.ProviderEmail))
        {
            parts.Add($"E-mail prestador: {request.Target.ProviderEmail}");
        }

        if (!string.IsNullOrWhiteSpace(lead.Journey.Qualification.NormalizedServiceCategoryName))
        {
            parts.Add($"Categoria: {lead.Journey.Qualification.NormalizedServiceCategoryName}");
        }

        if (!string.IsNullOrWhiteSpace(lead.Journey.Qualification.ProblemContext))
        {
            parts.Add($"Contexto: {lead.Journey.Qualification.ProblemContext}");
        }

        return string.Join(Environment.NewLine, parts);
    }

    private string BuildClientMessage(JourneyProviderConnectionRequest request)
    {
        var providerPhone = string.IsNullOrWhiteSpace(request.Target.ProviderPhone) ? "nao informado" : request.Target.ProviderPhone;
        var providerEmail = string.IsNullOrWhiteSpace(request.Target.ProviderEmail) ? "nao informado" : request.Target.ProviderEmail;

        return $"""
Seu atendimento ja foi reservado por {request.Target.ProviderName}.

Janela confirmada: {BuildSchedulingWindowLabel(request.Lead)}
Categoria: {ResolveCategory(request.Lead)}
Contato do prestador:
- Telefone / WhatsApp: {providerPhone}
- E-mail: {providerEmail}

Voce pode falar diretamente com o prestador para alinhar os detalhes finais do servico.
""";
    }

    private string BuildClientEmailBody(JourneyProviderConnectionRequest request)
    {
        var encoder = HtmlEncoder.Default;
        var lead = request.Lead;
        return $"""
<!DOCTYPE html>
<html lang="pt-BR">
<head><meta charset="utf-8"><title>Prestador conectado</title></head>
<body style="margin:0;padding:24px;background:#f4f7fb;font-family:Segoe UI,Arial,sans-serif;color:#17202a;">
  <article style="max-width:680px;margin:0 auto;background:#ffffff;border-radius:18px;padding:32px;border:1px solid #d9e2ec;">
    <p style="margin:0 0 12px;font-size:14px;letter-spacing:.08em;text-transform:uppercase;color:#5b7083;">ConsertaPraMim</p>
    <h1 style="margin:0 0 16px;font-size:28px;line-height:1.2;">Prestador conectado ao seu atendimento</h1>
    <p style="margin:0 0 20px;font-size:16px;line-height:1.6;">Seu atendimento foi reservado com sucesso. Agora voce ja pode falar diretamente com o prestador.</p>
    <section style="padding:20px;border-radius:16px;background:#f7fafc;border:1px solid #e2e8f0;">
      <p style="margin:0 0 8px;"><strong>Prestador:</strong> {encoder.Encode(request.Target.ProviderName)}</p>
      <p style="margin:0 0 8px;"><strong>Telefone / WhatsApp:</strong> {encoder.Encode(string.IsNullOrWhiteSpace(request.Target.ProviderPhone) ? "-" : request.Target.ProviderPhone)}</p>
      <p style="margin:0 0 8px;"><strong>E-mail:</strong> {encoder.Encode(string.IsNullOrWhiteSpace(request.Target.ProviderEmail) ? "-" : request.Target.ProviderEmail)}</p>
      <p style="margin:0 0 8px;"><strong>Janela confirmada:</strong> {encoder.Encode(BuildSchedulingWindowLabel(lead))}</p>
      <p style="margin:0;"><strong>Endereco:</strong> {encoder.Encode(BuildAddressSummary(lead))}</p>
    </section>
  </article>
</body>
</html>
""";
    }

    private string BuildProviderEmailBody(JourneyProviderConnectionRequest request)
    {
        var encoder = HtmlEncoder.Default;
        var lead = request.Lead;
        return $"""
<!DOCTYPE html>
<html lang="pt-BR">
<head><meta charset="utf-8"><title>Cliente liberado para contato</title></head>
<body style="margin:0;padding:24px;background:#f4f7fb;font-family:Segoe UI,Arial,sans-serif;color:#17202a;">
  <article style="max-width:680px;margin:0 auto;background:#ffffff;border-radius:18px;padding:32px;border:1px solid #d9e2ec;">
    <p style="margin:0 0 12px;font-size:14px;letter-spacing:.08em;text-transform:uppercase;color:#5b7083;">ConsertaPraMim</p>
    <h1 style="margin:0 0 16px;font-size:28px;line-height:1.2;">Cliente liberado para contato direto</h1>
    <p style="margin:0 0 20px;font-size:16px;line-height:1.6;">Voce confirmou o aceite vencedor da oportunidade. Abaixo estao os dados liberados do cliente para o contato direto.</p>
    <section style="padding:20px;border-radius:16px;background:#f7fafc;border:1px solid #e2e8f0;">
      <p style="margin:0 0 8px;"><strong>Cliente:</strong> {encoder.Encode(lead.Name)}</p>
      <p style="margin:0 0 8px;"><strong>Telefone / WhatsApp:</strong> {encoder.Encode(string.IsNullOrWhiteSpace(ResolveClientPhone(lead)) ? "-" : ResolveClientPhone(lead))}</p>
      <p style="margin:0 0 8px;"><strong>E-mail:</strong> {encoder.Encode(string.IsNullOrWhiteSpace(ResolveClientEmail(lead)) ? "-" : ResolveClientEmail(lead))}</p>
      <p style="margin:0 0 8px;"><strong>Endereco:</strong> {encoder.Encode(BuildAddressSummary(lead))}</p>
      <p style="margin:0 0 8px;"><strong>Janela confirmada:</strong> {encoder.Encode(BuildSchedulingWindowLabel(lead))}</p>
      <p style="margin:0;"><strong>Resumo:</strong> {encoder.Encode(ResolveQualificationSummary(lead))}</p>
    </section>
  </article>
</body>
</html>
""";
    }

    private static string BuildHistoryDescription(bool calendarUpdated, bool clientNotified, bool providerNotified, IReadOnlyList<string> warnings)
    {
        var parts = new List<string>
        {
            $"Agenda: {(calendarUpdated ? "atualizada" : "sem alteracao")}",
            $"Cliente: {(clientNotified ? "avisado" : "nao avisado")}",
            $"Prestador: {(providerNotified ? "avisado" : "nao avisado")}"
        };

        if (warnings.Count > 0)
        {
            parts.Add($"Alertas: {string.Join(" | ", warnings)}");
        }

        return string.Join(" | ", parts);
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

    private static string ResolveClientCity(AdminKanbanLeadDetailsRecord lead)
    {
        if (!string.IsNullOrWhiteSpace(lead.City))
        {
            return lead.City;
        }

        return string.IsNullOrWhiteSpace(lead.Journey.Qualification.City)
            ? "sua regiao"
            : lead.Journey.Qualification.City;
    }

    private static string ResolveCategory(AdminKanbanLeadDetailsRecord lead)
    {
        if (!string.IsNullOrWhiteSpace(lead.Journey.Matching.RequestedCategory))
        {
            return lead.Journey.Matching.RequestedCategory;
        }

        if (!string.IsNullOrWhiteSpace(lead.Journey.Qualification.NormalizedServiceCategoryName))
        {
            return lead.Journey.Qualification.NormalizedServiceCategoryName;
        }

        return string.IsNullOrWhiteSpace(lead.ServiceCategory) ? "Servico solicitado" : lead.ServiceCategory;
    }

    private static string ResolveQualificationSummary(AdminKanbanLeadDetailsRecord lead)
    {
        if (!string.IsNullOrWhiteSpace(lead.Journey.Qualification.ProblemContext))
        {
            return lead.Journey.Qualification.ProblemContext;
        }

        return string.IsNullOrWhiteSpace(lead.StatusNote)
            ? "Resumo do caso ainda sem detalhe adicional."
            : lead.StatusNote;
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
            ? "Endereco validado na jornada e liberado para o prestador reservado."
            : string.Join(", ", parts);
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

    private static string NormalizeTransport(string? transport) => (transport ?? string.Empty).Trim().ToLowerInvariant();

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
