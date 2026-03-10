using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Infrastructure.Configuration;
using ConsertaPraMim.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Tests.Unit.Services;

public class GoogleCalendarServiceTests
{
    [Fact]
    public async Task CreateEventAsync_ShouldReturnDisabled_WhenIntegrationIsOff()
    {
        var service = CreateService(new GoogleCalendarSyncOptions
        {
            Enabled = false
        });

        var result = await service.CreateEventAsync(
            new GoogleCalendarUpsertRequest(
                Title: "Visita tecnica",
                StartsAtUtc: DateTime.UtcNow.AddHours(1),
                EndsAtUtc: DateTime.UtcNow.AddHours(2)));

        Assert.False(result.Success);
        Assert.Equal("google_calendar_disabled", result.ErrorCode);
    }

    [Fact]
    public async Task CreateEventAsync_ShouldValidateWindowBeforeCallingGoogle()
    {
        var service = CreateService(new GoogleCalendarSyncOptions
        {
            Enabled = true,
            ProjectId = "consertapramim-dev",
            ServiceAccountEmail = "agenda@consertapramim.iam.gserviceaccount.com",
            PrivateKey = "-----BEGIN PRIVATE KEY-----\\nabc\\n-----END PRIVATE KEY-----\\n",
            CalendarId = "consertapramim-dev@group.calendar.google.com",
            Timezone = "America/Sao_Paulo"
        });

        var now = DateTime.UtcNow;
        var result = await service.CreateEventAsync(
            new GoogleCalendarUpsertRequest(
                Title: "Visita tecnica",
                StartsAtUtc: now.AddHours(2),
                EndsAtUtc: now.AddHours(1)));

        Assert.False(result.Success);
        Assert.Equal("google_calendar_invalid_window", result.ErrorCode);
    }

    [Fact]
    public async Task CreateEventAsync_ShouldValidateIdempotencyKey_WhenProvided()
    {
        var service = CreateService(new GoogleCalendarSyncOptions
        {
            Enabled = true,
            ProjectId = "consertapramim-dev",
            ServiceAccountEmail = "agenda@consertapramim.iam.gserviceaccount.com",
            PrivateKey = "-----BEGIN PRIVATE KEY-----\\nabc\\n-----END PRIVATE KEY-----\\n",
            CalendarId = "consertapramim-dev@group.calendar.google.com",
            Timezone = "America/Sao_Paulo"
        });

        var result = await service.CreateEventAsync(
            new GoogleCalendarUpsertRequest(
                Title: "Visita tecnica",
                StartsAtUtc: DateTime.UtcNow.AddHours(1),
                EndsAtUtc: DateTime.UtcNow.AddHours(2),
                IdempotencyKey: "ID_INVALIDO_MAIUSCULO"));

        Assert.False(result.Success);
        Assert.Equal("google_calendar_invalid_idempotency_key", result.ErrorCode);
    }

    private static GoogleCalendarService CreateService(GoogleCalendarSyncOptions options)
    {
        return new GoogleCalendarService(
            Options.Create(options),
            NullLogger<GoogleCalendarService>.Instance);
    }
}
