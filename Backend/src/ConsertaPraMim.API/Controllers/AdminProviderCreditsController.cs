using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/provider-credits")]
public class AdminProviderCreditsController : ControllerBase
{
    private readonly IAdminProviderCreditService _adminProviderCreditService;
    private readonly IProviderCreditService _providerCreditService;

    public AdminProviderCreditsController(
        IAdminProviderCreditService adminProviderCreditService,
        IProviderCreditService providerCreditService)
    {
        _adminProviderCreditService = adminProviderCreditService;
        _providerCreditService = providerCreditService;
    }

    /// <summary>
    /// Consulta o saldo atual da carteira de creditos de um prestador.
    /// </summary>
    /// <remarks>
    /// Endpoint de leitura administrativa usado para suporte comercial e auditoria financeira.
    /// Retorna saldo consolidado e data do ultimo movimento registrado no ledger.
    /// </remarks>
    /// <param name="providerId">Identificador do prestador.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisicao.</param>
    /// <returns>Saldo atual da carteira de creditos.</returns>
    [HttpGet("{providerId:guid}/balance")]
    [ProducesResponseType(typeof(ProviderCreditBalanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBalance(Guid providerId, CancellationToken cancellationToken)
    {
        try
        {
            var balance = await _providerCreditService.GetBalanceAsync(providerId, cancellationToken);
            return Ok(balance);
        }
        catch (InvalidOperationException ex) when (IsProviderNotFoundError(ex))
        {
            return NotFound(new { errorCode = "not_found", errorMessage = "Prestador nao encontrado para consulta de credito." });
        }
    }

    /// <summary>
    /// Consulta o extrato administrativo da carteira de creditos de um prestador.
    /// </summary>
    /// <remarks>
    /// Permite filtrar por periodo e tipo de lancamento (`Grant`, `Debit`, `Expire`, `Reversal`) com paginacao.
    /// </remarks>
    /// <param name="providerId">Identificador do prestador.</param>
    /// <param name="fromUtc">Data inicial em UTC (opcional).</param>
    /// <param name="toUtc">Data final em UTC (opcional).</param>
    /// <param name="entryType">Tipo de entrada de ledger (opcional).</param>
    /// <param name="page">Pagina atual (minimo 1).</param>
    /// <param name="pageSize">Quantidade de itens por pagina (maximo 100).</param>
    /// <param name="cancellationToken">Token de cancelamento da requisicao.</param>
    /// <returns>Extrato paginado do ledger do prestador.</returns>
    [HttpGet("{providerId:guid}/statement")]
    [ProducesResponseType(typeof(ProviderCreditStatementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatement(
        Guid providerId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? entryType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseEntryType(entryType, out var parsedEntryType))
        {
            return BadRequest(new { errorMessage = "entryType invalido." });
        }

        try
        {
            var query = new ProviderCreditStatementQueryDto(fromUtc, toUtc, parsedEntryType, page, pageSize);
            var statement = await _providerCreditService.GetStatementAsync(providerId, query, cancellationToken);
            return Ok(statement);
        }
        catch (InvalidOperationException ex) when (IsProviderNotFoundError(ex))
        {
            return NotFound(new { errorCode = "not_found", errorMessage = "Prestador nao encontrado para consulta de extrato." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { errorCode = "validation_error", errorMessage = ex.Message });
        }
    }

    /// <summary>
    /// Gera relatório consolidado de uso de créditos por prestador.
    /// </summary>
    /// <remarks>
    /// Relatório administrativo para auditoria operacional/comercial com:
    /// - total concedido, consumido, expirado e estornado no recorte;
    /// - saldo aberto por prestador;
    /// - variação líquida e quantidade de movimentos;
    /// - busca textual por nome/email.
    ///
    /// Filtros aceitos:
    /// - <c>fromUtc</c> / <c>toUtc</c> para período;
    /// - <c>entryType</c> para tipo específico do ledger;
    /// - <c>status</c> (`all`, `credit`, `debit`) para agrupamento lógico;
    /// - <c>searchTerm</c> para localizar prestadores por nome/email.
    /// </remarks>
    /// <param name="fromUtc">Data inicial UTC (opcional).</param>
    /// <param name="toUtc">Data final UTC (opcional).</param>
    /// <param name="entryType">Tipo de entrada de ledger (opcional).</param>
    /// <param name="status">Status lógico da movimentação (`all`, `credit`, `debit`).</param>
    /// <param name="searchTerm">Busca textual por nome/email do prestador.</param>
    /// <param name="page">Página atual (mínimo 1).</param>
    /// <param name="pageSize">Quantidade por página (máximo 100).</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>Relatório paginado de uso de créditos por prestador.</returns>
    [HttpGet("usage-report")]
    [ProducesResponseType(typeof(AdminProviderCreditUsageReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUsageReport(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? entryType,
        [FromQuery] string? status = "all",
        [FromQuery] string? searchTerm = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseEntryType(entryType, out var parsedEntryType))
        {
            return BadRequest(new { errorMessage = "entryType invalido." });
        }

        if (!TryParseStatus(status, out var parsedStatus))
        {
            return BadRequest(new { errorMessage = "status invalido. Utilize all, credit ou debit." });
        }

        var report = await _adminProviderCreditService.GetUsageReportAsync(
            new AdminProviderCreditUsageReportQueryDto(
                FromUtc: fromUtc,
                ToUtc: toUtc,
                EntryType: parsedEntryType,
                Status: parsedStatus,
                SearchTerm: searchTerm,
                Page: page,
                PageSize: pageSize),
            cancellationToken);

        return Ok(report);
    }

    /// <summary>
    /// Concede credito administrativo para um prestador com validacoes de negocio.
    /// </summary>
    /// <remarks>
    /// Regras aplicadas:
    /// - `Campanha`: expiracao obrigatoria e futura (maximo 365 dias).
    /// - `Premio`: expiracao opcional (default de 90 dias quando ausente).
    /// - `Ajuste`: expiracao opcional.
    ///
    /// Em caso de sucesso:
    /// - gera lancamento `Grant` no ledger;
    /// - envia notificacao em tempo real ao prestador;
    /// - registra auditoria administrativa com before/after.
    /// </remarks>
    /// <param name="request">Payload da concessao administrativa de credito.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisicao.</param>
    /// <returns>Resultado da mutacao administrativa de credito.</returns>
    [HttpPost("grants")]
    [ProducesResponseType(typeof(AdminProviderCreditMutationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AdminProviderCreditMutationResultDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AdminProviderCreditMutationResultDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(AdminProviderCreditMutationResultDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(AdminProviderCreditMutationResultDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(AdminProviderCreditMutationResultDto), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Grant(
        [FromBody] AdminProviderCreditGrantRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveActor(out var actorUserId, out var actorEmail))
        {
            return Unauthorized();
        }

        var result = await _adminProviderCreditService.GrantAsync(
            request,
            actorUserId,
            actorEmail,
            cancellationToken);

        return MapMutationResult(result);
    }

    /// <summary>
    /// Estorna credito nao consumido de um prestador.
    /// </summary>
    /// <remarks>
    /// O estorno administrativo gera lancamento `Debit` no ledger e falha quando o saldo disponivel
    /// nao e suficiente para a remocao solicitada.
    /// </remarks>
    /// <param name="request">Payload do estorno administrativo.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisicao.</param>
    /// <returns>Resultado da mutacao administrativa de credito.</returns>
    [HttpPost("reversals")]
    [ProducesResponseType(typeof(AdminProviderCreditMutationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AdminProviderCreditMutationResultDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AdminProviderCreditMutationResultDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(AdminProviderCreditMutationResultDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(AdminProviderCreditMutationResultDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(AdminProviderCreditMutationResultDto), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reverse(
        [FromBody] AdminProviderCreditReversalRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveActor(out var actorUserId, out var actorEmail))
        {
            return Unauthorized();
        }

        var result = await _adminProviderCreditService.ReverseAsync(
            request,
            actorUserId,
            actorEmail,
            cancellationToken);

        return MapMutationResult(result);
    }

    private IActionResult MapMutationResult(AdminProviderCreditMutationResultDto result)
    {
        if (result.Success)
        {
            return Ok(result);
        }

        return result.ErrorCode switch
        {
            "not_found" => NotFound(result),
            "provider_inactive" => Conflict(result),
            "insufficient_balance" => Conflict(result),
            _ => BadRequest(result)
        };
    }

    private bool TryResolveActor(out Guid actorUserId, out string actorEmail)
    {
        actorUserId = Guid.Empty;
        actorEmail = string.Empty;

        var actorUserIdRaw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        actorEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
        return !string.IsNullOrWhiteSpace(actorUserIdRaw) && Guid.TryParse(actorUserIdRaw, out actorUserId);
    }

    private static bool TryParseEntryType(string? raw, out ProviderCreditLedgerEntryType? entryType)
    {
        entryType = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (Enum.TryParse<ProviderCreditLedgerEntryType>(raw.Trim(), true, out var parsed))
        {
            entryType = parsed;
            return true;
        }

        return false;
    }

    private static bool TryParseStatus(string? raw, out string status)
    {
        status = "all";
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        var normalized = raw.Trim().ToLowerInvariant();
        if (normalized is "all" or "credit" or "debit")
        {
            status = normalized;
            return true;
        }

        return false;
    }

    private static bool IsProviderNotFoundError(InvalidOperationException ex)
    {
        return ex.Message.Contains("Prestador nao encontrado", StringComparison.OrdinalIgnoreCase);
    }
}
