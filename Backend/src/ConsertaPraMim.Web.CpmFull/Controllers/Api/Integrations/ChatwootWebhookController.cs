using AppMobileCPM.Integrations.Chatwoot;
using Microsoft.AspNetCore.Mvc;

namespace AppMobileCPM.Controllers.Api.Integrations;

[ApiController]
[IgnoreAntiforgeryToken]
[Route("api/integrations/chatwoot")]
public sealed class ChatwootWebhookController : ControllerBase
{
    private readonly IChatwootWebhookService _chatwootWebhookService;

    public ChatwootWebhookController(IChatwootWebhookService chatwootWebhookService)
    {
        _chatwootWebhookService = chatwootWebhookService;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, cancellationToken);

        var result = await _chatwootWebhookService.HandleAsync(
            new ChatwootWebhookRequest
            {
                RawBody = buffer.ToArray(),
                Signature = Request.Headers["X-Chatwoot-Signature"].ToString(),
                Timestamp = Request.Headers["X-Chatwoot-Timestamp"].ToString(),
                DeliveryId = Request.Headers["X-Chatwoot-Delivery"].ToString()
            },
            cancellationToken);

        return StatusCode(result.HttpStatusCode, new
        {
            success = result.Accepted,
            processStatus = result.ProcessStatus,
            message = result.Message,
            eventType = result.EventType,
            conversationId = result.ConversationId,
            leadId = result.LeadId,
            webhookEventId = result.WebhookEventId,
            duplicate = result.IsDuplicate
        });
    }
}
