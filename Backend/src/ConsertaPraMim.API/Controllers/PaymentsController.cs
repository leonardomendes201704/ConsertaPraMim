using System.Security.Claims;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize]
[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentCheckoutService _paymentCheckoutService;

    public PaymentsController(IPaymentCheckoutService paymentCheckoutService)
    {
        _paymentCheckoutService = paymentCheckoutService;
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

    private bool TryGetActor(out Guid actorUserId, out string actorRole)
    {
        actorUserId = Guid.Empty;
        actorRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

        var actorRaw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return !string.IsNullOrWhiteSpace(actorRaw) && Guid.TryParse(actorRaw, out actorUserId);
    }
}
