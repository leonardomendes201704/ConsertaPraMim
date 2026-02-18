using System.Security.Claims;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

/// <summary>
/// Endpoints dedicados ao app mobile/web do cliente para registrar dispositivos de push notification.
/// </summary>
/// <remarks>
/// Este controlador e exclusivo do canal mobile cliente e nao impacta contratos dos portais Web.
/// O registro de token permite notificacoes push (FCM) mesmo com app fechado, quando a plataforma estiver configurada.
/// </remarks>
[Authorize(Roles = "Client")]
[ApiController]
[Route("api/mobile/client/push-devices")]
public class MobileClientPushDevicesController : ControllerBase
{
    private readonly IMobilePushDeviceService _mobilePushDeviceService;

    public MobileClientPushDevicesController(IMobilePushDeviceService mobilePushDeviceService)
    {
        _mobilePushDeviceService = mobilePushDeviceService;
    }

    /// <summary>
    /// Registra (ou atualiza) o token push do dispositivo do cliente autenticado.
    /// </summary>
    /// <param name="request">
    /// Dados do dispositivo/token:
    /// <list type="bullet">
    /// <item><description><c>token</c>: token FCM do app (obrigatorio);</description></item>
    /// <item><description><c>platform</c>: android/ios/web (default: android);</description></item>
    /// <item><description><c>deviceId</c>, <c>deviceModel</c>, <c>osVersion</c>, <c>appVersion</c>: metadados opcionais para suporte.</description></item>
    /// </list>
    /// </param>
    /// <returns>Resumo do registro processado para o canal <c>client</c>.</returns>
    /// <response code="200">Token registrado/atualizado com sucesso.</response>
    /// <response code="400">Payload invalido (token ausente/plataforma invalida).</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Client.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(MobilePushDeviceRegistrationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Register([FromBody] MobilePushDeviceRegisterRequestDto request, CancellationToken cancellationToken)
    {
        if (!TryGetClientUserId(out var clientUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_client_push_invalid_user_claim",
                message = "Nao foi possivel identificar o cliente autenticado."
            });
        }

        try
        {
            var response = await _mobilePushDeviceService.RegisterAsync(
                clientUserId,
                appKind: "client",
                request,
                cancellationToken);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                errorCode = "mobile_client_push_register_invalid_operation",
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Desregistra token/dispositivo push do cliente autenticado.
    /// </summary>
    /// <remarks>
    /// Use no logout para impedir envio de notificacao para dispositivo antigo.
    /// </remarks>
    /// <param name="request">
    /// Informe ao menos um dos campos:
    /// <list type="bullet">
    /// <item><description><c>token</c>: token exato a desativar.</description></item>
    /// <item><description><c>deviceId</c>: desativa todos os tokens ativos do device no canal client.</description></item>
    /// </list>
    /// </param>
    /// <returns>Quantidade de dispositivos/tokens desativados.</returns>
    /// <response code="200">Processamento concluido.</response>
    /// <response code="400">Payload invalido (token/deviceId ausentes).</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Client.</response>
    [HttpPost("unregister")]
    [ProducesResponseType(typeof(MobilePushDeviceUnregisterResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Unregister([FromBody] MobilePushDeviceUnregisterRequestDto request, CancellationToken cancellationToken)
    {
        if (!TryGetClientUserId(out var clientUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_client_push_invalid_user_claim",
                message = "Nao foi possivel identificar o cliente autenticado."
            });
        }

        try
        {
            var response = await _mobilePushDeviceService.UnregisterAsync(
                clientUserId,
                appKind: "client",
                request,
                cancellationToken);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                errorCode = "mobile_client_push_unregister_invalid_operation",
                message = ex.Message
            });
        }
    }

    private bool TryGetClientUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var userIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdRaw, out userId);
    }
}
