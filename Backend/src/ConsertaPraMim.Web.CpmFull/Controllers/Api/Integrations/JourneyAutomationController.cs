using AppMobileCPM.Integrations.Journey;
using Microsoft.AspNetCore.Mvc;

namespace AppMobileCPM.Controllers.Api.Integrations;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[IgnoreAntiforgeryToken]
[Route("api/integrations/journey/automation")]
public sealed class JourneyAutomationController : ControllerBase
{
    private readonly IJourneyAutomationService _journeyAutomationService;

    public JourneyAutomationController(IJourneyAutomationService journeyAutomationService)
    {
        _journeyAutomationService = journeyAutomationService;
    }

    [HttpPost("intake")]
    public async Task<IActionResult> UpsertJourney(
        [FromBody] JourneyAutomationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _journeyAutomationService.UpsertJourneyAsync(
            request,
            Request.Headers[JourneyAutomationService.SharedSecretHeaderName].ToString(),
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
            journeyId = result.Payload.JourneyId,
            journeyPublicId = result.Payload.JourneyPublicId,
            createdLead = result.Payload.CreatedLead,
            createdJourney = result.Payload.CreatedJourney,
            boardType = result.Payload.BoardType,
            currentState = result.Payload.CurrentState,
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
