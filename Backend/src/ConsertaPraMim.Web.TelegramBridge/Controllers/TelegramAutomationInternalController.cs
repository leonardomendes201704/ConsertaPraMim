using ConsertaPraMim.Web.TelegramBridge.Models;
using ConsertaPraMim.Web.TelegramBridge.Options;
using ConsertaPraMim.Web.TelegramBridge.Security;
using ConsertaPraMim.Web.TelegramBridge.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Web.TelegramBridge.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[IgnoreAntiforgeryToken]
[Route("api/internal/telegram/messages")]
public sealed class TelegramAutomationInternalController : ControllerBase
{
    private const string SharedSecretHeaderName = "X-Telegram-Automation-Key";

    private readonly ITelegramChatService _telegramChatService;
    private readonly ITelegramHumanHandoffStateService _handoffStateService;
    private readonly TelegramAutomationOptions _options;

    public TelegramAutomationInternalController(
        ITelegramChatService telegramChatService,
        ITelegramHumanHandoffStateService handoffStateService,
        IOptions<TelegramAutomationOptions> options)
    {
        _telegramChatService = telegramChatService;
        _handoffStateService = handoffStateService;
        _options = options.Value;
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendHumanReply(
        [FromBody] TelegramBridgeHumanReplyRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsSecretValid(Request.Headers[SharedSecretHeaderName].ToString()))
        {
            return Unauthorized(new
            {
                success = false,
                message = "Chave de automacao Telegram invalida."
            });
        }

        if (request.TelegramChatId <= 0)
        {
            return BadRequest(new
            {
                success = false,
                message = "TelegramChatId invalido para entrega humana."
            });
        }

        if (string.IsNullOrWhiteSpace(request.MessageText))
        {
            return BadRequest(new
            {
                success = false,
                message = "Mensagem humana vazia nao pode ser enviada ao Telegram."
            });
        }

        if (request.ActivateHumanHandoff)
        {
            _handoffStateService.Activate(request.TelegramChatId, DateTime.UtcNow);
        }

        await _telegramChatService.SendFromPanelAsync(
            request.TelegramChatId,
            request.MessageText,
            [],
            cancellationToken);

        return Ok(new
        {
            success = true,
            telegramChatIdMasked = TelegramSecuritySanitizer.MaskChatId(request.TelegramChatId),
            humanHandoffActivated = request.ActivateHumanHandoff,
            message = "Mensagem humana enviada ao Telegram com sucesso."
        });
    }

    [HttpPost("handoff/reset")]
    public IActionResult ResetHumanHandoff([FromBody] TelegramBridgeResetHandoffRequest request)
    {
        if (!IsSecretValid(Request.Headers[SharedSecretHeaderName].ToString()))
        {
            return Unauthorized(new
            {
                success = false,
                message = "Chave de automacao Telegram invalida."
            });
        }

        if (request.TelegramChatId <= 0)
        {
            return BadRequest(new
            {
                success = false,
                message = "TelegramChatId invalido para reset de handoff."
            });
        }

        var wasActive = _handoffStateService.Deactivate(request.TelegramChatId);

        return Ok(new TelegramBridgeResetHandoffResponse
        {
            Success = true,
            TelegramChatId = request.TelegramChatId,
            HandoffWasActive = wasActive,
            Message = wasActive
                ? "Handoff humano do chat Telegram foi resetado com sucesso."
                : "Nao havia handoff humano ativo para esse chat Telegram."
        });
    }

    private bool IsSecretValid(string providedSecret) =>
        !string.IsNullOrWhiteSpace(providedSecret) &&
        string.Equals(providedSecret.Trim(), _options.SharedSecret.Trim(), StringComparison.Ordinal);
}
