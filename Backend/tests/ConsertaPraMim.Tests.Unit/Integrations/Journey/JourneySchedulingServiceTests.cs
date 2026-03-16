using AppMobileCPM.Integrations.Journey;
using AppMobileCPM.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Integrations.Journey;

public sealed class JourneySchedulingServiceTests
{
    [Fact(DisplayName = "Journey Scheduling | Deve sugerir janelas para cliente qualificado no Telegram")]
    public async Task ProcessTelegramTurnAsync_DeveSugerirJanelasQuandoLeadEstiverQualificado()
    {
        var kanbanService = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        var calendarGateway = new Mock<IJourneyCalendarGateway>(MockBehavior.Strict);
        var chatbotConversationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var journey = BuildJourney(
            leadId: 81,
            journeyId: 17,
            currentState: AdminKanbanJourneyStates.QualificationValidated);

        kanbanService
            .Setup(service => service.FindLeadIdByTelegramChatbotConversationId(chatbotConversationId))
            .Returns(81);
        kanbanService
            .Setup(service => service.GetJourneyDetails(81))
            .Returns(journey);
        calendarGateway
            .Setup(service => service.ListBusySlotsAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        kanbanService
            .Setup(service => service.UpdateJourneyScheduling(
                81,
                It.Is<AdminKanbanJourneySchedulingUpdateRequest>(request =>
                    request.Status == AdminKanbanJourneySchedulingStatuses.SlotSuggested &&
                    request.CurrentState == AdminKanbanJourneyStates.SlotSuggested &&
                    request.SuggestedSlots.Count == 2 &&
                    request.HistoryEventType == "agenda_janela_sugerida")))
            .Returns(new AdminKanbanJourneySchedulingUpdateResult
            {
                LeadId = 81,
                JourneyId = 17,
                CurrentState = AdminKanbanJourneyStates.SlotSuggested,
                Scheduling = new AdminKanbanJourneySchedulingRecord
                {
                    Status = AdminKanbanJourneySchedulingStatuses.SlotSuggested,
                    SuggestedSlots =
                    [
                        new AdminKanbanJourneySuggestedSlotRecord
                        {
                            OptionNumber = 1,
                            StartsAtUtc = new DateTime(2026, 3, 16, 11, 0, 0, DateTimeKind.Utc),
                            EndsAtUtc = new DateTime(2026, 3, 16, 12, 0, 0, DateTimeKind.Utc),
                            Label = "Hoje, 16/03, 08:00 as 09:00"
                        },
                        new AdminKanbanJourneySuggestedSlotRecord
                        {
                            OptionNumber = 2,
                            StartsAtUtc = new DateTime(2026, 3, 16, 12, 0, 0, DateTimeKind.Utc),
                            EndsAtUtc = new DateTime(2026, 3, 16, 13, 0, 0, DateTimeKind.Utc),
                            Label = "Hoje, 16/03, 09:00 as 10:00"
                        }
                    ]
                }
            });

        var sut = CreateSut(kanbanService.Object, calendarGateway.Object);

        var result = await sut.ProcessTelegramTurnAsync(
            new JourneySchedulingTurnRequest
            {
                ChatbotConversationId = chatbotConversationId,
                ChannelConversationId = "5513997114422",
                TelegramChatId = 5513997114422,
                MessageText = "Quero agendar uma visita",
                MessageSentAtUtc = new DateTime(2026, 3, 16, 10, 0, 0, DateTimeKind.Utc)
            });

        Assert.True(result.Success);
        Assert.True(result.Handled);
        Assert.Equal(AdminKanbanJourneyStates.SlotSuggested, result.CurrentState);
        Assert.Equal(AdminKanbanJourneySchedulingStatuses.SlotSuggested, result.SchedulingStatus);
        Assert.Equal(2, result.SuggestedSlots.Count);
        Assert.Contains("Responda com 1, 2 ou 3", result.ReplyText, StringComparison.Ordinal);
        kanbanService.VerifyAll();
        calendarGateway.VerifyAll();
    }

    [Fact(DisplayName = "Journey Scheduling | Deve confirmar janela selecionada e criar evento no Google Calendar")]
    public async Task ProcessTelegramTurnAsync_DeveConfirmarJanelaSelecionada()
    {
        var kanbanService = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        var calendarGateway = new Mock<IJourneyCalendarGateway>(MockBehavior.Strict);
        var chatbotConversationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var journey = BuildJourney(
            leadId: 82,
            journeyId: 18,
            currentState: AdminKanbanJourneyStates.SlotSuggested,
            scheduling: new AdminKanbanJourneySchedulingRecord
            {
                Status = AdminKanbanJourneySchedulingStatuses.SlotSuggested,
                SuggestedAtUtc = new DateTime(2026, 3, 16, 10, 0, 0, DateTimeKind.Utc),
                SuggestedSlots =
                [
                    new AdminKanbanJourneySuggestedSlotRecord
                    {
                        OptionNumber = 1,
                        StartsAtUtc = new DateTime(2026, 3, 16, 11, 0, 0, DateTimeKind.Utc),
                        EndsAtUtc = new DateTime(2026, 3, 16, 12, 0, 0, DateTimeKind.Utc),
                        Label = "Hoje, 16/03, 08:00 as 09:00"
                    },
                    new AdminKanbanJourneySuggestedSlotRecord
                    {
                        OptionNumber = 2,
                        StartsAtUtc = new DateTime(2026, 3, 16, 12, 0, 0, DateTimeKind.Utc),
                        EndsAtUtc = new DateTime(2026, 3, 16, 13, 0, 0, DateTimeKind.Utc),
                        Label = "Hoje, 16/03, 09:00 as 10:00"
                    }
                ]
            });

        kanbanService
            .Setup(service => service.FindLeadIdByTelegramChatbotConversationId(chatbotConversationId))
            .Returns(82);
        kanbanService
            .Setup(service => service.GetJourneyDetails(82))
            .Returns(journey);
        calendarGateway
            .Setup(service => service.CreateEventAsync(
                It.Is<JourneyCalendarEventUpsertRequest>(request =>
                    request.StartsAtUtc == new DateTime(2026, 3, 16, 12, 0, 0, DateTimeKind.Utc) &&
                    request.EndsAtUtc == new DateTime(2026, 3, 16, 13, 0, 0, DateTimeKind.Utc) &&
                    request.Metadata.ContainsKey("lead_id") &&
                    request.Metadata.ContainsKey("journey_id")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JourneyCalendarEventUpsertResult
            {
                Success = true,
                EventId = "cpm-jour-22222222",
                EventLink = "https://calendar.google.com/event?eid=abc"
            });
        kanbanService
            .Setup(service => service.UpdateJourneyScheduling(
                82,
                It.Is<AdminKanbanJourneySchedulingUpdateRequest>(request =>
                    request.Status == AdminKanbanJourneySchedulingStatuses.Confirmed &&
                    request.CurrentState == AdminKanbanJourneyStates.AppointmentConfirmed &&
                    request.GoogleCalendarEventId == "cpm-jour-22222222" &&
                    request.HistoryEventType == "agenda_confirmada")))
            .Returns(new AdminKanbanJourneySchedulingUpdateResult
            {
                LeadId = 82,
                JourneyId = 18,
                CurrentState = AdminKanbanJourneyStates.AppointmentConfirmed,
                Scheduling = new AdminKanbanJourneySchedulingRecord
                {
                    Status = AdminKanbanJourneySchedulingStatuses.Confirmed,
                    GoogleCalendarEventId = "cpm-jour-22222222",
                    GoogleCalendarEventLink = "https://calendar.google.com/event?eid=abc",
                    ScheduledStartAtUtc = new DateTime(2026, 3, 16, 12, 0, 0, DateTimeKind.Utc),
                    ScheduledEndAtUtc = new DateTime(2026, 3, 16, 13, 0, 0, DateTimeKind.Utc)
                }
            });

        var sut = CreateSut(kanbanService.Object, calendarGateway.Object);

        var result = await sut.ProcessTelegramTurnAsync(
            new JourneySchedulingTurnRequest
            {
                ChatbotConversationId = chatbotConversationId,
                ChannelConversationId = "5513997114422",
                TelegramChatId = 5513997114422,
                MessageText = "2",
                MessageSentAtUtc = new DateTime(2026, 3, 16, 10, 5, 0, DateTimeKind.Utc)
            });

        Assert.True(result.Success);
        Assert.True(result.Handled);
        Assert.Equal(AdminKanbanJourneyStates.AppointmentConfirmed, result.CurrentState);
        Assert.Equal(AdminKanbanJourneySchedulingStatuses.Confirmed, result.SchedulingStatus);
        Assert.Equal("cpm-jour-22222222", result.GoogleCalendarEventId);
        Assert.Contains("Atendimento confirmado", result.ReplyText, StringComparison.Ordinal);
        kanbanService.VerifyAll();
        calendarGateway.VerifyAll();
    }

    [Fact(DisplayName = "Journey Scheduling | Deve cancelar evento confirmado no Google Calendar")]
    public async Task ProcessTelegramTurnAsync_DeveCancelarEventoConfirmado()
    {
        var kanbanService = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        var calendarGateway = new Mock<IJourneyCalendarGateway>(MockBehavior.Strict);
        var chatbotConversationId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var journey = BuildJourney(
            leadId: 83,
            journeyId: 19,
            currentState: AdminKanbanJourneyStates.AppointmentConfirmed,
            scheduling: new AdminKanbanJourneySchedulingRecord
            {
                Status = AdminKanbanJourneySchedulingStatuses.Confirmed,
                GoogleCalendarEventId = "cpm-jour-33333333",
                ConfirmedAtUtc = new DateTime(2026, 3, 16, 10, 15, 0, DateTimeKind.Utc),
                ScheduledStartAtUtc = new DateTime(2026, 3, 16, 12, 0, 0, DateTimeKind.Utc),
                ScheduledEndAtUtc = new DateTime(2026, 3, 16, 13, 0, 0, DateTimeKind.Utc)
            });

        kanbanService
            .Setup(service => service.FindLeadIdByTelegramChatbotConversationId(chatbotConversationId))
            .Returns(83);
        kanbanService
            .Setup(service => service.GetJourneyDetails(83))
            .Returns(journey);
        calendarGateway
            .Setup(service => service.DeleteEventAsync("cpm-jour-33333333", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JourneyCalendarEventDeleteResult
            {
                Success = true
            });
        kanbanService
            .Setup(service => service.UpdateJourneyScheduling(
                83,
                It.Is<AdminKanbanJourneySchedulingUpdateRequest>(request =>
                    request.Status == AdminKanbanJourneySchedulingStatuses.Cancelled &&
                    request.CurrentState == AdminKanbanJourneyStates.AppointmentCancelled &&
                    request.HistoryEventType == "agenda_cancelada")))
            .Returns(new AdminKanbanJourneySchedulingUpdateResult
            {
                LeadId = 83,
                JourneyId = 19,
                CurrentState = AdminKanbanJourneyStates.AppointmentCancelled,
                Scheduling = new AdminKanbanJourneySchedulingRecord
                {
                    Status = AdminKanbanJourneySchedulingStatuses.Cancelled
                }
            });

        var sut = CreateSut(kanbanService.Object, calendarGateway.Object);

        var result = await sut.ProcessTelegramTurnAsync(
            new JourneySchedulingTurnRequest
            {
                ChatbotConversationId = chatbotConversationId,
                ChannelConversationId = "5513997114422",
                TelegramChatId = 5513997114422,
                MessageText = "cancelar agendamento",
                MessageSentAtUtc = new DateTime(2026, 3, 16, 10, 20, 0, DateTimeKind.Utc)
            });

        Assert.True(result.Success);
        Assert.True(result.Handled);
        Assert.Equal(AdminKanbanJourneyStates.AppointmentCancelled, result.CurrentState);
        Assert.Equal(AdminKanbanJourneySchedulingStatuses.Cancelled, result.SchedulingStatus);
        Assert.Contains("Agendamento cancelado", result.ReplyText, StringComparison.Ordinal);
        kanbanService.VerifyAll();
        calendarGateway.VerifyAll();
    }

    private static JourneySchedulingService CreateSut(
        IAdminKanbanService kanbanService,
        IJourneyCalendarGateway calendarGateway)
    {
        return new JourneySchedulingService(
            kanbanService,
            calendarGateway,
            Options.Create(new JourneySchedulingOptions
            {
                Enabled = true,
                ProjectId = "consertapramim",
                ServiceAccountEmail = "calendar@consertapramim.iam.gserviceaccount.com",
                PrivateKey = "-----BEGIN PRIVATE KEY-----\\nabc\\n-----END PRIVATE KEY-----\\n",
                CalendarId = "agenda@consertapramim.com",
                Timezone = "America/Sao_Paulo",
                BusinessHoursStartLocal = "08:00",
                BusinessHoursEndLocal = "12:00",
                SaturdayEnabled = true,
                SundayEnabled = false,
                SlotDurationMinutes = 60,
                SuggestionCount = 2,
                SuggestionWindowDays = 2,
                MinimumNoticeMinutes = 0,
                RequestTimeoutSeconds = 30,
                TokenRefreshSafetyMinutes = 5
            }),
            NullLogger<JourneySchedulingService>.Instance);
    }

    private static AdminKanbanLeadJourneyRecord BuildJourney(
        int leadId,
        int journeyId,
        string currentState,
        AdminKanbanJourneySchedulingRecord? scheduling = null)
    {
        return new AdminKanbanLeadJourneyRecord
        {
            JourneyId = journeyId,
            JourneyPublicId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            LeadId = leadId,
            BoardType = AdminKanbanBoardTypes.Clients,
            SourceChannel = AdminKanbanJourneySourceChannels.Telegram,
            SourceOrigin = "telegram-bot",
            CurrentState = currentState,
            ChatbotConversationId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            ChannelConversationId = "5513997114422",
            TelegramChatId = 5513997114422,
            PrimaryPhone = "+5513997114422",
            PrimaryEmail = "cliente@teste.com",
            CreatedAt = new DateTime(2026, 3, 16, 9, 0, 0, DateTimeKind.Utc),
            LastIntakeAt = new DateTime(2026, 3, 16, 9, 30, 0, DateTimeKind.Utc),
            Qualification = new AdminKanbanJourneyQualificationRecord
            {
                Status = AdminKanbanJourneyQualificationStatuses.Qualified,
                Source = AdminKanbanJourneyQualificationSources.Deterministic,
                ConfidenceScore = 0.92m,
                HasRequiredData = true,
                NeedsConfirmation = false,
                NormalizedServiceCategoryId = "eletricista",
                NormalizedServiceCategoryName = "Eletricista",
                ProblemContext = "Chuveiro queimado",
                Street = "Rua Bahia",
                Neighborhood = "Ocian",
                City = "Praia Grande",
                State = "SP",
                PostalCode = "11701-200"
            },
            Scheduling = scheduling ?? new AdminKanbanJourneySchedulingRecord()
        };
    }
}
