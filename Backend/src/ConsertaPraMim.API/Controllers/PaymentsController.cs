using System.Security.Claims;
using System.Text;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize]
[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentCheckoutService _paymentCheckoutService;
    private readonly IPaymentWebhookService _paymentWebhookService;

    public PaymentsController(
        IPaymentCheckoutService paymentCheckoutService,
        IPaymentWebhookService paymentWebhookService)
    {
        _paymentCheckoutService = paymentCheckoutService;
        _paymentWebhookService = paymentWebhookService;
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> CreateCheckout([FromBody] CreatePaymentCheckoutRequestDto request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _paymentCheckoutService.CreateCheckoutAsync(actorUserId, actorRole, request, cancellationToken);
        if (result.Success)
        {
            return Ok(result);
        }

        return result.ErrorCode switch
        {
            "forbidden" => Forbid(),
            "request_not_found" => NotFound(new { errorCode = result.ErrorCode, message = result.ErrorMessage }),
            "provider_not_found" => NotFound(new { errorCode = result.ErrorCode, message = result.ErrorMessage }),
            "invalid_state" => Conflict(new { errorCode = result.ErrorCode, message = result.ErrorMessage }),
            "provider_required" => BadRequest(new { errorCode = result.ErrorCode, message = result.ErrorMessage }),
            "invalid_method" => BadRequest(new { errorCode = result.ErrorCode, message = result.ErrorMessage }),
            "invalid_amount" => BadRequest(new { errorCode = result.ErrorCode, message = result.ErrorMessage }),
            "invalid_request" => BadRequest(new { errorCode = result.ErrorCode, message = result.ErrorMessage }),
            _ => BadRequest(new { errorCode = result.ErrorCode, message = result.ErrorMessage })
        };
    }

    [AllowAnonymous]
    [HttpPost("webhook/{provider}")]
    public async Task<IActionResult> ReceiveWebhook(string provider, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<PaymentTransactionProvider>(provider, ignoreCase: true, out var paymentProvider))
        {
            return BadRequest(new { errorCode = "invalid_provider", message = "Provider de pagamento invalido." });
        }

        Request.EnableBuffering();
        string rawBody;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
        {
            rawBody = await reader.ReadToEndAsync(cancellationToken);
        }
        Request.Body.Position = 0;

        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return BadRequest(new { errorCode = "invalid_payload", message = "Payload do webhook nao informado." });
        }

        var signature =
            Request.Headers["X-Payment-Signature"].FirstOrDefault() ??
            Request.Headers["X-Webhook-Signature"].FirstOrDefault() ??
            Request.Headers["X-Mock-Signature"].FirstOrDefault() ??
            string.Empty;

        var eventId =
            Request.Headers["X-Payment-Event-Id"].FirstOrDefault() ??
            Request.Headers["X-Event-Id"].FirstOrDefault();

        var result = await _paymentWebhookService.ProcessWebhookAsync(
            new PaymentWebhookRequestDto(
                paymentProvider,
                rawBody,
                signature,
                eventId),
            cancellationToken);

        if (result.Success)
        {
            return Accepted(result);
        }

        return result.ErrorCode switch
        {
            "invalid_signature" => Unauthorized(new { errorCode = result.ErrorCode, message = result.ErrorMessage }),
            "invalid_payload" => BadRequest(new { errorCode = result.ErrorCode, message = result.ErrorMessage }),
            "transaction_not_found" => NotFound(new { errorCode = result.ErrorCode, message = result.ErrorMessage }),
            _ => BadRequest(new { errorCode = result.ErrorCode, message = result.ErrorMessage })
        };
    }

    private bool TryGetActor(out Guid actorUserId, out string actorRole)
    {
        actorUserId = Guid.Empty;
        actorRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

        var actorRaw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return !string.IsNullOrWhiteSpace(actorRaw) && Guid.TryParse(actorRaw, out actorUserId);
    }
}
