using AppMobileCPM.Integrations.Journey;
using AppMobileCPM.Integrations.Telegram;
using AppMobileCPM.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Integrations.Journey;

public sealed class JourneyProviderConnectionServiceTests
{
    [Fact(DisplayName = "Journey Provider Connection | Deve avisar cliente no Telegram e atualizar agenda quando houver evento")]
    public async Task ConnectAsync_DeveAvisarClienteNoTelegramEAtualizarAgenda()
    {
        var lead = BuildLead(801, hasTelegramChat: true, hasCalendarEvent: true);
        var target = lead.Journey.Dispatch.Targets[0];
        var calendarGateway = new Mock<IJourneyCalendarGateway>(MockBehavior.Strict);
        var telegramClient = new Mock<ITelegramBridgeDeliveryClient>(MockBehavior.Strict);
        var kanbanService = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        var closureService = new Mock<IJourneyServiceClosureService>(MockBehavior.Strict);

        calendarGateway
            .Setup(service => service.UpdateEventAsync(
                "calendar-event-801",
                It.Is<JourneyCalendarEventUpsertRequest>(request => request.Metadata["reserved_provider_name"] == "Prestador Teste"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JourneyCalendarEventUpsertResult
            {
                Success = true,
                EventId = "calendar-event-801",
                EventLink = "https://calendar.google.com/event?eid=801"
            });
        telegramClient
            .Setup(service => service.SendHumanReplyAsync(It.Is<TelegramBridgeHumanReplyRequest>(request =>
                request.LeadId == 801 &&
                request.TelegramChatId == 5511999999999 &&
                request.ActivateHumanHandoff &&
                request.MessageText.Contains("Prestador Teste", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramBridgeHumanReplyResult
            {
                Success = true,
                HttpStatusCode = 200,
                Message = "Mensagem entregue ao Telegram.",
                HumanHandoffActivated = true
            });
        kanbanService
            .Setup(service => service.AddHistoryEvent(801, "jornada_conexao_direta_liberada", It.Is<string>(value =>
                value.Contains("Agenda: atualizada", StringComparison.Ordinal) &&
                value.Contains("Cliente: avisado", StringComparison.Ordinal) &&
                value.Contains("Prestador: avisado", StringComparison.Ordinal))))
            .Returns(true);
        closureService
            .Setup(service => service.StartServiceAsync(801, new DateTime(2026, 3, 22, 12, 0, 0, DateTimeKind.Utc), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JourneyServiceClosureStartResult
            {
                Success = true,
                Message = "Encerramento iniciado."
            });

        var sut = new JourneyProviderConnectionService(
            calendarGateway.Object,
            telegramClient.Object,
            kanbanService.Object,
            closureService.Object,
            Options.Create(new JourneyProviderNotificationOptions
            {
                Enabled = true,
                EmailEnabled = true,
                EmailTransport = "log",
                SenderEmail = "robot@consertapramim.com",
                SenderDisplayName = "ConsertaPraMim"
            }),
            NullLogger<JourneyProviderConnectionService>.Instance);

        var result = await sut.ConnectAsync(new JourneyProviderConnectionRequest
        {
            Lead = lead,
            Target = target,
            ReservedAtUtc = new DateTime(2026, 3, 22, 12, 0, 0, DateTimeKind.Utc)
        });

        Assert.True(result.Success);
        Assert.True(result.CalendarUpdated);
        Assert.True(result.ClientNotified);
        Assert.True(result.ProviderNotified);
        calendarGateway.VerifyAll();
        telegramClient.VerifyAll();
        kanbanService.VerifyAll();
        closureService.VerifyAll();
    }

    [Fact(DisplayName = "Journey Provider Connection | Deve registrar alerta quando cliente nao tiver canal de contato")]
    public async Task ConnectAsync_DeveRegistrarAlertaQuandoClienteNaoTiverCanalDeContato()
    {
        var lead = BuildLead(802, hasTelegramChat: false, hasCalendarEvent: false, includePhone: false, includeEmail: false);
        var target = lead.Journey.Dispatch.Targets[0];
        var calendarGateway = new Mock<IJourneyCalendarGateway>(MockBehavior.Strict);
        var telegramClient = new Mock<ITelegramBridgeDeliveryClient>(MockBehavior.Strict);
        var kanbanService = new Mock<IAdminKanbanService>(MockBehavior.Strict);
        var closureService = new Mock<IJourneyServiceClosureService>(MockBehavior.Strict);

        kanbanService
            .Setup(service => service.AddHistoryEvent(802, "jornada_conexao_direta_liberada", It.Is<string>(value =>
                value.Contains("Cliente: nao avisado", StringComparison.Ordinal) &&
                value.Contains("Prestador: avisado", StringComparison.Ordinal))))
            .Returns(true);
        closureService
            .Setup(service => service.StartServiceAsync(802, new DateTime(2026, 3, 22, 12, 30, 0, DateTimeKind.Utc), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JourneyServiceClosureStartResult
            {
                Success = true,
                Message = "Encerramento iniciado."
            });

        var sut = new JourneyProviderConnectionService(
            calendarGateway.Object,
            telegramClient.Object,
            kanbanService.Object,
            closureService.Object,
            Options.Create(new JourneyProviderNotificationOptions
            {
                Enabled = true,
                EmailEnabled = true,
                EmailTransport = "log",
                SenderEmail = "robot@consertapramim.com",
                SenderDisplayName = "ConsertaPraMim"
            }),
            NullLogger<JourneyProviderConnectionService>.Instance);

        var result = await sut.ConnectAsync(new JourneyProviderConnectionRequest
        {
            Lead = lead,
            Target = target,
            ReservedAtUtc = new DateTime(2026, 3, 22, 12, 30, 0, DateTimeKind.Utc)
        });

        Assert.False(result.Success);
        Assert.False(result.CalendarUpdated);
        Assert.False(result.ClientNotified);
        Assert.True(result.ProviderNotified);
        Assert.Contains("Cliente:", result.Message, StringComparison.Ordinal);
        calendarGateway.VerifyNoOtherCalls();
        telegramClient.VerifyNoOtherCalls();
        kanbanService.VerifyAll();
        closureService.VerifyAll();
    }

    private static AdminKanbanLeadDetailsRecord BuildLead(
        int leadId,
        bool hasTelegramChat,
        bool hasCalendarEvent,
        bool includePhone = true,
        bool includeEmail = true)
    {
        return new AdminKanbanLeadDetailsRecord
        {
            Id = leadId,
            StageId = 18,
            StageName = AdminKanbanJourneyClientStageNames.ProviderConnected,
            BoardType = AdminKanbanBoardTypes.Clients,
            Name = "Cliente Teste",
            Phone = includePhone ? "11999990000" : string.Empty,
            Email = includeEmail ? "cliente@teste.com" : string.Empty,
            ServiceCategory = "Eletricista",
            PostalCode = "11701-200",
            City = "Praia Grande",
            Source = "telegram",
            Priority = "normal",
            StatusNote = "Chuveiro nao esquenta.",
            InternalNotes = string.Empty,
            CreatedAt = new DateTime(2026, 3, 22, 10, 0, 0, DateTimeKind.Utc),
            Telegram = new AdminKanbanTelegramLinkRecord
            {
                TelegramChatId = hasTelegramChat ? 5511999999999 : null
            },
            Journey = new AdminKanbanLeadJourneyRecord
            {
                JourneyId = leadId + 9000,
                JourneyPublicId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                LeadId = leadId,
                BoardType = AdminKanbanBoardTypes.Clients,
                SourceChannel = AdminKanbanJourneySourceChannels.Telegram,
                SourceOrigin = "telegram-bot",
                CurrentState = AdminKanbanJourneyStates.ProviderConnected,
                PrimaryPhone = includePhone ? "11999990000" : string.Empty,
                PrimaryEmail = includeEmail ? "cliente@teste.com" : string.Empty,
                Qualification = new AdminKanbanJourneyQualificationRecord
                {
                    NormalizedServiceCategoryName = "Eletricista",
                    ProblemContext = "Chuveiro nao esquenta.",
                    Street = "Rua Bahia",
                    Neighborhood = "Ocian",
                    City = "Praia Grande",
                    State = "SP",
                    PostalCode = "11701-200"
                },
                Scheduling = new AdminKanbanJourneySchedulingRecord
                {
                    Status = AdminKanbanJourneySchedulingStatuses.Confirmed,
                    GoogleCalendarEventId = hasCalendarEvent ? $"calendar-event-{leadId}" : string.Empty,
                    GoogleCalendarEventLink = hasCalendarEvent ? $"https://calendar.google.com/event?eid={leadId}" : string.Empty,
                    ScheduledStartAtUtc = new DateTime(2026, 3, 23, 14, 0, 0, DateTimeKind.Utc),
                    ScheduledEndAtUtc = new DateTime(2026, 3, 23, 15, 0, 0, DateTimeKind.Utc)
                },
                Dispatch = new AdminKanbanJourneyDispatchRecord
                {
                    Status = AdminKanbanJourneyDispatchStatuses.Reserved,
                    ReservedProviderId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    ReservedProviderName = "Prestador Teste",
                    ReservedProviderEmail = "prestador@teste.com",
                    ReservedProviderPhone = "13999998888",
                    Targets =
                    [
                        new AdminKanbanJourneyDispatchTargetRecord
                        {
                            TargetKey = $"lead:{leadId}:wave:1:provider:33333333333333333333333333333333",
                            ProviderId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                            ProviderName = "Prestador Teste",
                            ProviderEmail = "prestador@teste.com",
                            ProviderPhone = "13999998888",
                            Status = AdminKanbanJourneyDispatchTargetStatuses.Accepted,
                            WaveNumber = 1,
                            RankPosition = 1,
                            CreatedAtUtc = new DateTime(2026, 3, 22, 10, 5, 0, DateTimeKind.Utc)
                        }
                    ]
                }
            },
            History = []
        };
    }
}
