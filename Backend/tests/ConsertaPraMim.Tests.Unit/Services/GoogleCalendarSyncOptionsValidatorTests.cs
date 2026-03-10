using ConsertaPraMim.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Tests.Unit.Services;

public class GoogleCalendarSyncOptionsValidatorTests
{
    private readonly GoogleCalendarSyncOptionsValidator _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenDisabledEvenWithoutCredentials()
    {
        var options = new GoogleCalendarSyncOptions
        {
            Enabled = false
        };

        var result = _validator.Validate(Options.DefaultName, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_ShouldFail_WhenEnabledAndMissingRequiredFields()
    {
        var options = new GoogleCalendarSyncOptions
        {
            Enabled = true,
            Timezone = "America/Sao_Paulo"
        };

        var result = _validator.Validate(Options.DefaultName, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains("ProjectId", StringComparison.Ordinal));
        Assert.Contains(result.Failures!, failure => failure.Contains("ServiceAccountEmail", StringComparison.Ordinal));
        Assert.Contains(result.Failures!, failure => failure.Contains("PrivateKey", StringComparison.Ordinal));
        Assert.Contains(result.Failures!, failure => failure.Contains("CalendarId", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenEnabledAndFullyConfigured()
    {
        var options = new GoogleCalendarSyncOptions
        {
            Enabled = true,
            ProjectId = "consertapramim-dev",
            ServiceAccountEmail = "agenda@consertapramim.iam.gserviceaccount.com",
            PrivateKey = "-----BEGIN PRIVATE KEY-----\\nabc\\n-----END PRIVATE KEY-----\\n",
            CalendarId = "consertapramim-dev@group.calendar.google.com",
            Timezone = "America/Sao_Paulo"
        };

        var result = _validator.Validate(Options.DefaultName, options);

        Assert.True(result.Succeeded);
    }
}
