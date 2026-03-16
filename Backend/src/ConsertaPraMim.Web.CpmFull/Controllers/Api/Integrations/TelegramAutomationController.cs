using AppMobileCPM.Integrations.Journey;
using AppMobileCPM.Integrations.Telegram;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AppMobileCPM.Controllers.Api.Integrations;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[IgnoreAntiforgeryToken]
[Route("api/integrations/telegram/automation")]
public sealed class TelegramAutomationController : ControllerBase
{
    private readonly ITelegramLeadAutomationService _telegramLeadAutomationService;
    private readonly ITelegramMessageAutomationService _telegramMessageAutomationService;
    private readonly IJourneySchedulingService _journeySchedulingService;
    private readonly TelegramAutomationOptions _telegramAutomationOptions;

    public TelegramAutomationController(
        ITelegramLeadAutomationService telegramLeadAutomationService,
        ITelegramMessageAutomationService telegramMessageAutomationService,
        IJourneySchedulingService journeySchedulingService,
        IOptions<TelegramAutomationOptions> telegramAutomationOptions)
    {
        _telegramLeadAutomationService = telegramLeadAutomationService;
        _telegramMessageAutomationService = telegramMessageAutomationService;
        _journeySchedulingService = journeySchedulingService;
        _telegramAutomationOptions = telegramAutomationOptions.Value;
    }

    [HttpPost("lead")]
    public async Task<IActionResult> UpsertLead(
        [FromBody] TelegramLeadAutomationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _telegramLeadAutomationService.UpsertLeadAsync(
            request,
            Request.Headers[TelegramLeadAutomationService.SharedSecretHeaderName].ToString(),
            cancellationToken);

        if (!result.Success)
        {
            return StatusCode(result.HttpStatusCode, new
            {
                success = false,
                message = result.Message
            });
        }

        return StatusCode(result.HttpStatusCode, new
        {
            success = true,
            leadId = result.Payload!.LeadId,
            created = result.Payload.Created,
            boardType = result.Payload.BoardType,
            message = result.Payload.Message,
            hasPhone = result.Payload.HasPhone,
            hasEmail = result.Payload.HasEmail,
            hasCity = result.Payload.HasCity,
            hasServiceCategory = result.Payload.HasServiceCategory,
            qualificationStatus = result.Payload.QualificationStatus,
            confirmationPrompt = result.Payload.ConfirmationPrompt,
            missingRequiredFields = result.Payload.MissingRequiredFields,
            chatwoot = new
            {
                status = result.Payload.ChatwootStatus,
                message = result.Payload.ChatwootMessage,
                contactId = result.Payload.ChatwootContactId,
                conversationId = result.Payload.ChatwootConversationId,
                inboxId = result.Payload.ChatwootInboxId
            }
        });
    }

    [HttpPost("message")]
    public async Task<IActionResult> MirrorMessage(
        [FromBody] TelegramInboundMessageAutomationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _telegramMessageAutomationService.EnqueueInboundMessageAsync(
            request,
            Request.Headers[TelegramLeadAutomationService.SharedSecretHeaderName].ToString(),
            cancellationToken);

        if (!result.Success)
        {
            return StatusCode(result.HttpStatusCode, new
            {
                success = false,
                message = result.Message
            });
        }

        return StatusCode(result.HttpStatusCode, new
        {
            success = true,
            leadId = result.Payload!.LeadId,
            queueStatus = result.Payload.QueueStatus,
            duplicate = result.Payload.Duplicate,
            message = result.Payload.Message
        });
    }

    [HttpPost("scheduling/turn")]
    public async Task<IActionResult> ProcessSchedulingTurn(
        [FromBody] JourneySchedulingTurnRequest request,
        CancellationToken cancellationToken)
    {
        var providedSecret = Request.Headers[TelegramLeadAutomationService.SharedSecretHeaderName].ToString();
        if (string.IsNullOrWhiteSpace(providedSecret) ||
            string.IsNullOrWhiteSpace(_telegramAutomationOptions.SharedSecret) ||
            !string.Equals(providedSecret.Trim(), _telegramAutomationOptions.SharedSecret.Trim(), StringComparison.Ordinal))
        {
            return StatusCode(StatusCodes.Status401Unauthorized, new
            {
                success = false,
                handled = false,
                message = "Chave de automacao Telegram invalida."
            });
        }

        var result = await _journeySchedulingService.ProcessTelegramTurnAsync(request, cancellationToken);
        return StatusCode(result.HttpStatusCode, new
        {
            success = result.Success,
            handled = result.Handled,
            leadId = result.LeadId,
            journeyId = result.JourneyId,
            currentState = result.CurrentState,
            schedulingStatus = result.SchedulingStatus,
            message = result.Message,
            replyText = result.ReplyText,
            removeReplyKeyboard = result.RemoveReplyKeyboard,
            googleCalendarEventId = result.GoogleCalendarEventId,
            googleCalendarEventLink = result.GoogleCalendarEventLink,
            scheduledStartAtUtc = result.ScheduledStartAtUtc,
            scheduledEndAtUtc = result.ScheduledEndAtUtc,
            suggestedSlots = result.SuggestedSlots.Select(item => new
            {
                optionNumber = item.OptionNumber,
                startsAtUtc = item.StartsAtUtc,
                endsAtUtc = item.EndsAtUtc,
                label = item.Label
            })
        });
    }
}
