using System.Security.Claims;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

/// <summary>
/// Endpoints dedicados ao app mobile/web do prestador para registro de dispositivos push.
/// </summary>
/// <remarks>
/// Mantem o contrato de push isolado para o canal mobile provider, sem acoplar com APIs de portal.
/// </remarks>
[Authorize(Roles = "Provider")]
[ApiController]
[Route("api/mobile/provider/push-devices")]
public class MobileProviderPushDevicesController : ControllerBase
{
    private readonly IMobilePushDeviceService _mobilePushDeviceService;

    public MobileProviderPushDevicesController(IMobilePushDeviceService mobilePushDeviceService)
    {
        _mobilePushDeviceService = mobilePushDeviceService;
    }

    /// <summary>
    /// Registra (ou atualiza) token push do dispositivo do prestador autenticado.
    /// </summary>
    /// <param name="request">
    /// Payload de registro do token FCM:
    /// <list type="bullet">
    /// <item><description><c>token</c>: obrigatorio.</description></item>
    /// <item><description><c>platform</c>: android/ios/web.</description></item>
    /// <item><description><c>deviceId</c>, <c>deviceModel</c>, <c>osVersion</c>, <c>appVersion</c>: opcionais.</description></item>
    /// </list>
    /// </param>
    /// <returns>Resumo do device registrado no canal <c>provider</c>.</returns>
    /// <response code="200">Registro concluido com sucesso.</response>
    /// <response code="400">Payload invalido.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(MobilePushDeviceRegistrationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Register([FromBody] MobilePushDeviceRegisterRequestDto request, CancellationToken cancellationToken)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_push_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        try
        {
            var response = await _mobilePushDeviceService.RegisterAsync(
                providerUserId,
                appKind: "provider",
                request,
                cancellationToken);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                errorCode = "mobile_provider_push_register_invalid_operation",
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Desregistra token/dispositivo push do prestador autenticado (uso recomendado no logout).
    /// </summary>
    /// <param name="request">
    /// Informe token e/ou deviceId:
    /// <list type="bullet">
    /// <item><description><c>token</c>: desativa token especifico;</description></item>
    /// <item><description><c>deviceId</c>: desativa todos tokens ativos do aparelho.</description></item>
    /// </list>
    /// </param>
    /// <returns>Quantidade de tokens desativados.</returns>
    /// <response code="200">Processamento concluido.</response>
    /// <response code="400">Payload invalido.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider.</response>
    [HttpPost("unregister")]
    [ProducesResponseType(typeof(MobilePushDeviceUnregisterResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Unregister([FromBody] MobilePushDeviceUnregisterRequestDto request, CancellationToken cancellationToken)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_push_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        try
        {
            var response = await _mobilePushDeviceService.UnregisterAsync(
                providerUserId,
                appKind: "provider",
                request,
                cancellationToken);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                errorCode = "mobile_provider_push_unregister_invalid_operation",
                message = ex.Message
            });
        }
    }

    private bool TryGetProviderUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var userIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdRaw, out userId);
    }
}
