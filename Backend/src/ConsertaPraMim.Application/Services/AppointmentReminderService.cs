using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ConsertaPraMim.Application.Services;

public class AppointmentReminderService : IAppointmentReminderService
{
    private static readonly ServiceAppointmentStatus[] ReminderEligibleStatuses =
    {
        ServiceAppointmentStatus.Confirmed,
        ServiceAppointmentStatus.RescheduleConfirmed
    };

    private readonly IServiceAppointmentRepository _appointmentRepository;
    private readonly IAppointmentReminderDispatchRepository _reminderRepository;
    private readonly INotificationService _notificationService;
    private readonly IEmailService _emailService;
    private readonly ILogger<AppointmentReminderService> _logger;
    private readonly int[] _offsetMinutes;
    private readonly int _maxAttempts;
    private readonly int _retryBaseDelaySeconds;

    public AppointmentReminderService(
        IServiceAppointmentRepository appointmentRepository,
        IAppointmentReminderDispatchRepository reminderRepository,
        INotificationService notificationService,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<AppointmentReminderService> logger)
    {
        _appointmentRepository = appointmentRepository;
        _reminderRepository = reminderRepository;
        _notificationService = notificationService;
        _emailService = emailService;
        _logger = logger;

        _offsetMinutes = ParseOffsets(configuration);
        _maxAttempts = ParseInt(configuration["ServiceAppointments:Reminders:MaxAttempts"], 3, 1, 10);
        _retryBaseDelaySeconds = ParseInt(configuration["ServiceAppointments:Reminders:RetryBaseDelaySeconds"], 60, 5, 3600);
    }

    public async Task ScheduleForAppointmentAsync(Guid appointmentId, string triggerReason)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(appointmentId);
        if (appointment == null)
        {
            return;
        }

        if (!ReminderEligibleStatuses.Contains(appointment.Status))
        {
            await CancelPendingForAppointmentAsync(appointmentId, $"Lembretes invalidados: agendamento em status {appointment.Status}.");
            return;
        }

        var nowUtc = DateTime.UtcNow;
        await _reminderRepository.CancelPendingByAppointmentAsync(
            appointmentId,
            $"Lembretes substituidos: {triggerReason}",
            nowUtc);

        var existing = await _reminderRepository.GetByAppointmentIdAsync(appointmentId);
        var existingActiveKeys = existing
            .Where(r => r.Status != AppointmentReminderDispatchStatus.Cancelled)
            .Select(r => r.EventKey)
            .ToHashSet(StringComparer.Ordinal);

        var actionUrl = $"/ServiceRequests/Details/{appointment.ServiceRequestId}";
        var pendingToAdd = new List<AppointmentReminderDispatch>();
        foreach (var recipient in BuildRecipients(appointment))
        {
            foreach (var channel in GetChannels())
            {
                foreach (var offset in _offsetMinutes)
                {
                    var scheduledForUtc = appointment.WindowStartUtc.AddMinutes(-offset);
                    var nextAttemptUtc = scheduledForUtc > nowUtc ? scheduledForUtc : nowUtc;
                    var eventKey = BuildEventKey(appointment, recipient.UserId, channel, offset);
                    if (existingActiveKeys.Contains(eventKey))
                    {
                        continue;
                    }

                    var (subject, message) = BuildReminderMessage(offset, appointment.WindowStartUtc, appointment.WindowEndUtc, appointment.Provider.Name, appointment.Client.Name);
                    pendingToAdd.Add(new AppointmentReminderDispatch
                    {
                        ServiceAppointmentId = appointment.Id,
                        RecipientUserId = recipient.UserId,
                        Channel = channel,
                        Status = AppointmentReminderDispatchStatus.Pending,
                        ReminderOffsetMinutes = offset,
                        ScheduledForUtc = scheduledForUtc,
                        NextAttemptAtUtc = nextAttemptUtc,
                        AttemptCount = 0,
                        MaxAttempts = _maxAttempts,
                        EventKey = eventKey,
                        Subject = subject,
                        Message = message,
                        ActionUrl = actionUrl
                    });
                }
            }
        }

