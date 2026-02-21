using ConsertaPraMim.API.BackgroundJobs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using ConsertaPraMim.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConsertaPraMim.Tests.Unit.Integration.BackgroundJobs;

public class ServiceAppointmentReminderWorkerIntegrationTests
{
    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Servico appointment reminder worker integracao | Run once | Deve process due em app reminder e persistir sent telemetry.
    /// </summary>
    [Fact(DisplayName = "Servico appointment reminder worker integracao | Run once | Deve process due em app reminder e persistir sent telemetry")]
    public async Task RunOnceAsync_ShouldProcessDueInAppReminder_AndPersistSentTelemetry()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var notificationService = new RecordingNotificationService();
        await using var provider = BuildServiceProvider(connection, notificationService, new NoOpEmailService());
        await EnsureDatabaseCreatedAsync(provider);

        var reminderId = await SeedDueReminderAsync(
            provider,
            AppointmentReminderChannel.InApp,
            status: AppointmentReminderDispatchStatus.Pending,
            maxAttempts: 3);

        var worker = new ServiceAppointmentReminderWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IConfiguration>(),
            NullLogger<ServiceAppointmentReminderWorker>.Instance);

        var processed = await worker.RunOnceAsync();

        using var assertScope = provider.CreateScope();
        var context = assertScope.ServiceProvider.GetRequiredService<ConsertaPraMimDbContext>();
        var reminder = await context.AppointmentReminderDispatches.SingleAsync(r => r.Id == reminderId);

        Assert.Equal(1, processed);
        Assert.Equal(AppointmentReminderDispatchStatus.Sent, reminder.Status);
        Assert.Equal(1, reminder.AttemptCount);
        Assert.NotNull(reminder.LastAttemptAtUtc);
        Assert.NotNull(reminder.SentAtUtc);
        Assert.NotNull(reminder.DeliveredAtUtc);
        Assert.Single(notificationService.Messages);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Servico appointment reminder worker integracao | Run once | Deve persistir retry state quando email enviar falha.
    /// </summary>
    [Fact(DisplayName = "Servico appointment reminder worker integracao | Run once | Deve persistir retry state quando email enviar falha")]
    public async Task RunOnceAsync_ShouldPersistRetryState_WhenEmailSendFails()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var provider = BuildServiceProvider(
            connection,
            new RecordingNotificationService(),
            new ThrowingEmailService("smtp indisponivel"));
        await EnsureDatabaseCreatedAsync(provider);

        var reminderId = await SeedDueReminderAsync(
            provider,
            AppointmentReminderChannel.Email,
            status: AppointmentReminderDispatchStatus.Pending,
            maxAttempts: 3);

        var worker = new ServiceAppointmentReminderWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IConfiguration>(),
            NullLogger<ServiceAppointmentReminderWorker>.Instance);

        var beforeRunUtc = DateTime.UtcNow;
        var processed = await worker.RunOnceAsync();

        using var assertScope = provider.CreateScope();
        var context = assertScope.ServiceProvider.GetRequiredService<ConsertaPraMimDbContext>();
        var reminder = await context.AppointmentReminderDispatches.SingleAsync(r => r.Id == reminderId);

        Assert.Equal(1, processed);
        Assert.Equal(AppointmentReminderDispatchStatus.FailedRetryable, reminder.Status);
        Assert.Equal(1, reminder.AttemptCount);
        Assert.NotNull(reminder.LastAttemptAtUtc);
        Assert.True(reminder.NextAttemptAtUtc > beforeRunUtc);
        Assert.Null(reminder.SentAtUtc);
        Assert.Null(reminder.DeliveredAtUtc);
        Assert.Contains("smtp indisponivel", reminder.LastError, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task EnsureDatabaseCreatedAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ConsertaPraMimDbContext>();
        await context.Database.EnsureCreatedAsync();
    }

    private static async Task<Guid> SeedDueReminderAsync(
        IServiceProvider provider,
        AppointmentReminderChannel channel,
        AppointmentReminderDispatchStatus status,
        int maxAttempts)
    {
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ConsertaPraMimDbContext>();

        var client = new User
        {
            Name = "Cliente Reminder Worker",
            Email = "cliente.worker@teste.com",
            PasswordHash = "hash",
            Phone = "11999999999",
            Role = UserRole.Client
        };

        var providerUser = new User
        {
            Name = "Prestador Reminder Worker",
            Email = "prestador.worker@teste.com",
            PasswordHash = "hash",
            Phone = "11999999998",
            Role = UserRole.Provider
        };

        var request = new ServiceRequest
        {
            ClientId = client.Id,
            Category = ServiceCategory.Electrical,
            Description = "Troca de disjuntor",
            AddressStreet = "Rua Teste 100",
            AddressCity = "Santos",
            AddressZip = "11000-000",
            Latitude = -23.9608,
            Longitude = -46.3336,
            Status = ServiceRequestStatus.Scheduled
        };

        var appointment = new ServiceAppointment
        {
            ServiceRequestId = request.Id,
            ClientId = client.Id,
            ProviderId = providerUser.Id,
            WindowStartUtc = DateTime.UtcNow.AddHours(2),
            WindowEndUtc = DateTime.UtcNow.AddHours(3),
            Status = ServiceAppointmentStatus.Confirmed
        };

        var reminder = new AppointmentReminderDispatch
        {
            ServiceAppointmentId = appointment.Id,
            RecipientUserId = channel == AppointmentReminderChannel.Email ? providerUser.Id : client.Id,
            Channel = channel,
            Status = status,
            ReminderOffsetMinutes = 120,
            ScheduledForUtc = DateTime.UtcNow.AddMinutes(-5),
            NextAttemptAtUtc = DateTime.UtcNow.AddMinutes(-1),
            AttemptCount = 0,
            MaxAttempts = maxAttempts,
            EventKey = $"{appointment.Id:N}:{channel}:integration",
            Subject = "Lembrete de agendamento",
            Message = "Teste de processamento do worker",
            ActionUrl = $"/ServiceRequests/Details/{request.Id}"
        };

        context.Users.AddRange(client, providerUser);
        context.ServiceRequests.Add(request);
        context.ServiceAppointments.Add(appointment);
        context.AppointmentReminderDispatches.Add(reminder);
        await context.SaveChangesAsync();

        return reminder.Id;
    }

    private static ServiceProvider BuildServiceProvider(
        SqliteConnection connection,
        INotificationService notificationService,
        IEmailService emailService)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServiceAppointments:Reminders:EnableWorker"] = "true",
                ["ServiceAppointments:Reminders:WorkerIntervalSeconds"] = "5",
                ["ServiceAppointments:Reminders:BatchSize"] = "200",
                ["ServiceAppointments:Reminders:MaxAttempts"] = "3",
                ["ServiceAppointments:Reminders:RetryBaseDelaySeconds"] = "30",
                ["ServiceAppointments:Reminders:OffsetsMinutes:0"] = "1440",
                ["ServiceAppointments:Reminders:OffsetsMinutes:1"] = "120",
                ["ServiceAppointments:Reminders:OffsetsMinutes:2"] = "30",
                ["ServiceAppointments:Reminders:PresenceConfirmationOffsetsMinutes:0"] = "120"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddDbContext<ConsertaPraMimDbContext>(options => options.UseSqlite(connection));
        services.AddScoped<IServiceAppointmentRepository, ServiceAppointmentRepository>();
        services.AddScoped<IAppointmentReminderDispatchRepository, AppointmentReminderDispatchRepository>();
        services.AddScoped<IAppointmentReminderPreferenceRepository, AppointmentReminderPreferenceRepository>();
        services.AddScoped<IAppointmentReminderService, AppointmentReminderService>();
        services.AddSingleton(notificationService);
        services.AddSingleton(emailService);

        return services.BuildServiceProvider();
    }

    private sealed class RecordingNotificationService : INotificationService
    {
        public List<(string Recipient, string Subject, string Message, string? ActionUrl)> Messages { get; } = new();

        public Task SendNotificationAsync(string recipient, string subject, string message, string? actionUrl = null)
        {
            Messages.Add((recipient, subject, message, actionUrl));
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpEmailService : IEmailService
    {
        public Task SendEmailAsync(string to, string subject, string body)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingEmailService(string errorMessage) : IEmailService
    {
        public Task SendEmailAsync(string to, string subject, string body)
        {
            throw new InvalidOperationException(errorMessage);
        }
    }
}
