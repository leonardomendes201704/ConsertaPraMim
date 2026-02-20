using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin/monitoring")]
public class AdminMonitoringController : ControllerBase
{
    private readonly IAdminMonitoringService _adminMonitoringService;

    public AdminMonitoringController(IAdminMonitoringService adminMonitoringService)
    {
        _adminMonitoringService = adminMonitoringService;
    }

    /// <summary>
    /// Retorna configuracao runtime da telemetria de monitoramento.
    /// </summary>
    /// <returns>Estado atual da telemetria e timestamp da ultima alteracao.</returns>
    /// <response code="200">Configuracao retornada com sucesso.</response>
    [HttpGet("config")]
    public async Task<IActionResult> GetRuntimeConfig()
    {
        var result = await _adminMonitoringService.GetRuntimeConfigAsync(HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>
    /// Liga ou desliga a telemetria de monitoramento em runtime.
    /// </summary>
    /// <param name="request">Payload com estado desejado da telemetria.</param>
    /// <returns>Configuracao atualizada.</returns>
    /// <response code="200">Configuracao atualizada com sucesso.</response>
    [HttpPut("config/telemetry")]
    public async Task<IActionResult> SetTelemetryEnabled([FromBody] AdminMonitoringUpdateTelemetryRequestDto request)
    {
        var result = await _adminMonitoringService.SetTelemetryEnabledAsync(request.Enabled, HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>
    /// Retorna configuracao runtime das origins de CORS.
    /// </summary>
    /// <returns>Lista de origins permitidas e timestamp da ultima alteracao.</returns>
    /// <response code="200">Configuracao retornada com sucesso.</response>
    [HttpGet("config/cors")]
    public async Task<IActionResult> GetCorsConfig()
    {
        var result = await _adminMonitoringService.GetCorsConfigAsync(HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>
    /// Atualiza lista de origins permitidas para CORS em runtime.
    /// </summary>
    /// <param name="request">Payload com as origins permitidas.</param>
    /// <returns>Configuracao atualizada.</returns>
    /// <response code="200">Configuracao atualizada com sucesso.</response>
    [HttpPut("config/cors")]
    public async Task<IActionResult> SetCorsConfig([FromBody] AdminUpdateCorsConfigRequestDto request)
    {
        var result = await _adminMonitoringService.SetCorsConfigAsync(
            request.AllowedOrigins ?? Array.Empty<string>(),
            HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>
    /// Lista secoes de configuracao runtime persistidas em banco.
    /// </summary>
    /// <returns>Lista de secoes com JSON editavel.</returns>
    /// <response code="200">Configuracoes retornadas com sucesso.</response>
    [HttpGet("config/sections")]
    public async Task<IActionResult> GetConfigSections()
    {
        var result = await _adminMonitoringService.GetConfigSectionsAsync(HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>
    /// Atualiza o JSON de uma secao de configuracao runtime.
    /// </summary>
    /// <param name="sectionPath">Nome da secao (ex.: `Monitoring`, `Payments`).</param>
    /// <param name="request">Payload com JSON da secao.</param>
    /// <returns>Secao atualizada.</returns>
    /// <response code="200">Configuracao atualizada com sucesso.</response>
    /// <response code="400">Secao invalida ou JSON invalido.</response>
    [HttpPut("config/sections/{sectionPath}")]
    public async Task<IActionResult> SetConfigSection(
        [FromRoute] string sectionPath,
        [FromBody] AdminUpdateRuntimeConfigSectionRequestDto request)
    {
        try
        {
            var result = await _adminMonitoringService.SetConfigSectionAsync(
                sectionPath,
                request.JsonValue,
                HttpContext.RequestAborted);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Retorna a visao geral operacional da API no periodo informado.
    /// </summary>
    /// <param name="range">Janela predefinida: `1h`, `2h`, `4h`, `6h`, `8h`, `12h`, `24h`, `7d` ou `30d`.</param>
    /// <param name="endpoint">Filtro opcional por trecho de endpoint/template.</param>
    /// <param name="statusCode">Filtro opcional por status HTTP.</param>
    /// <param name="userId">Filtro opcional por usuario autenticado.</param>
    /// <param name="tenantId">Filtro opcional por tenant.</param>
    /// <param name="severity">Filtro opcional: `info`, `warn`, `error`.</param>
    /// <returns>KPIs e series temporais para dashboard de observabilidade.</returns>
    /// <response code="200">Resumo retornado com sucesso.</response>
    /// <response code="401">Token ausente/invalido.</response>
    /// <response code="403">Usuario sem permissao administrativa.</response>
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview(
        [FromQuery] string? range = "1h",
        [FromQuery] string? endpoint = null,
        [FromQuery] int? statusCode = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? tenantId = null,
        [FromQuery] string? severity = null)
    {
        var result = await _adminMonitoringService.GetOverviewAsync(
            new AdminMonitoringOverviewQueryDto(range, endpoint, statusCode, userId, tenantId, severity),
            HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>
    /// Retorna ranking dos endpoints mais acessados no periodo.
    /// </summary>
    /// <param name="range">Janela predefinida: `1h`, `2h`, `4h`, `6h`, `8h`, `12h`, `24h`, `7d` ou `30d`.</param>
    /// <param name="take">Quantidade maxima de registros (1 a 100).</param>
    /// <param name="endpoint">Filtro opcional por endpoint/template.</param>
    /// <param name="statusCode">Filtro opcional por status HTTP.</param>
    /// <param name="userId">Filtro opcional por usuario autenticado.</param>
    /// <param name="tenantId">Filtro opcional por tenant.</param>
    /// <param name="severity">Filtro opcional de severidade.</param>
    /// <returns>Lista ordenada de endpoints com hit count, erro e latencia.</returns>
    /// <response code="200">Ranking retornado com sucesso.</response>
    [HttpGet("top-endpoints")]
    public async Task<IActionResult> GetTopEndpoints(
        [FromQuery] string? range = "1h",
        [FromQuery] int take = 20,
        [FromQuery] string? endpoint = null,
        [FromQuery] int? statusCode = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? tenantId = null,
        [FromQuery] string? severity = null)
    {
        var result = await _adminMonitoringService.GetTopEndpointsAsync(
            new AdminMonitoringTopEndpointsQueryDto(range, take, endpoint, statusCode, userId, tenantId, severity),
            HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>
    /// Retorna serie temporal e percentis de latencia.
    /// </summary>
    /// <param name="endpoint">Template/trecho do endpoint alvo.</param>
    /// <param name="range">Janela predefinida: `1h`, `2h`, `4h`, `6h`, `8h`, `12h`, `24h`, `7d` ou `30d`.</param>
    /// <param name="statusCode">Filtro opcional por status HTTP.</param>
    /// <param name="userId">Filtro opcional por usuario autenticado.</param>
    /// <param name="tenantId">Filtro opcional por tenant.</param>
    /// <param name="severity">Filtro opcional de severidade.</param>
    /// <returns>Percentis p50/p95/p99 consolidados e por serie temporal.</returns>
    /// <response code="200">Latencia retornada com sucesso.</response>
    [HttpGet("latency")]
    public async Task<IActionResult> GetLatency(
        [FromQuery] string? endpoint = null,
        [FromQuery] string? range = "1h",
        [FromQuery] int? statusCode = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? tenantId = null,
        [FromQuery] string? severity = null)
    {
        var result = await _adminMonitoringService.GetLatencyAsync(
            new AdminMonitoringLatencyQueryDto(endpoint, range, statusCode, userId, tenantId, severity),
            HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>
    /// Retorna analytics de erros agregados por tipo, endpoint ou status.
    /// </summary>
    /// <param name="range">Janela predefinida: `1h`, `2h`, `4h`, `6h`, `8h`, `12h`, `24h`, `7d` ou `30d`.</param>
    /// <param name="groupBy">Agrupamento: `type`, `endpoint` ou `status`.</param>
    /// <param name="endpoint">Filtro opcional por endpoint/template.</param>
    /// <param name="statusCode">Filtro opcional por status HTTP.</param>
    /// <param name="userId">Filtro opcional por usuario autenticado.</param>
    /// <param name="tenantId">Filtro opcional por tenant.</param>
    /// <param name="severity">Filtro opcional de severidade.</param>
    /// <returns>Top erros e serie temporal de ocorrencias.</returns>
    /// <response code="200">Analise de erros retornada com sucesso.</response>
    [HttpGet("errors")]
    public async Task<IActionResult> GetErrors(
        [FromQuery] string? range = "1h",
        [FromQuery] string? groupBy = "type",
        [FromQuery] string? endpoint = null,
        [FromQuery] int? statusCode = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? tenantId = null,
        [FromQuery] string? severity = null)
    {
        var result = await _adminMonitoringService.GetErrorsAsync(
            new AdminMonitoringErrorsQueryDto(range, groupBy, endpoint, statusCode, userId, tenantId, severity),
            HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>
    /// Retorna a lista paginada de requests para drilldown tecnico.
    /// </summary>
    /// <param name="range">Janela predefinida: `1h`, `2h`, `4h`, `6h`, `8h`, `12h`, `24h`, `7d` ou `30d`.</param>
    /// <param name="endpoint">Filtro opcional por endpoint/template.</param>
    /// <param name="statusCode">Filtro opcional por status HTTP.</param>
    /// <param name="userId">Filtro opcional por usuario autenticado.</param>
    /// <param name="tenantId">Filtro opcional por tenant.</param>
    /// <param name="severity">Filtro opcional: `info`, `warn`, `error`.</param>
    /// <param name="search">Busca textual em correlationId, endpoint e erro normalizado.</param>
    /// <param name="page">Pagina (inicio em 1).</param>
    /// <param name="pageSize">Tamanho da pagina (1 a 200).</param>
    /// <returns>Requests filtrados para analise operacional detalhada.</returns>
    /// <response code="200">Lista retornada com sucesso.</response>
    [HttpGet("requests")]
    public async Task<IActionResult> GetRequests(
        [FromQuery] string? range = "1h",
        [FromQuery] string? endpoint = null,
        [FromQuery] int? statusCode = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? tenantId = null,
        [FromQuery] string? severity = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var result = await _adminMonitoringService.GetRequestsAsync(
            new AdminMonitoringRequestsQueryDto(range, endpoint, statusCode, userId, tenantId, severity, search, page, pageSize),
            HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>
    /// Exporta em CSV (base64) todos os requests filtrados no drilldown.
    /// </summary>
    /// <param name="range">Janela predefinida: `1h`, `2h`, `4h`, `6h`, `8h`, `12h`, `24h`, `7d` ou `30d`.</param>
    /// <param name="endpoint">Filtro opcional por endpoint/template.</param>
    /// <param name="statusCode">Filtro opcional por status HTTP.</param>
    /// <param name="userId">Filtro opcional por usuario autenticado.</param>
    /// <param name="tenantId">Filtro opcional por tenant.</param>
    /// <param name="severity">Filtro opcional: `info`, `warn`, `error`.</param>
    /// <param name="search">Busca textual em correlationId, endpoint e erro normalizado.</param>
    /// <returns>Objeto contendo nome do arquivo, content-type e CSV em base64.</returns>
    /// <response code="200">Exportacao gerada com sucesso.</response>
    [HttpGet("requests/export")]
    public async Task<IActionResult> ExportRequests(
        [FromQuery] string? range = "1h",
        [FromQuery] string? endpoint = null,
        [FromQuery] int? statusCode = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? tenantId = null,
        [FromQuery] string? severity = null,
        [FromQuery] string? search = null)
    {
        var result = await _adminMonitoringService.ExportRequestsCsvBase64Async(
            new AdminMonitoringRequestsQueryDto(
                range,
                endpoint,
                statusCode,
                userId,
                tenantId,
                severity,
                search,
                Page: 1,
                PageSize: 1),
            HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>
    /// Retorna o detalhe tecnico de um request identificado por correlationId.
    /// </summary>
    /// <param name="correlationId">CorrelationId do request registrado na telemetria.</param>
    /// <returns>Payload detalhado do request/response monitorado.</returns>
    /// <response code="200">Detalhe encontrado.</response>
    /// <response code="404">CorrelationId nao encontrado na janela de retencao.</response>
    [HttpGet("request/{correlationId}")]
    public async Task<IActionResult> GetRequestByCorrelationId([FromRoute] string correlationId)
    {
        var result = await _adminMonitoringService.GetRequestByCorrelationIdAsync(correlationId, HttpContext.RequestAborted);
        return result == null ? NotFound() : Ok(result);
    }
}

