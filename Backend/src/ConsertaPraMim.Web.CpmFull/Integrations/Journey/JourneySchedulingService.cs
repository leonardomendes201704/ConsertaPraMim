using System.Globalization;
using System.Text.Json;
using AppMobileCPM.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneySchedulingService : IJourneySchedulingService
{
    private static readonly string[] SchedulingIntentKeywords =
    [
        "agendar",
        "agendamento",
        "marcar",
        "horario",
        "horario disponivel",
        "janela",
        "visita"
    ];

    private static readonly string[] RescheduleKeywords =
    [
        "reagendar",
        "remarcar",
        "outras opcoes",
        "outra opcao",
        "outro horario",
        "mudar horario",
        "alterar horario"
    ];

    private static readonly string[] CancelKeywords =
    [
        "cancelar agendamento",
        "cancelar visita",
        "desmarcar",
        "cancelar horario",
        "cancelar janela"
    ];

    private readonly IAdminKanbanService _kanbanService;
    private readonly IJourneyCalendarGateway _calendarGateway;
    private readonly JourneySchedulingOptions _options;
    private readonly ILogger<JourneySchedulingService> _logger;
    private readonly TimeZoneInfo _businessTimeZone;

    public JourneySchedulingService(
        IAdminKanbanService kanbanService,
        IJourneyCalendarGateway calendarGateway,
        IOptions<JourneySchedulingOptions> options,
        ILogger<JourneySchedulingService> logger)
    {
        _kanbanService = kanbanService;
        _calendarGateway = calendarGateway;
        _options = options.Value;
        _logger = logger;
        _businessTimeZone = ResolveTimeZone(_options.Timezone);
    }

    public async Task<JourneySchedulingTurnResult> ProcessTelegramTurnAsync(
        JourneySchedulingTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_options.Enabled)
        {
            return JourneySchedulingTurnResult.Disabled("Autoagendamento da jornada desabilitado no ambiente atual.");
        }

        if (request.ChatbotConversationId == Guid.Empty && request.TelegramChatId <= 0)
        {
            return JourneySchedulingTurnResult.Failed(StatusCodes.Status400BadRequest, "Identificador da conversa Telegram invalido para autoagendamento.");
        }

        var leadId = request.ChatbotConversationId != Guid.Empty
            ? _kanbanService.FindLeadIdByTelegramChatbotConversationId(request.ChatbotConversationId)
            : _kanbanService.FindLeadIdByTelegramChatId(request.TelegramChatId);
        if (!leadId.HasValue || leadId.Value <= 0)
        {
            return JourneySchedulingTurnResult.NoOp();
        }

        var journey = _kanbanService.GetJourneyDetails(leadId.Value);
        if (journey is null ||
            !string.Equals(journey.SourceChannel, AdminKanbanJourneySourceChannels.Telegram, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(journey.BoardType, AdminKanbanBoardTypes.Clients, StringComparison.OrdinalIgnoreCase))
        {
            return JourneySchedulingTurnResult.NoOp();
        }

        var normalizedMessage = NormalizeMessage(request.MessageText);
        var schedule = journey.Scheduling ?? new AdminKanbanJourneySchedulingRecord();

        if (IsCancelIntent(normalizedMessage) &&
            (string.Equals(schedule.Status, AdminKanbanJourneySchedulingStatuses.Confirmed, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(schedule.Status, AdminKanbanJourneySchedulingStatuses.SlotSuggested, StringComparison.OrdinalIgnoreCase) ||
             !string.IsNullOrWhiteSpace(schedule.GoogleCalendarEventId)))
        {
            return await CancelSchedulingAsync(journey, request, cancellationToken);
        }

        if (IsRescheduleIntent(normalizedMessage))
        {
            return await SuggestSlotsAsync(
                journey,
                request,
                referenceUtc: ResolveRescheduleReferenceUtc(schedule),
                force: true,
                cancellationToken);
        }

        if (string.Equals(schedule.Status, AdminKanbanJourneySchedulingStatuses.SlotSuggested, StringComparison.OrdinalIgnoreCase) &&
            TryResolveSelectedSlot(schedule.SuggestedSlots, normalizedMessage, out var selectedSlot))
        {
            return await ConfirmSlotAsync(journey, request, selectedSlot, cancellationToken);
        }

        if (string.Equals(journey.CurrentState, AdminKanbanJourneyStates.QualificationValidated, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(schedule.Status, AdminKanbanJourneySchedulingStatuses.Confirmed, StringComparison.OrdinalIgnoreCase) &&
            (!string.Equals(schedule.Status, AdminKanbanJourneySchedulingStatuses.SlotSuggested, StringComparison.OrdinalIgnoreCase) || IsSchedulingIntent(normalizedMessage)))
        {
            return await SuggestSlotsAsync(
                journey,
                request,
                referenceUtc: NormalizeUtc(request.MessageSentAtUtc) ?? DateTime.UtcNow,
                force: !string.Equals(schedule.Status, AdminKanbanJourneySchedulingStatuses.SlotSuggested, StringComparison.OrdinalIgnoreCase),
                cancellationToken);
        }

        return JourneySchedulingTurnResult.NoOp();
    }

    private async Task<JourneySchedulingTurnResult> SuggestSlotsAsync(
        AdminKanbanLeadJourneyRecord journey,
        JourneySchedulingTurnRequest request,
        DateTime referenceUtc,
        bool force,
        CancellationToken cancellationToken)
    {
        if (!force &&
            string.Equals(journey.Scheduling.Status, AdminKanbanJourneySchedulingStatuses.SlotSuggested, StringComparison.OrdinalIgnoreCase) &&
            journey.Scheduling.SuggestedSlots.Count > 0)
        {
            return BuildSuggestedSlotsResponse(journey, journey.Scheduling);
        }

        var slots = await BuildSuggestedSlotsAsync(referenceUtc, cancellationToken);
        if (slots.Count == 0)
        {
            var noAvailabilityScheduling = new AdminKanbanJourneySchedulingRecord
            {
                Status = AdminKanbanJourneySchedulingStatuses.NoAvailability,
                Summary = "Nao foram encontradas janelas disponiveis no horizonte configurado."
            };

            _kanbanService.UpdateJourneyScheduling(
                journey.LeadId,
                new AdminKanbanJourneySchedulingUpdateRequest
                {
                    Status = noAvailabilityScheduling.Status,
                    Summary = noAvailabilityScheduling.Summary,
                    CurrentState = AdminKanbanJourneyStates.QualificationValidated,
                    SourceChannel = AdminKanbanJourneySourceChannels.Telegram,
                    HistoryEventType = "agenda_sem_disponibilidade",
                    HistoryDescription = "Autoagendamento nao encontrou janelas livres no Google Calendar.",
                    MetadataJson = BuildSchedulingMetadataJson("no_availability", request, noAvailabilityScheduling)
                });

            return new JourneySchedulingTurnResult
            {
                Success = true,
                Handled = true,
                HttpStatusCode = StatusCodes.Status200OK,
                LeadId = journey.LeadId,
                JourneyId = journey.JourneyId,
                CurrentState = AdminKanbanJourneyStates.QualificationValidated,
                SchedulingStatus = AdminKanbanJourneySchedulingStatuses.NoAvailability,
                Message = "Nenhuma janela disponivel foi encontrada.",
                ReplyText = "Nao encontrei janelas livres na agenda oficial neste momento. Responda com \"reagendar\" mais tarde para tentar novas opcoes.",
                RemoveReplyKeyboard = true
            };
        }

        var scheduling = new AdminKanbanJourneySchedulingRecord
        {
            Status = AdminKanbanJourneySchedulingStatuses.SlotSuggested,
            Summary = $"Foram sugeridas {slots.Count} janelas para confirmacao do cliente.",
            SuggestedAtUtc = DateTime.UtcNow,
            SuggestedSlots = slots.Select((slot, index) => new AdminKanbanJourneySuggestedSlotRecord
            {
                OptionNumber = index + 1,
                StartsAtUtc = slot.StartsAtUtc,
                EndsAtUtc = slot.EndsAtUtc,
                Label = slot.Label
            }).ToList()
        };

        _kanbanService.UpdateJourneyScheduling(
            journey.LeadId,
            new AdminKanbanJourneySchedulingUpdateRequest
            {
                Status = scheduling.Status,
                Summary = scheduling.Summary,
                SuggestedAtUtc = scheduling.SuggestedAtUtc,
                CurrentState = AdminKanbanJourneyStates.SlotSuggested,
                SourceChannel = AdminKanbanJourneySourceChannels.Telegram,
                HistoryEventType = "agenda_janela_sugerida",
                HistoryDescription = "Autoagendamento sugeriu janelas de atendimento ao cliente no Telegram.",
                MetadataJson = BuildSchedulingMetadataJson("suggested", request, scheduling),
                SuggestedSlots = scheduling.SuggestedSlots
            });

        return BuildSuggestedSlotsResponse(journey, scheduling);
    }

    private async Task<JourneySchedulingTurnResult> ConfirmSlotAsync(
        AdminKanbanLeadJourneyRecord journey,
        JourneySchedulingTurnRequest request,
        AdminKanbanJourneySuggestedSlotRecord selectedSlot,
        CancellationToken cancellationToken)
    {
        var eventRequest = BuildCalendarUpsertRequest(journey, selectedSlot);
        var currentSchedule = journey.Scheduling;
        var upsertResult = string.IsNullOrWhiteSpace(currentSchedule.GoogleCalendarEventId)
            ? await _calendarGateway.CreateEventAsync(eventRequest, cancellationToken)
            : await _calendarGateway.UpdateEventAsync(currentSchedule.GoogleCalendarEventId, eventRequest, cancellationToken);

        if (!upsertResult.Success)
        {
            _kanbanService.UpdateJourneyScheduling(
                journey.LeadId,
                new AdminKanbanJourneySchedulingUpdateRequest
                {
                    Status = AdminKanbanJourneySchedulingStatuses.SlotSuggested,
                    Summary = "Falha ao confirmar a janela sugerida no Google Calendar.",
                    SuggestedAtUtc = currentSchedule.SuggestedAtUtc,
                    CurrentState = AdminKanbanJourneyStates.SlotSuggested,
                    SourceChannel = AdminKanbanJourneySourceChannels.Telegram,
                    HistoryEventType = "agenda_confirmacao_falhou",
                    HistoryDescription = "Tentativa de confirmar a janela no Google Calendar falhou.",
                    MetadataJson = BuildSchedulingMetadataJson(
                        "confirm_failed",
                        request,
                        currentSchedule,
                        new Dictionary<string, object?>
                        {
                            ["selectedOption"] = selectedSlot.OptionNumber,
                            ["calendarErrorCode"] = upsertResult.ErrorCode,
                            ["calendarErrorMessage"] = upsertResult.ErrorMessage
                        }),
                    SuggestedSlots = currentSchedule.SuggestedSlots
                });

            return new JourneySchedulingTurnResult
            {
                Success = false,
                Handled = true,
                HttpStatusCode = StatusCodes.Status502BadGateway,
                LeadId = journey.LeadId,
                JourneyId = journey.JourneyId,
                CurrentState = AdminKanbanJourneyStates.SlotSuggested,
                SchedulingStatus = AdminKanbanJourneySchedulingStatuses.SlotSuggested,
                Message = upsertResult.ErrorMessage,
                ReplyText = "Nao consegui confirmar essa janela na agenda oficial agora. Responda com \"reagendar\" para eu buscar novas opcoes.",
                RemoveReplyKeyboard = true
            };
        }

        var confirmedScheduling = new AdminKanbanJourneySchedulingRecord
        {
            Status = AdminKanbanJourneySchedulingStatuses.Confirmed,
            Summary = $"Atendimento confirmado para {selectedSlot.Label}.",
            GoogleCalendarEventId = upsertResult.EventId,
            GoogleCalendarEventLink = upsertResult.EventLink,
            ConfirmedAtUtc = DateTime.UtcNow,
            ScheduledStartAtUtc = selectedSlot.StartsAtUtc,
            ScheduledEndAtUtc = selectedSlot.EndsAtUtc,
            SuggestedSlots = currentSchedule.SuggestedSlots
        };

        _kanbanService.UpdateJourneyScheduling(
            journey.LeadId,
            new AdminKanbanJourneySchedulingUpdateRequest
            {
                Status = confirmedScheduling.Status,
                Summary = confirmedScheduling.Summary,
                GoogleCalendarEventId = confirmedScheduling.GoogleCalendarEventId,
                GoogleCalendarEventLink = confirmedScheduling.GoogleCalendarEventLink,
                ConfirmedAtUtc = confirmedScheduling.ConfirmedAtUtc,
                ScheduledStartAtUtc = confirmedScheduling.ScheduledStartAtUtc,
                ScheduledEndAtUtc = confirmedScheduling.ScheduledEndAtUtc,
                CurrentState = AdminKanbanJourneyStates.AppointmentConfirmed,
                SourceChannel = AdminKanbanJourneySourceChannels.Telegram,
                HistoryEventType = "agenda_confirmada",
                HistoryDescription = "Cliente confirmou a janela de atendimento no Telegram e o evento foi gravado no Google Calendar.",
                MetadataJson = BuildSchedulingMetadataJson(
                    "confirmed",
                    request,
                    confirmedScheduling,
                    new Dictionary<string, object?>
                    {
                        ["selectedOption"] = selectedSlot.OptionNumber
                    }),
                SuggestedSlots = currentSchedule.SuggestedSlots
            });

        return new JourneySchedulingTurnResult
        {
            Success = true,
            Handled = true,
            HttpStatusCode = StatusCodes.Status200OK,
            LeadId = journey.LeadId,
            JourneyId = journey.JourneyId,
            CurrentState = AdminKanbanJourneyStates.AppointmentConfirmed,
            SchedulingStatus = AdminKanbanJourneySchedulingStatuses.Confirmed,
            Message = "Janela confirmada com sucesso.",
            ReplyText = $"Perfeito. Atendimento confirmado para {selectedSlot.Label}. Se precisar alterar depois, responda com \"reagendar\" ou \"cancelar agendamento\".",
            RemoveReplyKeyboard = true,
            GoogleCalendarEventId = confirmedScheduling.GoogleCalendarEventId,
            GoogleCalendarEventLink = confirmedScheduling.GoogleCalendarEventLink,
            ScheduledStartAtUtc = confirmedScheduling.ScheduledStartAtUtc,
            ScheduledEndAtUtc = confirmedScheduling.ScheduledEndAtUtc,
            SuggestedSlots = confirmedScheduling.SuggestedSlots.Select(MapSuggestedSlot).ToList()
        };
    }

    private async Task<JourneySchedulingTurnResult> CancelSchedulingAsync(
        AdminKanbanLeadJourneyRecord journey,
        JourneySchedulingTurnRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(journey.Scheduling.GoogleCalendarEventId))
        {
            var deleteResult = await _calendarGateway.DeleteEventAsync(journey.Scheduling.GoogleCalendarEventId, cancellationToken);
            if (!deleteResult.Success)
            {
                return new JourneySchedulingTurnResult
                {
                    Success = false,
                    Handled = true,
                    HttpStatusCode = StatusCodes.Status502BadGateway,
                    LeadId = journey.LeadId,
                    JourneyId = journey.JourneyId,
                    CurrentState = journey.CurrentState,
                    SchedulingStatus = journey.Scheduling.Status,
                    Message = deleteResult.ErrorMessage,
                    ReplyText = "Nao consegui cancelar o evento na agenda oficial agora. Tente novamente em instantes.",
                    RemoveReplyKeyboard = true
                };
            }
        }

        var cancelledScheduling = new AdminKanbanJourneySchedulingRecord
        {
            Status = AdminKanbanJourneySchedulingStatuses.Cancelled,
            Summary = "Agendamento cancelado pelo cliente no canal Telegram.",
            CancelledAtUtc = DateTime.UtcNow
        };

        _kanbanService.UpdateJourneyScheduling(
            journey.LeadId,
            new AdminKanbanJourneySchedulingUpdateRequest
            {
                Status = cancelledScheduling.Status,
                Summary = cancelledScheduling.Summary,
                CancelledAtUtc = cancelledScheduling.CancelledAtUtc,
                CurrentState = AdminKanbanJourneyStates.AppointmentCancelled,
                SourceChannel = AdminKanbanJourneySourceChannels.Telegram,
                HistoryEventType = "agenda_cancelada",
                HistoryDescription = "Cliente cancelou a janela automatica de atendimento no Telegram.",
                MetadataJson = BuildSchedulingMetadataJson("cancelled", request, cancelledScheduling)
            });

        return new JourneySchedulingTurnResult
        {
            Success = true,
            Handled = true,
            HttpStatusCode = StatusCodes.Status200OK,
            LeadId = journey.LeadId,
            JourneyId = journey.JourneyId,
            CurrentState = AdminKanbanJourneyStates.AppointmentCancelled,
            SchedulingStatus = AdminKanbanJourneySchedulingStatuses.Cancelled,
            Message = "Agendamento cancelado com sucesso.",
            ReplyText = "Tudo certo. Agendamento cancelado. Quando quiser novas opcoes, responda com \"agendar\" ou \"reagendar\".",
            RemoveReplyKeyboard = true
        };
    }

    private async Task<IReadOnlyList<JourneySchedulingSuggestedSlot>> BuildSuggestedSlotsAsync(
        DateTime referenceUtc,
        CancellationToken cancellationToken)
    {
        var searchStartUtc = NormalizeUtc(referenceUtc).AddMinutes(Math.Max(_options.MinimumNoticeMinutes, 0));
        var searchEndUtc = searchStartUtc.AddDays(Math.Max(_options.SuggestionWindowDays, 1));
        var busySlots = await _calendarGateway.ListBusySlotsAsync(searchStartUtc, searchEndUtc, cancellationToken);
        var mergedBusySlots = MergeBusySlots(busySlots);
        var duration = TimeSpan.FromMinutes(_options.SlotDurationMinutes);
        var suggestions = new List<JourneySchedulingSuggestedSlot>();

        var searchStartLocal = TimeZoneInfo.ConvertTimeFromUtc(searchStartUtc, _businessTimeZone);
        var searchEndLocal = TimeZoneInfo.ConvertTimeFromUtc(searchEndUtc, _businessTimeZone);
        for (var cursorDate = searchStartLocal.Date;
             cursorDate <= searchEndLocal.Date && suggestions.Count < _options.SuggestionCount;
             cursorDate = cursorDate.AddDays(1))
        {
            if (!IsAllowedDay(cursorDate.DayOfWeek))
            {
                continue;
            }

            var dayStartLocal = cursorDate + ParseTime(_options.BusinessHoursStartLocal);
            var dayEndLocal = cursorDate + ParseTime(_options.BusinessHoursEndLocal);
            if (cursorDate == searchStartLocal.Date && searchStartLocal > dayStartLocal)
            {
                dayStartLocal = searchStartLocal;
            }

            var candidateStartLocal = dayStartLocal;
            while (candidateStartLocal.Add(duration) <= dayEndLocal && suggestions.Count < _options.SuggestionCount)
            {
                var candidateEndLocal = candidateStartLocal.Add(duration);
                var candidateStartUtc = TimeZoneInfo.ConvertTimeToUtc(candidateStartLocal, _businessTimeZone);
                var candidateEndUtc = TimeZoneInfo.ConvertTimeToUtc(candidateEndLocal, _businessTimeZone);
                if (!mergedBusySlots.Any(busy => Overlaps(candidateStartUtc, candidateEndUtc, busy.StartsAtUtc, busy.EndsAtUtc)))
                {
                    suggestions.Add(new JourneySchedulingSuggestedSlot
                    {
                        OptionNumber = suggestions.Count + 1,
                        StartsAtUtc = candidateStartUtc,
                        EndsAtUtc = candidateEndUtc,
                        Label = BuildSuggestedSlotLabel(candidateStartLocal, candidateEndLocal, searchStartLocal.Date)
                    });
                }

                candidateStartLocal = candidateStartLocal.AddMinutes(_options.SlotDurationMinutes);
            }
        }

        return suggestions;
    }

    private JourneyCalendarEventUpsertRequest BuildCalendarUpsertRequest(
        AdminKanbanLeadJourneyRecord journey,
        AdminKanbanJourneySuggestedSlotRecord selectedSlot)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["lead_id"] = journey.LeadId.ToString(CultureInfo.InvariantCulture),
            ["journey_id"] = journey.JourneyId.ToString(CultureInfo.InvariantCulture),
            ["journey_public_id"] = journey.JourneyPublicId.ToString("N"),
            ["board_type"] = journey.BoardType,
            ["source_channel"] = journey.SourceChannel
        };

        if (journey.TelegramChatId.HasValue && journey.TelegramChatId.Value > 0)
        {
            metadata["telegram_chat_id"] = journey.TelegramChatId.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(journey.Qualification.NormalizedServiceCategoryName))
        {
            metadata["service_category"] = journey.Qualification.NormalizedServiceCategoryName;
        }

        return new JourneyCalendarEventUpsertRequest
        {
            Title = $"ConsertaPraMim - Atendimento inicial #{journey.JourneyPublicId.ToString("N")[..8]}",
            StartsAtUtc = selectedSlot.StartsAtUtc,
            EndsAtUtc = selectedSlot.EndsAtUtc,
            Description = BuildCalendarDescription(journey),
            Location = BuildAddress(journey),
            Metadata = metadata,
            IdempotencyKey = $"cpm-jour-{journey.JourneyPublicId:N}"
        };
    }

    private JourneySchedulingTurnResult BuildSuggestedSlotsResponse(
        AdminKanbanLeadJourneyRecord journey,
        AdminKanbanJourneySchedulingRecord scheduling)
    {
        var numberedLines = scheduling.SuggestedSlots
            .OrderBy(item => item.OptionNumber)
            .Select(item => $"{item.OptionNumber}. {item.Label}");

        return new JourneySchedulingTurnResult
        {
            Success = true,
            Handled = true,
            HttpStatusCode = StatusCodes.Status200OK,
            LeadId = journey.LeadId,
            JourneyId = journey.JourneyId,
            CurrentState = AdminKanbanJourneyStates.SlotSuggested,
            SchedulingStatus = AdminKanbanJourneySchedulingStatuses.SlotSuggested,
            Message = "Janelas sugeridas com sucesso.",
            ReplyText = $"Encontrei estas janelas para atendimento:{Environment.NewLine}{string.Join(Environment.NewLine, numberedLines)}{Environment.NewLine}{Environment.NewLine}Responda com 1, 2 ou 3 para confirmar. Se quiser outras opcoes, responda com \"reagendar\".",
            RemoveReplyKeyboard = true,
            SuggestedSlots = scheduling.SuggestedSlots.Select(MapSuggestedSlot).ToList()
        };
    }

    private static JourneySchedulingSuggestedSlot MapSuggestedSlot(AdminKanbanJourneySuggestedSlotRecord slot) => new()
    {
        OptionNumber = slot.OptionNumber,
        StartsAtUtc = slot.StartsAtUtc,
        EndsAtUtc = slot.EndsAtUtc,
        Label = slot.Label
    };

    private static bool TryResolveSelectedSlot(
        IReadOnlyList<AdminKanbanJourneySuggestedSlotRecord> suggestedSlots,
        string normalizedMessage,
        out AdminKanbanJourneySuggestedSlotRecord selectedSlot)
    {
        selectedSlot = new AdminKanbanJourneySuggestedSlotRecord();
        if (suggestedSlots.Count == 0 || string.IsNullOrWhiteSpace(normalizedMessage))
        {
            return false;
        }

        if (int.TryParse(normalizedMessage.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var optionNumber))
        {
            var directMatch = suggestedSlots.FirstOrDefault(item => item.OptionNumber == optionNumber);
            if (directMatch is not null)
            {
                selectedSlot = directMatch;
                return true;
            }
        }

        foreach (var slot in suggestedSlots)
        {
            if (normalizedMessage.Contains($" {slot.OptionNumber} ", StringComparison.Ordinal) ||
                normalizedMessage.EndsWith($" {slot.OptionNumber}", StringComparison.Ordinal) ||
                normalizedMessage.StartsWith($"{slot.OptionNumber} ", StringComparison.Ordinal))
            {
                selectedSlot = slot;
                return true;
            }
        }

        return false;
    }

    private static bool IsSchedulingIntent(string normalizedMessage) =>
        !string.IsNullOrWhiteSpace(normalizedMessage) &&
        SchedulingIntentKeywords.Any(keyword => normalizedMessage.Contains(keyword, StringComparison.Ordinal));

    private static bool IsRescheduleIntent(string normalizedMessage) =>
        !string.IsNullOrWhiteSpace(normalizedMessage) &&
        RescheduleKeywords.Any(keyword => normalizedMessage.Contains(keyword, StringComparison.Ordinal));

    private static bool IsCancelIntent(string normalizedMessage) =>
        !string.IsNullOrWhiteSpace(normalizedMessage) &&
        CancelKeywords.Any(keyword => normalizedMessage.Contains(keyword, StringComparison.Ordinal));

    private static string NormalizeMessage(string? messageText) =>
        string.IsNullOrWhiteSpace(messageText)
            ? string.Empty
            : messageText.Trim().ToLowerInvariant();

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

    private static DateTime NormalizeUtc(DateTime value) => NormalizeUtc((DateTime?)value) ?? value;

    private DateTime ResolveRescheduleReferenceUtc(AdminKanbanJourneySchedulingRecord schedule)
    {
        if (schedule.SuggestedSlots.Count > 0)
        {
            return schedule.SuggestedSlots.Max(item => item.EndsAtUtc);
        }

        if (schedule.ScheduledEndAtUtc.HasValue)
        {
            return schedule.ScheduledEndAtUtc.Value;
        }

        return DateTime.UtcNow;
    }

    private static IReadOnlyList<JourneyCalendarBusySlot> MergeBusySlots(IReadOnlyList<JourneyCalendarBusySlot> busySlots)
    {
        if (busySlots.Count == 0)
        {
            return [];
        }

        var ordered = busySlots
            .OrderBy(item => item.StartsAtUtc)
            .ToList();
        var merged = new List<JourneyCalendarBusySlot> { ordered[0] };
        for (var index = 1; index < ordered.Count; index++)
        {
            var current = ordered[index];
            var last = merged[^1];
            if (current.StartsAtUtc <= last.EndsAtUtc)
            {
                merged[^1] = new JourneyCalendarBusySlot
                {
                    StartsAtUtc = last.StartsAtUtc,
                    EndsAtUtc = current.EndsAtUtc > last.EndsAtUtc ? current.EndsAtUtc : last.EndsAtUtc
                };
                continue;
            }

            merged.Add(current);
        }

        return merged;
    }

    private static bool Overlaps(DateTime startA, DateTime endA, DateTime startB, DateTime endB) =>
        startA < endB && endA > startB;

    private static TimeSpan ParseTime(string value) =>
        TimeOnly.TryParse(value, out var parsed)
            ? parsed.ToTimeSpan()
            : TimeSpan.FromHours(8);

    private bool IsAllowedDay(DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        DayOfWeek.Saturday => _options.SaturdayEnabled,
        DayOfWeek.Sunday => _options.SundayEnabled,
        _ => true
    };

    private string BuildSuggestedSlotLabel(DateTime startsAtLocal, DateTime endsAtLocal, DateTime referenceLocalDate)
    {
        var culture = CultureInfo.GetCultureInfo("pt-BR");
        var prefix = startsAtLocal.Date == referenceLocalDate
            ? "Hoje"
            : startsAtLocal.Date == referenceLocalDate.AddDays(1)
                ? "Amanha"
                : startsAtLocal.ToString("dddd", culture);

        return $"{prefix}, {startsAtLocal:dd/MM}, {startsAtLocal:HH:mm} as {endsAtLocal:HH:mm}";
    }

    private static string BuildAddress(AdminKanbanLeadJourneyRecord journey)
    {
        var addressParts = new[]
        {
            journey.Qualification.Street,
            journey.Qualification.Neighborhood,
            journey.Qualification.City,
            journey.Qualification.State,
            journey.Qualification.PostalCode
        }.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();

        return addressParts.Length == 0 ? "Endereco a confirmar" : string.Join(", ", addressParts);
    }

    private static string BuildCalendarDescription(AdminKanbanLeadJourneyRecord journey)
    {
        var parts = new List<string>
        {
            $"Lead: {journey.LeadId}",
            $"Journey: {journey.JourneyPublicId:N}",
            $"Canal: {AdminKanbanJourneySourceChannels.GetLabel(journey.SourceChannel)}",
            $"Endereco: {BuildAddress(journey)}"
        };

        if (!string.IsNullOrWhiteSpace(journey.PrimaryPhone))
        {
            parts.Add($"Telefone: {journey.PrimaryPhone}");
        }

        if (!string.IsNullOrWhiteSpace(journey.PrimaryEmail))
        {
            parts.Add($"Email: {journey.PrimaryEmail}");
        }

        if (!string.IsNullOrWhiteSpace(journey.Qualification.NormalizedServiceCategoryName))
        {
            parts.Add($"Categoria: {journey.Qualification.NormalizedServiceCategoryName}");
        }

        if (!string.IsNullOrWhiteSpace(journey.Qualification.ProblemContext))
        {
            parts.Add($"Contexto: {journey.Qualification.ProblemContext}");
        }

        return string.Join(Environment.NewLine, parts);
    }

    private static string BuildSchedulingMetadataJson(
        string action,
        JourneySchedulingTurnRequest request,
        AdminKanbanJourneySchedulingRecord scheduling,
        IReadOnlyDictionary<string, object?>? additional = null)
    {
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["action"] = action,
            ["chatbotConversationId"] = request.ChatbotConversationId,
            ["telegramChatId"] = request.TelegramChatId,
            ["messageText"] = request.MessageText,
            ["messageSentAtUtc"] = NormalizeUtc(request.MessageSentAtUtc),
            ["status"] = scheduling.Status,
            ["summary"] = scheduling.Summary,
            ["googleCalendarEventId"] = scheduling.GoogleCalendarEventId,
            ["googleCalendarEventLink"] = scheduling.GoogleCalendarEventLink,
            ["suggestedAtUtc"] = scheduling.SuggestedAtUtc,
            ["confirmedAtUtc"] = scheduling.ConfirmedAtUtc,
            ["cancelledAtUtc"] = scheduling.CancelledAtUtc,
            ["scheduledStartAtUtc"] = scheduling.ScheduledStartAtUtc,
            ["scheduledEndAtUtc"] = scheduling.ScheduledEndAtUtc,
            ["suggestedSlots"] = scheduling.SuggestedSlots
        };

        if (additional is not null)
        {
            foreach (var item in additional)
            {
                payload[item.Key] = item.Value;
            }
        }

        return JsonSerializer.Serialize(payload);
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        var normalized = string.IsNullOrWhiteSpace(timeZoneId)
            ? "America/Sao_Paulo"
            : timeZoneId.Trim();

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(normalized);
        }
        catch (TimeZoneNotFoundException) when (string.Equals(normalized, "America/Sao_Paulo", StringComparison.OrdinalIgnoreCase))
        {
            return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
