using System.Security.Claims;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

/// <summary>
/// Endpoints dedicados ao app admin mobile para registro de dispositivos push.
/// </summary>
[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/mobile/admin/push-devices")]
public class MobileAdminPushDevicesController : ControllerBase
{
    private readonly IMobilePushDeviceService _mobilePushDeviceService;

    public MobileAdminPushDevicesController(IMobilePushDeviceService mobilePushDeviceService)
    {
        _mobilePushDeviceService = mobilePushDeviceService;
    }

    /// <summary>
    /// Registra (ou atualiza) token push do admin autenticado.
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(MobilePushDeviceRegistrationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Register([FromBody] MobilePushDeviceRegisterRequestDto request, CancellationToken cancellationToken)
    {
        if (!TryGetAdminUserId(out var adminUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_admin_push_invalid_user_claim",
                message = "Nao foi possivel identificar o admin autenticado."
            });
        }

        try
        {
            var response = await _mobilePushDeviceService.RegisterAsync(
                adminUserId,
                appKind: "admin",
                request,
                cancellationToken);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                errorCode = "mobile_admin_push_register_invalid_operation",
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Desregistra token/dispositivo push do admin autenticado.
    /// </summary>
    [HttpPost("unregister")]
    [ProducesResponseType(typeof(MobilePushDeviceUnregisterResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Unregister([FromBody] MobilePushDeviceUnregisterRequestDto request, CancellationToken cancellationToken)
    {
        if (!TryGetAdminUserId(out var adminUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_admin_push_invalid_user_claim",
                message = "Nao foi possivel identificar o admin autenticado."
            });
        }

        try
        {
            var response = await _mobilePushDeviceService.UnregisterAsync(
                adminUserId,
                appKind: "admin",
                request,
                cancellationToken);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                errorCode = "mobile_admin_push_unregister_invalid_operation",
                message = ex.Message
            });
        }
    }

    private bool TryGetAdminUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var userIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdRaw, out userId);
    }
}
