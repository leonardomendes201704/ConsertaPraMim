using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/growth")]
public class AdminGrowthController : ControllerBase
{
    private readonly IAdminGrowthService _adminGrowthService;
    private readonly IAdminLiquidityScoreService _adminLiquidityScoreService;

    public AdminGrowthController(
        IAdminGrowthService adminGrowthService,
        IAdminLiquidityScoreService adminLiquidityScoreService)
    {
        _adminGrowthService = adminGrowthService;
        _adminLiquidityScoreService = adminLiquidityScoreService;
    }

    /// <summary>
    /// Retorna funil operacional de crescimento (pedido -> proposta -> aceite) com medicao de SLA por etapa.
    /// </summary>
    /// <param name="fromUtc">Data inicial opcional do recorte, em UTC.</param>
    /// <param name="toUtc">Data final opcional do recorte, em UTC.</param>
    /// <param name="category">Filtro opcional por categoria (nome, enum ou legacy).</param>
    /// <param name="city">Filtro opcional por cidade.</param>
    /// <param name="proposalSlaMinutes">SLA alvo para primeira proposta, em minutos.</param>
    /// <param name="acceptanceSlaHours">SLA alvo para aceite apos primeira proposta, em horas.</param>
    /// <returns>Payload de funil com estagios, taxas de SLA e alertas operacionais.</returns>
    [HttpGet("funnel")]
    [ProducesResponseType(typeof(AdminGrowthFunnelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetFunnel(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? category,
        [FromQuery] string? city,
        [FromQuery] int proposalSlaMinutes = 30,
        [FromQuery] int acceptanceSlaHours = 24)
    {
        var query = new AdminGrowthFunnelQueryDto(
            FromUtc: fromUtc,
            ToUtc: toUtc,
            Category: category,
            City: city,
            ProposalSlaMinutes: proposalSlaMinutes,
            AcceptanceSlaHours: acceptanceSlaHours);

        var response = await _adminGrowthService.GetFunnelAsync(query);
        return Ok(response);
    }

    /// <summary>
    /// Retorna cockpit executivo de growth com North Star, KPIs e tendencia semanal.
    /// </summary>
    /// <param name="fromUtc">Data inicial opcional do recorte, em UTC.</param>
    /// <param name="toUtc">Data final opcional do recorte, em UTC.</param>
    /// <param name="category">Filtro opcional por categoria.</param>
    /// <param name="city">Filtro opcional por cidade.</param>
    /// <param name="proposalSlaMinutes">SLA alvo para primeira proposta, em minutos.</param>
    /// <param name="acceptanceSlaHours">SLA alvo para aceite apos primeira proposta, em horas.</param>
    /// <param name="northStarResolutionHours">Janela alvo, em horas, para medir a North Star `RQ72`.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisicao.</param>
    /// <returns>Painel executivo consolidado com metas trimestrais, KPIs e serie semanal.</returns>
    [HttpGet("executive-cockpit")]
    [ProducesResponseType(typeof(AdminGrowthExecutiveCockpitDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetExecutiveCockpit(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? category,
        [FromQuery] string? city,
        [FromQuery] int proposalSlaMinutes = 30,
        [FromQuery] int acceptanceSlaHours = 24,
        [FromQuery] int northStarResolutionHours = 72,
        CancellationToken cancellationToken = default)
    {
        var response = await _adminGrowthService.GetExecutiveCockpitAsync(
            new AdminGrowthExecutiveCockpitQueryDto(
                FromUtc: fromUtc,
                ToUtc: toUtc,
                Category: category,
                City: city,
                ProposalSlaMinutes: proposalSlaMinutes,
                AcceptanceSlaHours: acceptanceSlaHours,
                NorthStarResolutionHours: northStarResolutionHours),
            cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Retorna ritual semanal de growth com pauta recomendada e ultimas atas registradas.
    /// </summary>
    /// <param name="asOfUtc">Data de referencia opcional, em UTC.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisicao.</param>
    /// <returns>Snapshot da semana com agenda e historico de atas recentes.</returns>
    [HttpGet("weekly-ritual")]
    [ProducesResponseType(typeof(AdminGrowthWeeklyRitualSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetWeeklyRitual(
        [FromQuery] DateTime? asOfUtc,
        CancellationToken cancellationToken = default)
    {
        var response = await _adminGrowthService.GetWeeklyRitualSnapshotAsync(asOfUtc, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Registra ata semanal de growth com decisoes, owners e riscos.
    /// </summary>
    /// <param name="request">Payload da ata semanal.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisicao.</param>
    /// <returns>Ata persistida para trilha operacional.</returns>
    [HttpPost("weekly-ritual/record")]
    [ProducesResponseType(typeof(AdminGrowthWeeklyRitualRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RecordWeeklyRitual(
        [FromBody] AdminGrowthWeeklyRitualRecordRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Summary))
        {
            return BadRequest(new { errorCode = "invalid_request", errorMessage = "Resumo da ata semanal e obrigatorio." });
        }

        var actorUserId = ResolveActorUserId();
        var actorEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "admin@consertapramim.local";
        var response = await _adminGrowthService.RecordWeeklyRitualAsync(
            request,
            actorUserId,
            actorEmail,
            cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Retorna snapshot da revisao mensal de estrategia com pauta e historico de atas.
    /// </summary>
    /// <param name="referenceMonthUtc">Mes de referencia opcional (qualquer data do mes, em UTC).</param>
    /// <param name="cancellationToken">Token de cancelamento da requisicao.</param>
    /// <returns>Agenda mensal e historico recente de revisoes estrategicas.</returns>
    [HttpGet("monthly-review")]
    [ProducesResponseType(typeof(AdminGrowthMonthlyReviewSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMonthlyReview(
        [FromQuery] DateTime? referenceMonthUtc,
        CancellationToken cancellationToken = default)
    {
        var response = await _adminGrowthService.GetMonthlyReviewSnapshotAsync(referenceMonthUtc, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Registra ata da revisao mensal de growth com decisoes estrategicas e plano do proximo ciclo.
    /// </summary>
    /// <param name="request">Payload da revisao mensal.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisicao.</param>
    /// <returns>Ata mensal persistida para trilha executiva.</returns>
    [HttpPost("monthly-review/record")]
    [ProducesResponseType(typeof(AdminGrowthMonthlyReviewRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RecordMonthlyReview(
        [FromBody] AdminGrowthMonthlyReviewRecordRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ExecutiveSummary))
        {
            return BadRequest(new { errorCode = "invalid_request", errorMessage = "Resumo executivo da revisao mensal e obrigatorio." });
        }

        var actorUserId = ResolveActorUserId();
        var actorEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "admin@consertapramim.local";
        var response = await _adminGrowthService.RecordMonthlyReviewAsync(
            request,
            actorUserId,
            actorEmail,
            cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Retorna score de liquidez por regiao/categoria com historico diario e alertas de deficit.
    /// </summary>
    /// <param name="fromUtc">Data inicial opcional do recorte, em UTC.</param>
    /// <param name="toUtc">Data final opcional do recorte, em UTC.</param>
    /// <param name="category">Filtro opcional por categoria.</param>
    /// <param name="city">Filtro opcional por cidade.</param>
    /// <param name="proposalSlaMinutes">SLA alvo para primeira proposta, em minutos.</param>
    /// <param name="take">Limite de combinacoes regiao/categoria retornadas no ranking.</param>
    /// <returns>Score de liquidez por combinacao, serie historica e alertas operacionais.</returns>
    [HttpGet("liquidity-score")]
    [ProducesResponseType(typeof(AdminLiquidityScoreResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetLiquidityScore(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? category,
        [FromQuery] string? city,
        [FromQuery] int proposalSlaMinutes = 30,
        [FromQuery] int take = 50)
    {
        var response = await _adminLiquidityScoreService.GetScoreAsync(
            new AdminLiquidityScoreQueryDto(
                FromUtc: fromUtc,
                ToUtc: toUtc,
                Category: category,
                City: city,
                ProposalSlaMinutes: proposalSlaMinutes,
                Take: take));

        return Ok(response);
    }

    /// <summary>
    /// Retorna segmentacao de prestadores inativos para motor de reativacao automatica.
    /// </summary>
    /// <param name="asOfUtc">Data de referencia opcional para calcular inatividade (UTC).</param>
    /// <param name="warmFromDays">Inicio da faixa de atencao (dias sem atividade).</param>
    /// <param name="coldFromDays">Inicio da faixa fria (dias sem atividade).</param>
    /// <param name="dormantFromDays">Inicio da faixa dormente (dias sem atividade).</param>
    /// <param name="hibernatedFromDays">Inicio da faixa hibernada (dias sem atividade).</param>
    /// <param name="previewTake">Quantidade de prestadores de preview para operacao.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisicao.</param>
    /// <returns>Segmentos por periodo/categoria/regiao para reativacao.</returns>
    [HttpGet("provider-reactivation/segments")]
    [ProducesResponseType(typeof(AdminProviderReactivationSegmentsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetProviderReactivationSegments(
        [FromQuery] DateTime? asOfUtc,
        [FromQuery] int warmFromDays = 7,
        [FromQuery] int coldFromDays = 15,
        [FromQuery] int dormantFromDays = 31,
        [FromQuery] int hibernatedFromDays = 61,
        [FromQuery] int previewTake = 50,
        CancellationToken cancellationToken = default)
    {
        var response = await _adminGrowthService.GetProviderReactivationSegmentsAsync(
            new AdminProviderReactivationSegmentsQueryDto(
                AsOfUtc: asOfUtc,
                WarmFromDays: warmFromDays,
                ColdFromDays: coldFromDays,
                DormantFromDays: dormantFromDays,
                HibernatedFromDays: hibernatedFromDays,
                PreviewTake: previewTake),
            cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Retorna performance das campanhas de reativacao com taxa de retorno por rodada.
    /// </summary>
    /// <param name="fromUtc">Data inicial opcional do recorte em UTC.</param>
    /// <param name="toUtc">Data final opcional do recorte em UTC.</param>
    /// <param name="take">Quantidade maxima de campanhas no retorno.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisicao.</param>
    /// <returns>Resumo consolidado e lista de campanhas com entrega/reativacao.</returns>
    [HttpGet("provider-reactivation/campaigns/performance")]
    [ProducesResponseType(typeof(AdminProviderReactivationCampaignPerformanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetProviderReactivationCampaignPerformance(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var response = await _adminGrowthService.GetProviderReactivationCampaignPerformanceAsync(
            new AdminProviderReactivationCampaignPerformanceQueryDto(
                FromUtc: fromUtc,
                ToUtc: toUtc,
                Take: take),
            cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Atualiza preferencia de opt-out/frequencia de reativacao para um prestador.
    /// </summary>
    /// <param name="request">Payload da preferencia (opt-out e limite semanal).</param>
    /// <param name="cancellationToken">Token de cancelamento da requisicao.</param>
    /// <returns>Snapshot persistido da preferencia de reativacao.</returns>
    [HttpPost("provider-reactivation/preferences")]
    [ProducesResponseType(typeof(AdminProviderReactivationPreferenceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpsertProviderReactivationPreference(
        [FromBody] AdminProviderReactivationPreferenceUpsertRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || request.ProviderId == Guid.Empty)
        {
            return BadRequest(new { errorCode = "invalid_request", errorMessage = "ProviderId valido e obrigatorio." });
        }

        var actorUserId = ResolveActorUserId();
        var actorEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "admin@consertapramim.local";
        var response = await _adminGrowthService.UpsertProviderReactivationPreferenceAsync(
            request,
            actorUserId,
            actorEmail,
            cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Executa rodada de campanha de reativacao com controle de cadencia.
    /// </summary>
    /// <param name="request">Parametros de execucao da campanha (cadencia, limite e segmento).</param>
    /// <param name="cancellationToken">Token de cancelamento da requisicao.</param>
    /// <returns>Resultado da rodada com status e lista de destinatarios selecionados.</returns>
    [HttpPost("provider-reactivation/campaigns/run")]
    [ProducesResponseType(typeof(AdminProviderReactivationCampaignRunResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RunProviderReactivationCampaign(
        [FromBody] AdminProviderReactivationCampaignRunRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return BadRequest(new { errorCode = "invalid_request", errorMessage = "Payload de campanha nao informado." });
        }

        var actorUserId = ResolveActorUserId();
        var actorEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "admin@consertapramim.local";
        var response = await _adminGrowthService.RunProviderReactivationCampaignAsync(
            request,
            actorUserId,
            actorEmail,
            cancellationToken);

        return Ok(response);
    }

    private Guid ResolveActorUserId()
    {
        var nameIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(nameIdRaw, out var actorUserId)
            ? actorUserId
            : Guid.Empty;
    }
}



