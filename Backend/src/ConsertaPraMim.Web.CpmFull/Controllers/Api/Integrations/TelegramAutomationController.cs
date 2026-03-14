using AppMobileCPM.Integrations.Telegram;
using Microsoft.AspNetCore.Mvc;

namespace AppMobileCPM.Controllers.Api.Integrations;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[IgnoreAntiforgeryToken]
[Route("api/integrations/telegram/automation")]
public sealed class TelegramAutomationController : ControllerBase
{
    private readonly ITelegramLeadAutomationService _telegramLeadAutomationService;

    public TelegramAutomationController(ITelegramLeadAutomationService telegramLeadAutomationService)
    {
        _telegramLeadAutomationService = telegramLeadAutomationService;
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
}
