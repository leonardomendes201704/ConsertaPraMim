using System.Security.Claims;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

/// <summary>
/// Endpoints dedicados ao app mobile/web do cliente para consulta de pedidos.
/// </summary>
/// <remarks>
/// Este controlador foi criado para isolar o contrato do app cliente dos contratos usados pelos portais Web.
/// Assim, evolucoes no app mobile nao quebram as telas de backoffice/prestador/cliente web.
/// </remarks>
[Authorize(Roles = "Client")]
[ApiController]
[Route("api/mobile/client/orders")]
public class MobileClientOrdersController : ControllerBase
{
    private readonly IMobileClientOrderService _mobileClientOrderService;

    public MobileClientOrdersController(IMobileClientOrderService mobileClientOrderService)
    {
        _mobileClientOrderService = mobileClientOrderService;
    }

    /// <summary>
    /// Retorna os pedidos do cliente autenticado separados por grupos operacionais do app.
    /// </summary>
    /// <param name="takePerBucket">
    /// Quantidade maxima por grupo retornado.
    /// O endpoint retorna dois grupos: <c>openOrders</c> (nao finalizados) e <c>finalizedOrders</c> (finalizados/cancelados).
    /// </param>
    /// <returns>
    /// Payload orientado ao app com listas separadas para renderizacao das abas "Ativos" e "Historico".
    /// </returns>
    /// <response code="200">Pedidos retornados com sucesso.</response>
    /// <response code="401">Token invalido/ausente ou usuario nao autenticado.</response>
    /// <response code="403">Usuario autenticado sem role Client.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyOrders([FromQuery] int takePerBucket = 100)
    {
        var userIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdRaw, out var clientUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_client_orders_invalid_user_claim",
                message = "Nao foi possivel identificar o cliente autenticado."
            });
        }

        var response = await _mobileClientOrderService.GetMyOrdersAsync(clientUserId, takePerBucket);
        return Ok(response);
    }
}
