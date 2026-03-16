using System.Net;
using System.Net.Mail;
using System.Text.Encodings.Web;
using AppMobileCPM.Services;
using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyProviderDispatchNotificationService : IJourneyProviderDispatchNotificationService
{
    private readonly JourneyProviderNotificationOptions _options;
    private readonly IJourneyProviderDispatchLinkService _linkService;
    private readonly ILogger<JourneyProviderDispatchNotificationService> _logger;

    public JourneyProviderDispatchNotificationService(
        IOptions<JourneyProviderNotificationOptions> options,
        IJourneyProviderDispatchLinkService linkService,
        ILogger<JourneyProviderDispatchNotificationService> logger)
    {
        _options = options.Value;
        _linkService = linkService;
        _logger = logger;
    }

    public async Task<JourneyProviderDispatchNotificationResult> SendOpportunityAsync(
        JourneyProviderDispatchNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_options.Enabled || !_options.EmailEnabled)
        {
            return new JourneyProviderDispatchNotificationResult
            {
                Success = false,
                PermanentFailure = true,
                DeliveryChannel = "email",
                DeliveryStatus = AdminKanbanJourneyDispatchDeliveryStatuses.Failed,
                Message = "Notificacao de oportunidade desabilitada neste ambiente."
            };
        }

        if (string.IsNullOrWhiteSpace(request.Target.ProviderEmail))
        {
            return new JourneyProviderDispatchNotificationResult
            {
                Success = false,
                PermanentFailure = true,
                DeliveryChannel = "email",
                DeliveryStatus = AdminKanbanJourneyDispatchDeliveryStatuses.Failed,
                Message = "Prestador sem e-mail valido para notificacao da oportunidade."
            };
        }

        var expiresAtUtc = ResolveTokenExpiration(request);
        var responseToken = _linkService.GenerateToken(
            JourneyProviderDispatchLinkPurposes.ResponsePage,
            request.Lead.Id,
            request.Lead.Journey.JourneyId,
            request.Target.ProviderId,
            request.Target.TargetKey,
            expiresAtUtc);
        var openToken = _linkService.GenerateToken(
            JourneyProviderDispatchLinkPurposes.OpenTracking,
            request.Lead.Id,
            request.Lead.Journey.JourneyId,
            request.Target.ProviderId,
            request.Target.TargetKey,
            expiresAtUtc);
        var acceptUrl = _linkService.BuildResponsePageUrl(responseToken, JourneyProviderOpportunityActions.Accept);
        var declineUrl = _linkService.BuildResponsePageUrl(responseToken, JourneyProviderOpportunityActions.Decline);
        var openTrackingUrl = _options.OpenTrackingEnabled
            ? _linkService.BuildOpenTrackingUrl(openToken)
            : null;

        var subject = BuildSubject(request);
        var htmlBody = BuildHtmlBody(request, acceptUrl, declineUrl, openTrackingUrl);

        try
        {
            if (NormalizeTransport(_options.EmailTransport) == "smtp")
            {
                await SendViaSmtpAsync(request.Target.ProviderEmail, subject, htmlBody, cancellationToken);
            }
            else
            {
                _logger.LogInformation(
                    "JOURNEY PROVIDER EMAIL [LOG] To={To} Subject={Subject} Body={Body}",
                    request.Target.ProviderEmail,
                    subject,
                    htmlBody);
            }

            return new JourneyProviderDispatchNotificationResult
            {
                Success = true,
                DeliveryChannel = "email",
                DeliveryStatus = AdminKanbanJourneyDispatchDeliveryStatuses.Sent,
                Message = "Oportunidade enviada por e-mail com links assinados."
            };
        }
        catch (SmtpException ex)
        {
            _logger.LogError(
                ex,
                "Falha SMTP ao notificar prestador da jornada. LeadId={LeadId} ProviderId={ProviderId}.",
                request.Lead.Id,
                request.Target.ProviderId);

            return new JourneyProviderDispatchNotificationResult
            {
                Success = false,
                PermanentFailure = false,
                DeliveryChannel = "email",
                DeliveryStatus = AdminKanbanJourneyDispatchDeliveryStatuses.Failed,
                Message = $"Falha SMTP ao enviar oportunidade: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falha ao enviar notificacao da oportunidade. LeadId={LeadId} ProviderId={ProviderId}.",
                request.Lead.Id,
                request.Target.ProviderId);

            return new JourneyProviderDispatchNotificationResult
            {
                Success = false,
                PermanentFailure = false,
                DeliveryChannel = "email",
                DeliveryStatus = AdminKanbanJourneyDispatchDeliveryStatuses.Failed,
                Message = $"Falha ao enviar oportunidade: {ex.Message}"
            };
        }
    }

    private async Task SendViaSmtpAsync(string recipientEmail, string subject, string htmlBody, CancellationToken cancellationToken)
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

    private DateTime ResolveTokenExpiration(JourneyProviderDispatchNotificationRequest request)
    {
        var normalizedNowUtc = NormalizeUtc(request.NowUtc) ?? DateTime.UtcNow;
        return normalizedNowUtc.AddMinutes(_options.LinkExpirationMinutes);
    }

    private string BuildSubject(JourneyProviderDispatchNotificationRequest request)
    {
        var city = string.IsNullOrWhiteSpace(request.Lead.City)
            ? request.Lead.Journey.Qualification.City
            : request.Lead.City;
        var category = string.IsNullOrWhiteSpace(request.Lead.Journey.Matching.RequestedCategory)
            ? request.Lead.ServiceCategory
            : request.Lead.Journey.Matching.RequestedCategory;

        return string.IsNullOrWhiteSpace(city)
            ? $"Nova oportunidade de {category}"
            : $"Nova oportunidade de {category} em {city}";
    }

    private string BuildHtmlBody(
        JourneyProviderDispatchNotificationRequest request,
        Uri acceptUrl,
        Uri declineUrl,
        Uri? openTrackingUrl)
    {
        var encoder = HtmlEncoder.Default;
        var qualification = request.Lead.Journey.Qualification;
        var scheduling = request.Lead.Journey.Scheduling;
        var category = string.IsNullOrWhiteSpace(request.Lead.Journey.Matching.RequestedCategory)
            ? request.Lead.ServiceCategory
            : request.Lead.Journey.Matching.RequestedCategory;
        var address = BuildAddressSummary(qualification);
        var window = BuildSchedulingWindowLabel(scheduling);
        var summary = string.IsNullOrWhiteSpace(qualification.ProblemContext)
            ? "Sem resumo adicional informado no momento."
            : qualification.ProblemContext;
        var portalLink = string.IsNullOrWhiteSpace(_options.ProviderPortalBaseUrl)
            ? string.Empty
            : $"""<p style="margin:24px 0 0;"><a href="{encoder.Encode(_options.ProviderPortalBaseUrl)}" style="color:#0d6efd;text-decoration:none;font-weight:600;">Abrir portal do prestador</a></p>""";

        var trackingPixel = openTrackingUrl is null
            ? string.Empty
            : $"""<img src="{encoder.Encode(openTrackingUrl.ToString())}" alt="" width="1" height="1" style="display:none;" />""";

        return $"""
<!DOCTYPE html>
<html lang="pt-BR">
<head>
  <meta charset="utf-8">
  <title>Oportunidade ConsertaPraMim</title>
</head>
<body style="margin:0;padding:24px;background:#f4f7fb;font-family:Segoe UI,Arial,sans-serif;color:#17202a;">
  <article style="max-width:680px;margin:0 auto;background:#ffffff;border-radius:18px;padding:32px;border:1px solid #d9e2ec;">
    <p style="margin:0 0 12px;font-size:14px;letter-spacing:.08em;text-transform:uppercase;color:#5b7083;">ConsertaPraMim</p>
    <h1 style="margin:0 0 16px;font-size:28px;line-height:1.2;">Nova oportunidade para sua regiao</h1>
    <p style="margin:0 0 20px;font-size:16px;line-height:1.6;">O cliente solicitou um atendimento que combina com sua categoria e raio de atuacao. A reserva oficial acontece apenas pelo link assinado abaixo.</p>
    <section style="padding:20px;border-radius:16px;background:#f7fafc;border:1px solid #e2e8f0;">
      <p style="margin:0 0 8px;"><strong>Categoria:</strong> {encoder.Encode(category)}</p>
      <p style="margin:0 0 8px;"><strong>Janela solicitada:</strong> {encoder.Encode(window)}</p>
      <p style="margin:0 0 8px;"><strong>Endereco:</strong> {encoder.Encode(address)}</p>
      <p style="margin:0;"><strong>Resumo:</strong> {encoder.Encode(summary)}</p>
    </section>
    <div style="display:flex;flex-wrap:wrap;gap:12px;margin:28px 0 12px;">
      <a href="{encoder.Encode(acceptUrl.ToString())}" style="display:inline-block;background:#0f766e;color:#ffffff;text-decoration:none;font-weight:700;padding:14px 20px;border-radius:12px;">Aceitar oportunidade</a>
      <a href="{encoder.Encode(declineUrl.ToString())}" style="display:inline-block;background:#ffffff;color:#991b1b;text-decoration:none;font-weight:700;padding:14px 20px;border-radius:12px;border:1px solid #fecaca;">Recusar oportunidade</a>
    </div>
    <p style="margin:0;font-size:13px;line-height:1.6;color:#52606d;">Os links sao assinados, expiram automaticamente e exigem confirmacao dentro da pagina segura. Respostas por texto em outros canais nao reservam o caso.</p>
    {portalLink}
  </article>
  {trackingPixel}
</body>
</html>
""";
    }

    private static string BuildAddressSummary(AdminKanbanJourneyQualificationRecord qualification)
    {
        var parts = new[]
        {
            qualification.Street,
            qualification.Neighborhood,
            qualification.City,
            qualification.State,
            qualification.PostalCode
        }.Where(part => !string.IsNullOrWhiteSpace(part)).ToList();

        return parts.Count == 0
            ? "Endereco confirmado na jornada, visivel apos a reserva valida."
            : string.Join(", ", parts);
    }

    private static string BuildSchedulingWindowLabel(AdminKanbanJourneySchedulingRecord scheduling)
    {
        var start = NormalizeUtc(scheduling.ScheduledStartAtUtc);
        var end = NormalizeUtc(scheduling.ScheduledEndAtUtc);
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
