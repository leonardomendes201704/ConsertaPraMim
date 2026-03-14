using Microsoft.Extensions.Options;
using BridgeTelegramOptions = ConsertaPraMim.Web.TelegramBridge.Options.TelegramAutomationOptions;
using BridgeTelegramOptionsValidator = ConsertaPraMim.Web.TelegramBridge.Options.TelegramAutomationOptionsValidator;
using CpmFullTelegramOptions = AppMobileCPM.Integrations.Telegram.TelegramAutomationOptions;
using CpmFullTelegramOptionsValidator = AppMobileCPM.Integrations.Telegram.TelegramAutomationOptionsValidator;

namespace ConsertaPraMim.Tests.Unit.Integrations.Telegram;

public sealed class TelegramAutomationOptionsValidatorTests
{
    [Fact(DisplayName = "Telegram Automation Options | Bridge | Deve aceitar modo desligado sem URL/segredo")]
    public void BridgeValidator_DeveAceitarModoDesligado()
    {
        var validator = new BridgeTelegramOptionsValidator();
        var result = validator.Validate(Options.DefaultName, new BridgeTelegramOptions
        {
            Enabled = false
        });

        Assert.True(result.Succeeded);
    }

    [Fact(DisplayName = "Telegram Automation Options | Bridge | Deve falhar sem URL e segredo quando habilitado")]
    public void BridgeValidator_DeveFalharSemCamposObrigatorios()
    {
        var validator = new BridgeTelegramOptionsValidator();
        var result = validator.Validate(Options.DefaultName, new BridgeTelegramOptions
        {
            Enabled = true,
            ClientsAutomationEnabled = true,
            RequestTimeoutSeconds = 0
        });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains("CpmFullBaseUrl", StringComparison.Ordinal));
        Assert.Contains(result.Failures!, failure => failure.Contains("SharedSecret", StringComparison.Ordinal));
        Assert.Contains(result.Failures!, failure => failure.Contains("RequestTimeoutSeconds", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Telegram Automation Options | CPM Full | Deve falhar sem segredo quando habilitado")]
    public void CpmFullValidator_DeveFalharSemSegredo()
    {
        var validator = new CpmFullTelegramOptionsValidator();
        var result = validator.Validate(Options.DefaultName, new CpmFullTelegramOptions
        {
            Enabled = true,
            ClientsAutomationEnabled = true,
            SharedSecret = ""
        });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains("SharedSecret", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Telegram Automation Options | CPM Full | Deve aceitar configuracao valida")]
    public void CpmFullValidator_DeveAceitarConfiguracaoValida()
    {
        var validator = new CpmFullTelegramOptionsValidator();
        var result = validator.Validate(Options.DefaultName, new CpmFullTelegramOptions
        {
            Enabled = true,
            ClientsAutomationEnabled = true,
            SharedSecret = "segredo-compartilhado"
        });

        Assert.True(result.Succeeded);
    }
}
