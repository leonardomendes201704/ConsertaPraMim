using ConsertaPraMim.Web.TelegramBridge.Options;
using ConsertaPraMim.Web.TelegramBridge.Security;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public sealed class TelegramUpdateTransportBootstrapService : IHostedService
{
    private readonly ITelegramBotApiClient _telegramBotApiClient;
    private readonly TelegramBridgeOptions _options;
    private readonly ILogger<TelegramUpdateTransportBootstrapService> _logger;

    public TelegramUpdateTransportBootstrapService(
        ITelegramBotApiClient telegramBotApiClient,
        IOptions<TelegramBridgeOptions> options,
        ILogger<TelegramUpdateTransportBootstrapService> logger)
    {
        _telegramBotApiClient = telegramBotApiClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_telegramBotApiClient.IsConfigured)
        {
            _logger.LogInformation("Bootstrap do transporte Telegram ignorado porque o bot token nao esta configurado.");
            return;
        }

        try
        {
            if (_options.UsesWebhookTransport())
            {
                var webhookUrl = _options.BuildWebhookUrl();
                await _telegramBotApiClient.SetWebhookAsync(
                    webhookUrl,
                    _options.WebhookSecretToken,
                    _options.WebhookDropPendingUpdates,
                    cancellationToken);

                var webhookInfo = await _telegramBotApiClient.GetWebhookInfoAsync(cancellationToken);
                _logger.LogInformation(
                    "Webhook Telegram configurado em {WebhookUrl}. PendingUpdates={PendingUpdates}. LastError={LastError}",
                    webhookUrl,
                    webhookInfo?.PendingUpdateCount ?? 0,
                    string.IsNullOrWhiteSpace(webhookInfo?.LastErrorMessage)
                        ? "nenhum"
                        : TelegramSecuritySanitizer.SanitizeMessage(webhookInfo.LastErrorMessage));
                return;
            }

            await _telegramBotApiClient.DeleteWebhookAsync(dropPendingUpdates: false, cancellationToken);
            _logger.LogInformation("Webhook Telegram removido para operar em modo long polling.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao inicializar o transporte Telegram configurado.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
