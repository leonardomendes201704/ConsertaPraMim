using System.Security.Cryptography;
using System.Text;
using ConsertaPraMim.Web.TelegramBridge.Models;
using ConsertaPraMim.Web.TelegramBridge.Options;
using ConsertaPraMim.Web.TelegramBridge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Web.TelegramBridge.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[IgnoreAntiforgeryToken]
[Route("api/integrations/telegram/webhook")]
public sealed class TelegramWebhookController : ControllerBase
{
    private const string SecretTokenHeaderName = "X-Telegram-Bot-Api-Secret-Token";

    private readonly ITelegramInboundUpdateProcessor _updateProcessor;
    private readonly TelegramBridgeOptions _options;

    public TelegramWebhookController(
        ITelegramInboundUpdateProcessor updateProcessor,
        IOptions<TelegramBridgeOptions> options)
    {
        _updateProcessor = updateProcessor;
        _options = options.Value;
    }

    [HttpPost]
    public async Task<IActionResult> Receive(
        [FromBody] TelegramUpdate? update,
        CancellationToken cancellationToken)
    {
        if (!_options.UsesWebhookTransport())
        {
            return NotFound(new
            {
                success = false,
                message = "Modo webhook do Telegram desabilitado no ambiente atual."
            });
        }

        if (update is null)
        {
            return BadRequest(new
            {
                success = false,
                message = "Payload do webhook Telegram invalido."
            });
        }

        if (!IsSecretTokenValid(Request.Headers[SecretTokenHeaderName].ToString()))
        {
            return Unauthorized(new
            {
                success = false,
                message = "Secret token do webhook Telegram invalido."
            });
        }

        var processed = await _updateProcessor.ProcessAsync(update, "webhook", cancellationToken);

        return Ok(new
        {
            success = true,
            processed,
            transport = TelegramBridgeOptions.WebhookTransport
        });
    }

    private bool IsSecretTokenValid(string providedSecret)
    {
        var expectedSecret = _options.WebhookSecretToken?.Trim();
        providedSecret = providedSecret?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(expectedSecret) || string.IsNullOrWhiteSpace(providedSecret))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expectedSecret);
        var providedBytes = Encoding.UTF8.GetBytes(providedSecret);

        return expectedBytes.Length == providedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}
