using ConsertaPraMim.Web.TelegramBridge.Options;
using ConsertaPraMim.Web.TelegramBridge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Web.TelegramBridge.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[IgnoreAntiforgeryToken]
[Route("api/internal/telegram/observability")]
public sealed class TelegramObservabilityInternalController : ControllerBase
{
    private const string SharedSecretHeaderName = "X-Telegram-Automation-Key";

    private readonly ITelegramChatbotObservabilityService _observabilityService;
    private readonly TelegramAutomationOptions _options;

    public TelegramObservabilityInternalController(
        ITelegramChatbotObservabilityService observabilityService,
        IOptions<TelegramAutomationOptions> options)
    {
        _observabilityService = observabilityService;
        _options = options.Value;
    }

    [HttpGet("dashboard")]
    public IActionResult GetDashboard()
    {
        if (!IsSecretValid(Request.Headers[SharedSecretHeaderName].ToString()))
        {
            return Unauthorized(new
            {
                success = false,
                message = "Chave de automacao Telegram invalida."
            });
        }

        return Ok(new
        {
            success = true,
            message = "Diagnostico interno do Telegram Bridge carregado.",
            snapshot = _observabilityService.GetSnapshot()
        });
    }

    private bool IsSecretValid(string providedSecret) =>
        !string.IsNullOrWhiteSpace(providedSecret) &&
        string.Equals(providedSecret.Trim(), _options.SharedSecret.Trim(), StringComparison.Ordinal);
}
