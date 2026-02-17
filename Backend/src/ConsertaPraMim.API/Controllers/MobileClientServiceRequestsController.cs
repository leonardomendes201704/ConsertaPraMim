using System.Security.Claims;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

/// <summary>
/// Endpoints dedicados ao app mobile/web do cliente para abertura de pedidos de servico.
/// </summary>
/// <remarks>
/// Este controlador isola o contrato de criacao de pedidos usado no app.
/// O objetivo e manter independencia em relacao aos portais Web, evitando regressao cruzada entre canais.
/// Fluxo implementado:
/// 1) listar categorias ativas;
/// 2) resolver CEP para endereco padronizado;
/// 3) criar pedido com validacoes de negocio do backend.
/// </remarks>
[Authorize(Roles = "Client")]
[ApiController]
[Route("api/mobile/client/service-requests")]
public class MobileClientServiceRequestsController : ControllerBase
{
    private readonly IMobileClientServiceRequestService _mobileClientServiceRequestService;

    public MobileClientServiceRequestsController(IMobileClientServiceRequestService mobileClientServiceRequestService)
    {
        _mobileClientServiceRequestService = mobileClientServiceRequestService;
    }

    /// <summary>
    /// Lista as categorias de servico ativas disponiveis para abertura de pedido no app.
    /// </summary>
    /// <returns>
    /// Colecao de categorias ativas com:
    /// <list type="bullet">
    /// <item><description><c>id</c>: identificador da categoria para envio no create;</description></item>
    /// <item><description><c>name</c>: nome exibivel no app;</description></item>
    /// <item><description><c>slug</c>: chave semantica para logs/analytics;</description></item>
    /// <item><description><c>legacyCategory</c>: categoria legado usada no core do dominio;</description></item>
    /// <item><description><c>icon</c>: icon name para renderizacao no app.</description></item>
    /// </list>
    /// </returns>
    /// <response code="200">Categorias retornadas com sucesso.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Client.</response>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(IReadOnlyList<MobileClientServiceRequestCategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCategories()
    {
        if (!TryGetClientUserId(out _))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_client_service_request_invalid_user_claim",
                message = "Nao foi possivel identificar o cliente autenticado."
            });
        }

        var categories = await _mobileClientServiceRequestService.GetActiveCategoriesAsync();
        return Ok(categories);
    }

    /// <summary>
    /// Resolve o CEP informado para endereco padronizado e coordenadas geograficas.
    /// </summary>
    /// <param name="zipCode">CEP com ou sem mascara (apenas 8 digitos significativos).</param>
    /// <returns>
    /// Endereco resolvido para o fluxo de abertura do pedido no app, com rua/cidade e coordenadas.
    /// </returns>
    /// <response code="200">CEP resolvido com sucesso.</response>
    /// <response code="400">CEP invalido (formato inconsistente).</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Client.</response>
    /// <response code="404">CEP nao encontrado ou sem geocodificacao valida.</response>
    [HttpGet("zip-resolution")]
    [ProducesResponseType(typeof(MobileClientResolveZipResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolveZip([FromQuery] string zipCode)
    {
        if (!TryGetClientUserId(out _))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_client_service_request_invalid_user_claim",
                message = "Nao foi possivel identificar o cliente autenticado."
            });
        }

        if (string.IsNullOrWhiteSpace(zipCode))
        {
            return BadRequest(new
            {
                errorCode = "mobile_client_service_request_invalid_zip",
                message = "Informe um CEP valido com 8 digitos."
            });
        }

        var resolved = await _mobileClientServiceRequestService.ResolveZipAsync(zipCode);
        if (resolved == null)
        {
            return NotFound(new
            {
                errorCode = "mobile_client_service_request_zip_not_found",
                message = "Nao foi possivel localizar esse CEP."
            });
        }

        return Ok(resolved);
    }

    /// <summary>
    /// Cria um novo pedido de servico para o cliente autenticado.
    /// </summary>
    /// <param name="request">
    /// Payload de criacao do pedido no app.
    /// Campos obrigatorios: <c>categoryId</c>, <c>description</c>, <c>zipCode</c>.
    /// Campos opcionais: <c>street</c> e <c>city</c> (caso app ja tenha resolucao local).
    /// </param>
    /// <returns>
    /// Pedido criado em contrato dedicado mobile, incluindo resumo em formato de card para renderizacao imediata.
    /// </returns>
    /// <response code="201">Pedido criado com sucesso.</response>
    /// <response code="400">Erro de validacao funcional (categoria invalida, CEP invalido, descricao insuficiente).</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Client.</response>
    [HttpPost]
    [ProducesResponseType(typeof(MobileClientCreateServiceRequestResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] MobileClientCreateServiceRequestRequestDto request)
    {
        if (!TryGetClientUserId(out var clientUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_client_service_request_invalid_user_claim",
                message = "Nao foi possivel identificar o cliente autenticado."
            });
        }

        try
        {
            var response = await _mobileClientServiceRequestService.CreateAsync(clientUserId, request);
            return CreatedAtAction(
                nameof(ConsertaPraMim.API.Controllers.MobileClientOrdersController.GetOrderDetails),
                "MobileClientOrders",
                new { orderId = response.Order.Id },
                response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new
            {
                errorCode = "mobile_client_service_request_unauthorized_actor",
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                errorCode = "mobile_client_service_request_invalid_operation",
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