        if (pendingToAdd.Count == 0)
        {
            return;
        }

        try
        {
            await _reminderRepository.AddRangeAsync(pendingToAdd);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Duplicate reminder scheduling ignored for appointment {AppointmentId}.", appointmentId);
        }
    }

    public async Task CancelPendingForAppointmentAsync(Guid appointmentId, string reason)
    {
        await _reminderRepository.CancelPendingByAppointmentAsync(appointmentId, reason, DateTime.UtcNow);
    }

    public async Task<int> ProcessDueRemindersAsync(int batchSize = 200, CancellationToken cancellationToken = default)
    {
        var due = await _reminderRepository.GetDueAsync(DateTime.UtcNow, batchSize);
        if (due.Count == 0)
        {
            return 0;
        }

        var processed = 0;
        foreach (var reminder in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var nowUtc = DateTime.UtcNow;

            if (reminder.ServiceAppointment == null || !ReminderEligibleStatuses.Contains(reminder.ServiceAppointment.Status))
            {
                reminder.Status = AppointmentReminderDispatchStatus.Cancelled;
                reminder.CancelledAtUtc = nowUtc;
                reminder.LastError = "Lembrete cancelado porque o agendamento nao esta mais elegivel.";
                reminder.UpdatedAt = nowUtc;
                await _reminderRepository.UpdateAsync(reminder);
                processed++;
                continue;
            }

            try
            {
                await SendReminderAsync(reminder);
                reminder.AttemptCount += 1;
                reminder.LastAttemptAtUtc = nowUtc;
                reminder.SentAtUtc = nowUtc;
                reminder.Status = AppointmentReminderDispatchStatus.Sent;
                reminder.UpdatedAt = nowUtc;
                await _reminderRepository.UpdateAsync(reminder);
                processed++;
            }
            catch (Exception ex)
            {
                reminder.AttemptCount += 1;
                reminder.LastAttemptAtUtc = nowUtc;
                reminder.LastError = Truncate(ex.Message, 1000);
                reminder.UpdatedAt = nowUtc;

                if (reminder.AttemptCount >= reminder.MaxAttempts)
                {
                    reminder.Status = AppointmentReminderDispatchStatus.FailedPermanent;
                    reminder.NextAttemptAtUtc = nowUtc;
                }
                else
                {
                    var retryDelaySeconds = CalculateBackoffSeconds(reminder.AttemptCount);
                    reminder.Status = AppointmentReminderDispatchStatus.FailedRetryable;
                    reminder.NextAttemptAtUtc = nowUtc.AddSeconds(retryDelaySeconds);
                }

                await _reminderRepository.UpdateAsync(reminder);
                processed++;
            }
        }

        return processed;
    }

    public async Task<AppointmentReminderDispatchListResultDto> GetDispatchesAsync(AppointmentReminderDispatchQueryDto query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var skip = (page - 1) * pageSize;

        var items = await _reminderRepository.QueryAsync(
            query.AppointmentId,
            query.Status,
            query.Channel,
            NormalizeToUtc(query.FromUtc),
            NormalizeToUtc(query.ToUtc),
            skip,
            pageSize);

        var total = await _reminderRepository.CountAsync(
            query.AppointmentId,
            query.Status,
            query.Channel,
            NormalizeToUtc(query.FromUtc),
            NormalizeToUtc(query.ToUtc));

        return new AppointmentReminderDispatchListResultDto(
            items.Select(MapToDto).ToList(),
            total,
            page,
            pageSize);
    }

    private async Task SendReminderAsync(AppointmentReminderDispatch reminder)
    {
        switch (reminder.Channel)
        {
            case AppointmentReminderChannel.InApp:
                await _notificationService.SendNotificationAsync(
                    reminder.RecipientUserId.ToString("N"),
                    reminder.Subject,
                    reminder.Message,
                    reminder.ActionUrl);
                break;
            case AppointmentReminderChannel.Email:
                var email = reminder.RecipientUser?.Email?.Trim();
                if (string.IsNullOrWhiteSpace(email))
                {
                    throw new InvalidOperationException("Nao foi possivel enviar lembrete por email: destinatario sem email.");
                }

                await _emailService.SendEmailAsync(email, reminder.Subject, reminder.Message);
                break;
            default:
                throw new InvalidOperationException($"Canal de lembrete nao suportado: {reminder.Channel}.");
        }
    }

    private static IEnumerable<(Guid UserId, string Name, string Email)> BuildRecipients(ServiceAppointment appointment)
    {
        yield return (appointment.ClientId, appointment.Client.Name, appointment.Client.Email);
        yield return (appointment.ProviderId, appointment.Provider.Name, appointment.Provider.Email);
    }

    private static AppointmentReminderChannel[] GetChannels()
    {
        return
        [
            AppointmentReminderChannel.InApp,
            AppointmentReminderChannel.Email
        ];
    }

    private static string BuildEventKey(
        ServiceAppointment appointment,
        Guid recipientUserId,
        AppointmentReminderChannel channel,
        int offsetMinutes)
    {
        return $"{appointment.Id:N}:{appointment.WindowStartUtc:yyyyMMddHHmm}:{recipientUserId:N}:{channel}:{offsetMinutes}";
    }

    private static (string Subject, string Message) BuildReminderMessage(
        int offsetMinutes,
        DateTime windowStartUtc,
        DateTime windowEndUtc,
        string providerName,
        string clientName)
    {
        var label = offsetMinutes switch
        {
            1440 => "em 24 horas",
            120 => "em 2 horas",
            30 => "em 30 minutos",
            _ => $"em {offsetMinutes} minutos"
        };

        var subject = $"Lembrete de agendamento {label}";
        var message =
            $"Seu atendimento entre cliente {clientName} e prestador {providerName} esta agendado para {windowStartUtc:dd/MM HH:mm} ate {windowEndUtc:HH:mm} (UTC).";
        return (subject, message);
    }

    private int CalculateBackoffSeconds(int attemptCount)
    {
        var exponent = Math.Max(0, attemptCount - 1);
        var delay = _retryBaseDelaySeconds * Math.Pow(2, exponent);
        return (int)Math.Clamp(delay, _retryBaseDelaySeconds, 21600);
    }

    private static DateTime? NormalizeToUtc(DateTime? value)
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

    private static AppointmentReminderDispatchDto MapToDto(AppointmentReminderDispatch reminder)
    {
        return new AppointmentReminderDispatchDto(
            reminder.Id,
            reminder.ServiceAppointmentId,
            reminder.RecipientUserId,
            reminder.RecipientUser?.Email ?? string.Empty,
            reminder.Channel,
            reminder.Status,
            reminder.ReminderOffsetMinutes,
            reminder.ScheduledForUtc,
            reminder.NextAttemptAtUtc,
            reminder.AttemptCount,
            reminder.MaxAttempts,
            reminder.EventKey,
            reminder.Subject,
            reminder.Message,
            reminder.ActionUrl,
            reminder.LastAttemptAtUtc,
            reminder.SentAtUtc,
            reminder.CancelledAtUtc,
            reminder.LastError,
            reminder.CreatedAt,
            reminder.UpdatedAt);
    }

    private static int[] ParseOffsets(IConfiguration configuration)
    {
        var configured = configuration
            .GetSection("ServiceAppointments:Reminders:OffsetsMinutes")
            .GetChildren()
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => int.TryParse(v, out var parsed) ? parsed : (int?)null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToArray();

        if (configured.Length > 0)
        {
            return configured
                .Select(v => Math.Clamp(v, 1, 10080))
                .Distinct()
                .OrderByDescending(v => v)
                .ToArray();
        }

        return [1440, 120, 30];
    }

    private static int ParseInt(string? raw, int defaultValue, int min, int max)
    {
        if (!int.TryParse(raw, out var parsed))
        {
            return defaultValue;
        }

        return Math.Clamp(parsed, min, max);
    }

    private static string Truncate(string value, int max)
    {
        if (value.Length <= max)
        {
            return value;
        }

        return value[..max];
    }
}
