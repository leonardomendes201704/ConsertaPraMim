using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AppointmentReminderServiceTests
{
    /// <summary>
    /// Cenario: reprocessamento de agendamento nao pode duplicar reminders com a mesma chave de evento.
    /// Passos: injeta reminder existente com EventKey conhecido e executa ScheduleForAppointmentAsync.
    /// Resultado esperado: novos dispatches sao criados sem repetir a chave ja registrada.
    /// </summary>
    [Fact(DisplayName = "Appointment reminder servico | Agendar for appointment | Deve avoid duplicate por event key")]
    public async Task ScheduleForAppointmentAsync_ShouldAvoidDuplicateByEventKey()
    {
        var appointment = BuildConfirmedAppointment();
        var existingKey = $"{appointment.Id:N}:{appointment.WindowStartUtc:yyyyMMddHHmm}:{appointment.ClientId:N}:{AppointmentReminderChannel.InApp}:{1440}:reminder";
        var existing = new List<AppointmentReminderDispatch>
        {
            new()
            {
                ServiceAppointmentId = appointment.Id,
                RecipientUserId = appointment.ClientId,
                Channel = AppointmentReminderChannel.InApp,
                ReminderOffsetMinutes = 1440,
                EventKey = existingKey,
                Status = AppointmentReminderDispatchStatus.Pending,
                ScheduledForUtc = appointment.WindowStartUtc.AddMinutes(-1440),
                NextAttemptAtUtc = appointment.WindowStartUtc.AddMinutes(-1440),
                Subject = "S",
                Message = "M"
            }
        };

        var appointmentRepository = new Mock<IServiceAppointmentRepository>();
        appointmentRepository
            .Setup(r => r.GetByIdAsync(appointment.Id))
            .ReturnsAsync(appointment);

        var reminderRepository = new Mock<IAppointmentReminderDispatchRepository>();
        reminderRepository
            .Setup(r => r.CancelPendingByAppointmentAsync(appointment.Id, It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync(1);
        reminderRepository
            .Setup(r => r.GetByAppointmentIdAsync(appointment.Id))
            .ReturnsAsync(existing);

        IReadOnlyCollection<AppointmentReminderDispatch>? added = null;
        reminderRepository
            .Setup(r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<AppointmentReminderDispatch>>()))
            .Callback<IReadOnlyCollection<AppointmentReminderDispatch>>(rows => added = rows)
            .Returns(Task.CompletedTask);

        var service = BuildService(
            appointmentRepository.Object,
            reminderRepository.Object,
            Mock.Of<INotificationService>(),
            Mock.Of<IEmailService>());

        await service.ScheduleForAppointmentAsync(appointment.Id, "confirmado");

        Assert.NotNull(added);
        Assert.Equal(11, added!.Count);
        Assert.DoesNotContain(added, r => r.EventKey == existingKey);
    }

    /// <summary>
    /// Cenario: fluxo de confirmacao de presenca deve gerar lembrete dedicado no offset configurado.
    /// Passos: agenda appointment confirmado sem reminders preexistentes e dispara ScheduleForAppointmentAsync.
    /// Resultado esperado: existe dispatch in-app com offset 120, EventKey de presence e ActionUrl com presencePrompt.
    /// </summary>
    [Fact(DisplayName = "Appointment reminder servico | Agendar for appointment | Deve criar presence confirmation reminder for configured offset")]
    public async Task ScheduleForAppointmentAsync_ShouldCreatePresenceConfirmationReminder_ForConfiguredOffset()
    {
        var appointment = BuildConfirmedAppointment();

        var appointmentRepository = new Mock<IServiceAppointmentRepository>();
        appointmentRepository
            .Setup(r => r.GetByIdAsync(appointment.Id))
            .ReturnsAsync(appointment);

        var reminderRepository = new Mock<IAppointmentReminderDispatchRepository>();
        reminderRepository
            .Setup(r => r.CancelPendingByAppointmentAsync(appointment.Id, It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync(0);
        reminderRepository
            .Setup(r => r.GetByAppointmentIdAsync(appointment.Id))
            .ReturnsAsync(new List<AppointmentReminderDispatch>());

        IReadOnlyCollection<AppointmentReminderDispatch>? added = null;
        reminderRepository
            .Setup(r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<AppointmentReminderDispatch>>()))
            .Callback<IReadOnlyCollection<AppointmentReminderDispatch>>(rows => added = rows)
            .Returns(Task.CompletedTask);

        var service = BuildService(
            appointmentRepository.Object,
            reminderRepository.Object,
            Mock.Of<INotificationService>(),
            Mock.Of<IEmailService>());

        await service.ScheduleForAppointmentAsync(appointment.Id, "confirmado");

        Assert.NotNull(added);
        var confirmationReminder = added!
            .FirstOrDefault(r => r.RecipientUserId == appointment.ClientId &&
                                 r.Channel == AppointmentReminderChannel.InApp &&
                                 r.ReminderOffsetMinutes == 120);

        Assert.NotNull(confirmationReminder);
        Assert.Contains(":presence", confirmationReminder!.EventKey, StringComparison.Ordinal);
        Assert.Contains("presencePrompt=1", confirmationReminder.ActionUrl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Confirmacao de presenca", confirmationReminder.Subject, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Cenario: envio de reminder por e-mail falha repetidamente ate atingir limite maximo de tentativas.
    /// Passos: cria dispatch devido com AttemptCount proximo do maximo e simula falha do provedor SMTP.
    /// Resultado esperado: status final vira FailedPermanent e contador de tentativas e atualizado para o teto.
    /// </summary>
    [Fact(DisplayName = "Appointment reminder servico | Process due reminders | Deve marcar falha permanent quando max attempts reached")]
    public async Task ProcessDueRemindersAsync_ShouldMarkFailedPermanent_WhenMaxAttemptsReached()
    {
        var appointment = BuildConfirmedAppointment();
        var reminder = new AppointmentReminderDispatch
        {
            Id = Guid.NewGuid(),
            ServiceAppointmentId = appointment.Id,
            ServiceAppointment = appointment,
            RecipientUserId = appointment.ClientId,
            RecipientUser = appointment.Client,
            Channel = AppointmentReminderChannel.Email,
            Status = AppointmentReminderDispatchStatus.Pending,
            ReminderOffsetMinutes = 30,
            ScheduledForUtc = DateTime.UtcNow.AddMinutes(-30),
            NextAttemptAtUtc = DateTime.UtcNow.AddMinutes(-1),
            AttemptCount = 2,
            MaxAttempts = 3,
            EventKey = "k1",
            Subject = "Lembrete",
            Message = "Mensagem"
        };

        var reminderRepository = new Mock<IAppointmentReminderDispatchRepository>();
        reminderRepository
            .Setup(r => r.GetDueAsync(It.IsAny<DateTime>(), It.IsAny<int>()))
            .ReturnsAsync(new List<AppointmentReminderDispatch> { reminder });

        AppointmentReminderDispatch? updated = null;
        reminderRepository
            .Setup(r => r.UpdateAsync(It.IsAny<AppointmentReminderDispatch>()))
            .Callback<AppointmentReminderDispatch>(r => updated = r)
            .Returns(Task.CompletedTask);

        var emailService = new Mock<IEmailService>();
        emailService
            .Setup(s => s.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("smtp indisponivel"));

        var service = BuildService(
            Mock.Of<IServiceAppointmentRepository>(),
            reminderRepository.Object,
            Mock.Of<INotificationService>(),
            emailService.Object);

        var processed = await service.ProcessDueRemindersAsync();

        Assert.Equal(1, processed);
        Assert.NotNull(updated);
        Assert.Equal(AppointmentReminderDispatchStatus.FailedPermanent, updated!.Status);
        Assert.Equal(3, updated.AttemptCount);
    }

    /// <summary>
    /// Cenario: lote de reminders vencidos contem canais diferentes (in-app e e-mail) para destinatarios distintos.
    /// Passos: processa dois dispatches due e monitora chamadas de notificacao e e-mail.
    /// Resultado esperado: ambos canais sao executados, reminders marcam entrega e quantidade processada reflete os dois itens.
    /// </summary>
    [Fact(DisplayName = "Appointment reminder servico | Process due reminders | Deve enviar em app e email quando dispatches due")]
    public async Task ProcessDueRemindersAsync_ShouldSendInAppAndEmail_WhenDispatchesAreDue()
    {
        var appointment = BuildConfirmedAppointment();
        var inAppReminder = new AppointmentReminderDispatch
        {
            Id = Guid.NewGuid(),
            ServiceAppointmentId = appointment.Id,
            ServiceAppointment = appointment,
            RecipientUserId = appointment.ClientId,
            RecipientUser = appointment.Client,
            Channel = AppointmentReminderChannel.InApp,
            Status = AppointmentReminderDispatchStatus.Pending,
            ReminderOffsetMinutes = 120,
            ScheduledForUtc = DateTime.UtcNow.AddMinutes(-120),
            NextAttemptAtUtc = DateTime.UtcNow.AddMinutes(-1),
            AttemptCount = 0,
            MaxAttempts = 3,
            EventKey = "inapp",
            Subject = "Lembrete",
            Message = "Mensagem"
        };

        var emailReminder = new AppointmentReminderDispatch
        {
            Id = Guid.NewGuid(),
            ServiceAppointmentId = appointment.Id,
            ServiceAppointment = appointment,
            RecipientUserId = appointment.ProviderId,
            RecipientUser = appointment.Provider,
            Channel = AppointmentReminderChannel.Email,
            Status = AppointmentReminderDispatchStatus.Pending,
            ReminderOffsetMinutes = 30,
            ScheduledForUtc = DateTime.UtcNow.AddMinutes(-30),
            NextAttemptAtUtc = DateTime.UtcNow.AddMinutes(-1),
            AttemptCount = 0,
            MaxAttempts = 3,
            EventKey = "email",
            Subject = "Lembrete",
            Message = "Mensagem"
        };

        var reminderRepository = new Mock<IAppointmentReminderDispatchRepository>();
        reminderRepository
            .Setup(r => r.GetDueAsync(It.IsAny<DateTime>(), It.IsAny<int>()))
            .ReturnsAsync(new List<AppointmentReminderDispatch> { inAppReminder, emailReminder });
        reminderRepository
            .Setup(r => r.UpdateAsync(It.IsAny<AppointmentReminderDispatch>()))
            .Returns(Task.CompletedTask);

        var notificationService = new Mock<INotificationService>();
        var emailService = new Mock<IEmailService>();

        var service = BuildService(
            Mock.Of<IServiceAppointmentRepository>(),
            reminderRepository.Object,
            notificationService.Object,
            emailService.Object);

        var processed = await service.ProcessDueRemindersAsync();

        Assert.Equal(2, processed);
        notificationService.Verify(s => s.SendNotificationAsync(
            appointment.ClientId.ToString("N"),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>()), Times.Once);
        emailService.Verify(s => s.SendEmailAsync(
            appointment.Provider.Email,
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);
        Assert.NotNull(inAppReminder.DeliveredAtUtc);
        Assert.NotNull(emailReminder.DeliveredAtUtc);
    }

    /// <summary>
    /// Cenario: confirmacao de presenca recebida deve ser registrada como telemetria no repositorio de reminders.
    /// Passos: chama RegisterPresenceResponseTelemetryAsync com appointment/destinatario e resposta positiva.
    /// Resultado esperado: servico delega corretamente ao repositorio e retorna a quantidade atualizada.
    /// </summary>
    [Fact(DisplayName = "Appointment reminder servico | Register presence resposta telemetry | Deve delegate para repository")]
    public async Task RegisterPresenceResponseTelemetryAsync_ShouldDelegateToRepository()
    {
        var appointmentId = Guid.NewGuid();
        var recipientUserId = Guid.NewGuid();
        var reminderRepository = new Mock<IAppointmentReminderDispatchRepository>();
        reminderRepository
            .Setup(r => r.RegisterPresenceResponseAsync(
                appointmentId,
                recipientUserId,
                true,
                "Confirmado",
                It.IsAny<DateTime>()))
            .ReturnsAsync(2);

        var service = BuildService(
            Mock.Of<IServiceAppointmentRepository>(),
            reminderRepository.Object,
            Mock.Of<INotificationService>(),
            Mock.Of<IEmailService>());

        var registered = await service.RegisterPresenceResponseTelemetryAsync(
            appointmentId,
            recipientUserId,
            true,
            "Confirmado",
            DateTime.UtcNow);

        Assert.Equal(2, registered);
    }

    private static AppointmentReminderService BuildService(
        IServiceAppointmentRepository appointmentRepository,
        IAppointmentReminderDispatchRepository reminderRepository,
        INotificationService notificationService,
        IEmailService emailService)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServiceAppointments:Reminders:MaxAttempts"] = "3",
                ["ServiceAppointments:Reminders:RetryBaseDelaySeconds"] = "30",
                ["ServiceAppointments:Reminders:OffsetsMinutes:0"] = "1440",
                ["ServiceAppointments:Reminders:OffsetsMinutes:1"] = "120",
                ["ServiceAppointments:Reminders:OffsetsMinutes:2"] = "30"
            })
            .Build();

        return new AppointmentReminderService(
            appointmentRepository,
            reminderRepository,
            BuildPreferenceRepository().Object,
            notificationService,
            emailService,
            configuration,
            Mock.Of<ILogger<AppointmentReminderService>>());
    }

    private static Mock<IAppointmentReminderPreferenceRepository> BuildPreferenceRepository()
    {
        var preferenceRepository = new Mock<IAppointmentReminderPreferenceRepository>();
        preferenceRepository
            .Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(Array.Empty<AppointmentReminderPreference>());
        return preferenceRepository;
    }

    private static ServiceAppointment BuildConfirmedAppointment()
    {
        var client = new User
        {
            Id = Guid.NewGuid(),
            Name = "Cliente Teste",
            Email = "cliente.teste@local",
            PasswordHash = "hash",
            Phone = "11999999999",
            Role = UserRole.Client
        };

        var provider = new User
        {
            Id = Guid.NewGuid(),
            Name = "Prestador Teste",
            Email = "prestador.teste@local",
            PasswordHash = "hash",
            Phone = "11988888888",
            Role = UserRole.Provider
        };

        return new ServiceAppointment
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = Guid.NewGuid(),
            ClientId = client.Id,
            Client = client,
            ProviderId = provider.Id,
            Provider = provider,
            WindowStartUtc = DateTime.UtcNow.AddHours(12),
            WindowEndUtc = DateTime.UtcNow.AddHours(13),
            Status = ServiceAppointmentStatus.Confirmed
        };
    }
}
