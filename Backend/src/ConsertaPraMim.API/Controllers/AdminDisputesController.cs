using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/disputes")]
public class AdminDisputesController : ControllerBase
{
    private readonly IAdminDisputeQueueService _adminDisputeQueueService;

    public AdminDisputesController(IAdminDisputeQueueService adminDisputeQueueService)
    {
        _adminDisputeQueueService = adminDisputeQueueService;
    }

    /// <summary>
    /// Retorna a fila operacional de disputas abertas para mediacao administrativa.
    /// </summary>
    /// <remarks>
    /// Regras de negocio:
    /// - Lista apenas disputas em andamento (abertas, em analise ou aguardando partes).
    /// - A fila e ordenada por prioridade e vencimento de SLA para apoiar triagem.
    /// - O campo <c>highlightedDisputeCaseId</c> permite destacar um caso especifico,
    ///   tipicamente quando o admin chegou via notificacao.
    /// </remarks>
    /// <param name="disputeCaseId">Caso de disputa a destacar visualmente na fila (opcional).</param>
    /// <param name="take">Quantidade maxima de casos retornados (1 a 200).</param>
    /// <returns>Snapshot da fila com metadados de roteamento e itens de disputa.</returns>
    /// <response code="200">Fila carregada com sucesso.</response>
    /// <response code="401">Token ausente ou invalido.</response>
    /// <response code="403">Usuario sem perfil administrativo.</response>
    [HttpGet("queue")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetQueue([FromQuery] Guid? disputeCaseId, [FromQuery] int take = 100)
    {
        var response = await _adminDisputeQueueService.GetQueueAsync(disputeCaseId, take);
        return Ok(response);
    }

    /// <summary>
    /// Retorna o detalhe completo de uma disputa para mediacao administrativa.
    /// </summary>
    /// <remarks>
    /// Inclui visao consolidada para analise:
    /// - dados do caso (tipo, prioridade, SLA, status e ownership);
    /// - historico de mensagens, anexos e trilha de auditoria;
    /// - contexto de pedido/agendamento para tomada de decisao.
    /// </remarks>
    /// <param name="id">Identificador da disputa.</param>
    /// <returns>Detalhe operacional do caso de disputa.</returns>
    /// <response code="200">Caso encontrado e carregado com sucesso.</response>
    /// <response code="401">Token ausente ou invalido.</response>
    /// <response code="403">Usuario sem perfil administrativo.</response>
    /// <response code="404">Disputa nao encontrada.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var response = await _adminDisputeQueueService.GetCaseDetailsAsync(id);
        return response == null ? NotFound() : Ok(response);
    }
}
