using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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
    /// <param name="status">Filtro por status operacional (`Open`, `UnderReview`, `WaitingParties`).</param>
    /// <param name="type">Filtro por tipo (`ServiceQuality`, `Billing`, `Conduct`, `NoShow`, `Other`).</param>
    /// <param name="operatorAdminId">Filtro por operador admin owner do caso.</param>
    /// <param name="operatorScope">Escopo de ownership: `all`, `assigned`, `unassigned`.</param>
    /// <param name="sla">Filtro de SLA: `all`, `breached`, `ontrack`.</param>
    /// <returns>Snapshot da fila com metadados de roteamento e itens de disputa.</returns>
    /// <response code="200">Fila carregada com sucesso.</response>
    /// <response code="401">Token ausente ou invalido.</response>
    /// <response code="403">Usuario sem perfil administrativo.</response>
    [HttpGet("queue")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetQueue(
        [FromQuery] Guid? disputeCaseId,
        [FromQuery] int take = 100,
        [FromQuery] string? status = null,
        [FromQuery] string? type = null,
        [FromQuery] Guid? operatorAdminId = null,
        [FromQuery] string? operatorScope = null,
        [FromQuery] string? sla = null)
    {
        var response = await _adminDisputeQueueService.GetQueueAsync(
            disputeCaseId,
            take,
            status,
            type,
            operatorAdminId,
            operatorScope,
            sla);
        return Ok(response);
    }

    /// <summary>
    /// Exporta a fila de disputas para CSV para auditoria externa.
    /// </summary>
    /// <remarks>
    /// Aplica os mesmos filtros da consulta da fila (`queue`) e retorna um arquivo CSV
    /// com colunas operacionais para analise, governanca e compartilhamento externo.
    /// </remarks>
    /// <param name="disputeCaseId">Caso de disputa a destacar na exportacao (opcional).</param>
    /// <param name="take">Quantidade maxima de registros exportados (1 a 200).</param>
    /// <param name="status">Filtro por status operacional.</param>
    /// <param name="type">Filtro por tipo de disputa.</param>
    /// <param name="operatorAdminId">Filtro por operador admin owner do caso.</param>
    /// <param name="operatorScope">Escopo de ownership: `all`, `assigned`, `unassigned`.</param>
    /// <param name="sla">Filtro de SLA: `all`, `breached`, `ontrack`.</param>
    /// <returns>Arquivo CSV da fila de disputas filtrada.</returns>
    /// <response code="200">CSV gerado com sucesso.</response>
    /// <response code="401">Token ausente ou invalido.</response>
    /// <response code="403">Usuario sem perfil administrativo.</response>
    [HttpGet("queue/export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportQueue(
        [FromQuery] Guid? disputeCaseId,
        [FromQuery] int take = 200,
        [FromQuery] string? status = null,
        [FromQuery] string? type = null,
        [FromQuery] Guid? operatorAdminId = null,
        [FromQuery] string? operatorScope = null,
        [FromQuery] string? sla = null)
    {
        var csv = await _adminDisputeQueueService.ExportQueueCsvAsync(
            disputeCaseId,
            take,
            status,
            type,
            operatorAdminId,
            operatorScope,
            sla);

        var fileName = $"admin-disputes-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", fileName);
    }

    /// <summary>
    /// Retorna KPIs e breakdowns de observabilidade do modulo de disputas.
    /// </summary>
    /// <remarks>
    /// O endpoint consolida metricas de qualidade operacional e compliance, incluindo:
    /// - volume de disputas abertas no periodo;
    /// - taxa de procedencia das decisoes (procedente/parcial);
    /// - tempo medio e mediano de resolucao;
    /// - backlog aberto e casos com SLA vencido;
    /// - distribuicao por tipo, prioridade, status e principais motivos.
    ///
    /// Esse snapshot e usado para monitoramento de risco e planejamento de capacidade
    /// da operacao de mediacao.
    /// </remarks>
    /// <param name="fromUtc">Data inicial UTC (opcional). Padrao: ultimos 30 dias.</param>
    /// <param name="toUtc">Data final UTC (opcional). Padrao: agora (UTC).</param>
    /// <param name="topTake">Quantidade de motivos no ranking de top reasons (3 a 50).</param>
    /// <returns>Dashboard de observabilidade do fluxo de disputas.</returns>
    /// <response code="200">Snapshot calculado com sucesso.</response>
    /// <response code="401">Token ausente ou invalido.</response>
    /// <response code="403">Usuario sem perfil administrativo.</response>
    [HttpGet("observability")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetObservability(
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] int topTake = 10)
    {
        var response = await _adminDisputeQueueService.GetObservabilityAsync(
            new AdminDisputeObservabilityQueryDto(fromUtc, toUtc, topTake));
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

    /// <summary>
    /// Atualiza o workflow operacional da disputa no backoffice.
    /// </summary>
    /// <remarks>
    /// Permite ao administrador mover o caso entre estados operacionais
    /// (`Open`, `UnderReview`, `WaitingParties`, `Cancelled`) durante a mediacao.
    ///
    /// Regras de negocio:
    /// - disputas encerradas nao podem ter workflow alterado;
    /// - transicoes invalidas retornam erro de validacao;
    /// - para `WaitingParties`, o campo `waitingForRole` e obrigatorio (`Client`/`Provider`);
    /// - opcionalmente, o admin pode assumir ownership do caso.
    /// </remarks>
    /// <param name="id">Identificador da disputa.</param>
    /// <param name="request">Payload de alteracao de workflow.</param>
    /// <returns>Resultado da operacao com snapshot atualizado do caso.</returns>
    /// <response code="200">Workflow atualizado com sucesso.</response>
    /// <response code="400">Payload invalido ou transicao nao permitida.</response>
    /// <response code="401">Token ausente ou invalido.</response>
    /// <response code="403">Usuario sem perfil administrativo.</response>
    /// <response code="404">Disputa nao encontrada.</response>
    [HttpPut("{id:guid}/workflow")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateWorkflow(Guid id, [FromBody] UpdateDisputeWorkflowRequest request)
    {
        var actorRaw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var actorEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(actorRaw) || !Guid.TryParse(actorRaw, out var actorUserId))
        {
            return Unauthorized();
        }

        var response = await _adminDisputeQueueService.UpdateWorkflowAsync(
            id,
            actorUserId,
            actorEmail,
            new ConsertaPraMim.Application.DTOs.AdminUpdateDisputeWorkflowRequestDto(
                request.Status,
                request.WaitingForRole,
                request.Note,
                request.ClaimOwnership));

        if (response.Success)
        {
            return Ok(response);
        }

        return response.ErrorCode switch
        {
            "not_found" => NotFound(response),
            "forbidden" => Forbid(),
            _ => BadRequest(response)
        };
    }

    /// <summary>
    /// Registra a decisao final de mediacao da disputa.
    /// </summary>
    /// <remarks>
    /// Outcome aceitos:
    /// - `procedente`: disputa procede em favor de quem abriu;
    /// - `improcedente`: disputa rejeitada;
    /// - `parcial`: decisao parcial com composicao.
    ///
    /// Regras de negocio:
    /// - justificativa e obrigatoria;
    /// - ao decidir, o caso e encerrado e torna-se imutavel para novas decisoes;
    /// - opcionalmente, pode aplicar impacto financeiro:
    ///   - <c>refund_client</c> para solicitar reembolso do pagamento;
    ///   - <c>credit_provider</c> para conceder credito ao prestador;
    ///   - <c>debit_provider</c> para debitar saldo de credito do prestador;
    /// - a trilha de auditoria registra outcome, justificativa e status final.
    /// </remarks>
    /// <param name="id">Identificador da disputa.</param>
    /// <param name="request">Payload da decisao administrativa.</param>
    /// <returns>Resultado da decisao com snapshot atualizado do caso.</returns>
    /// <response code="200">Decisao registrada com sucesso.</response>
    /// <response code="400">Payload invalido, disputa fechada ou regra de negocio violada.</response>
    /// <response code="401">Token ausente ou invalido.</response>
    /// <response code="403">Usuario sem perfil administrativo.</response>
    /// <response code="404">Disputa nao encontrada.</response>
    [HttpPost("{id:guid}/decision")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegisterDecision(Guid id, [FromBody] RegisterDisputeDecisionRequest request)
    {
        var actorRaw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var actorEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(actorRaw) || !Guid.TryParse(actorRaw, out var actorUserId))
        {
            return Unauthorized();
        }

        var response = await _adminDisputeQueueService.RegisterDecisionAsync(
            id,
            actorUserId,
            actorEmail,
            new ConsertaPraMim.Application.DTOs.AdminRegisterDisputeDecisionRequestDto(
                request.Outcome,
                request.Justification,
                request.ResolutionSummary,
                string.IsNullOrWhiteSpace(request.FinancialAction) || request.FinancialAction.Equals("none", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : new ConsertaPraMim.Application.DTOs.AdminDisputeFinancialDecisionRequestDto(
                        request.FinancialAction,
                        request.FinancialAmount,
                        request.FinancialReason)));

        if (response.Success)
        {
            return Ok(response);
        }

        return response.ErrorCode switch
        {
            "not_found" => NotFound(response),
            "forbidden" => Forbid(),
            _ => BadRequest(response)
        };
    }

    public record UpdateDisputeWorkflowRequest(
        string Status,
        string? WaitingForRole,
        string? Note,
        bool ClaimOwnership = true);

    public record RegisterDisputeDecisionRequest(
        string Outcome,
        string Justification,
        string? ResolutionSummary = null,
        string? FinancialAction = null,
        decimal? FinancialAmount = null,
        string? FinancialReason = null);
}
