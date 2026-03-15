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
    private const string ActiveStatus = "active";
    private const string BotResumedStatus = "bot_resumed";
    private const string DefaultActivationReasonCode = "human_handoff";
    private const string DefaultActivationReasonLabel = "Handoff humano ativo";
    private const string DefaultActivationSource = "telegram_bridge";
    private const string DefaultResumeReasonCode = "bot_resumed";
    private const string DefaultResumeReasonLabel = "Bot retomado";
    private const string DefaultResumeSource = "telegram_bridge";

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
            _handoffStateService.Activate(
                request.TelegramChatId,
                request.HandoffActivatedAtUtc ?? DateTime.UtcNow,
                request.HandoffReasonCode,
                request.HandoffReasonLabel,
                request.HandoffSource);
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

    [HttpPost("handoff/activate")]
    public IActionResult ActivateHumanHandoff([FromBody] TelegramBridgeSetHandoffRequest request)
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
                message = "TelegramChatId invalido para ativacao de handoff."
            });
        }

        var state = _handoffStateService.Activate(
            request.TelegramChatId,
            request.OccurredAtUtc ?? DateTime.UtcNow,
            request.ReasonCode,
            request.ReasonLabel,
            request.Source);

        return Ok(ToSetHandoffResponse(
            state,
            "Handoff humano do chat Telegram ativado com sucesso."));
    }

    [HttpPost("handoff/resume")]
    public IActionResult ResumeBot([FromBody] TelegramBridgeSetHandoffRequest request)
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
                message = "TelegramChatId invalido para retomada do bot."
            });
        }

        var state = _handoffStateService.ResumeBot(
            request.TelegramChatId,
            request.OccurredAtUtc ?? DateTime.UtcNow,
            request.ReasonCode,
            request.ReasonLabel,
            request.Source);

        return Ok(ToSetHandoffResponse(
            state,
            "Bot do chat Telegram retomado com sucesso."));
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

    private static TelegramBridgeSetHandoffResponse ToSetHandoffResponse(
        TelegramHumanHandoffState state,
        string message) =>
        new()
        {
            Success = true,
            Message = message,
            TelegramChatId = state.TelegramChatId,
            IsActive = state.IsActive,
            HandoffStatus = NormalizeStatus(state.Status),
            ReasonCode = string.IsNullOrWhiteSpace(state.ReasonCode)
                ? (state.IsActive ? DefaultActivationReasonCode : DefaultResumeReasonCode)
                : state.ReasonCode,
            ReasonLabel = string.IsNullOrWhiteSpace(state.ReasonLabel)
                ? (state.IsActive ? DefaultActivationReasonLabel : DefaultResumeReasonLabel)
                : state.ReasonLabel,
            StartedAtUtc = state.StartedAtUtc,
            UpdatedAtUtc = state.UpdatedAtUtc
        };

    private static string NormalizeStatus(string? status) =>
        string.IsNullOrWhiteSpace(status)
            ? ActiveStatus
            : status.Trim().ToLowerInvariant() switch
            {
                ActiveStatus => ActiveStatus,
                BotResumedStatus => BotResumedStatus,
                _ => status.Trim().ToLowerInvariant()
            };
}
