using System.Security.Claims;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Infrastructure.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace ConsertaPraMim.API.Controllers;

/// <summary>
/// Endpoints dedicados ao app mobile/web do prestador para operacao de pedidos e propostas.
/// </summary>
/// <remarks>
/// O contrato deste controlador e exclusivo para o canal mobile do prestador.
/// Nenhum endpoint aqui substitui contratos usados pelos portais Web, preservando isolamento entre canais.
/// </remarks>
[Authorize(Roles = "Provider")]
[ApiController]
[Route("api/mobile/provider")]
public class MobileProviderController : ControllerBase
{
    private const long ChecklistEvidenceMaxFileSizeBytes = 25_000_000;

    private static readonly HashSet<string> AllowedChatAttachmentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".mp4", ".webm", ".mov"
    };

    private static readonly HashSet<string> AllowedChecklistEvidenceExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".mp4", ".webm", ".mov"
    };

    private static readonly HashSet<string> AllowedChecklistEvidenceContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "video/mp4", "video/webm", "video/quicktime"
    };

    private readonly IMobileProviderService _mobileProviderService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IChatService _chatService;
    private readonly IZipGeocodingService _zipGeocodingService;
    private readonly IHubContext<ChatHub> _chatHubContext;

    public MobileProviderController(
        IMobileProviderService mobileProviderService,
        IFileStorageService fileStorageService,
        IChatService chatService,
        IZipGeocodingService zipGeocodingService,
        IHubContext<ChatHub> chatHubContext)
    {
        _mobileProviderService = mobileProviderService;
        _fileStorageService = fileStorageService;
        _chatService = chatService;
        _zipGeocodingService = zipGeocodingService;
        _chatHubContext = chatHubContext;
    }

    /// <summary>
    /// Retorna o dashboard operacional do prestador autenticado.
    /// </summary>
    /// <remarks>
    /// O payload consolida:
    /// <list type="bullet">
    /// <item><description>KPIs de operacao (oportunidades, propostas e agenda);</description></item>
    /// <item><description>lista de pedidos proximos para acao rapida;</description></item>
    /// <item><description>destaques de agenda com foco em pendencias de confirmacao.</description></item>
    /// </list>
    /// </remarks>
    /// <param name="takeNearbyRequests">Quantidade maxima de cards de pedidos proximos no dashboard.</param>
    /// <param name="takeAgenda">Quantidade maxima de cards de agenda no dashboard.</param>
    /// <response code="200">Dashboard retornado com sucesso.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider.</response>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(MobileProviderDashboardResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] int takeNearbyRequests = 20,
        [FromQuery] int takeAgenda = 10)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var payload = await _mobileProviderService.GetDashboardAsync(providerUserId, takeNearbyRequests, takeAgenda);
        return Ok(payload);
    }

    /// <summary>
    /// Retorna configuracoes de perfil operacional do prestador autenticado para o app.
    /// </summary>
    /// <remarks>
    /// Este endpoint reproduz no app as mesmas opcoes da tela de configuracoes do portal do prestador:
    /// <list type="bullet">
    /// <item><description>status operacional e atualizacao em tempo real;</description></item>
    /// <item><description>CEP base com coordenadas geograficas;</description></item>
    /// <item><description>raio de atendimento com limite do plano;</description></item>
    /// <item><description>especialidades permitidas e selecionadas conforme plano.</description></item>
    /// </list>
    /// </remarks>
    /// <response code="200">Configuracoes carregadas com sucesso.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider.</response>
    /// <response code="404">Perfil de prestador nao encontrado para o usuario autenticado.</response>
    [HttpGet("profile/settings")]
    [ProducesResponseType(typeof(MobileProviderProfileSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfileSettings()
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var settings = await _mobileProviderService.GetProfileSettingsAsync(providerUserId);
        if (settings == null)
        {
            return NotFound(new
            {
                errorCode = "mobile_provider_profile_not_found",
                message = "Perfil de prestador nao encontrado."
            });
        }

        return Ok(settings);
    }

    /// <summary>
    /// Atualiza configuracoes completas do perfil operacional do prestador no app.
    /// </summary>
    /// <remarks>
    /// Aplica as mesmas regras de negocio do portal:
    /// <list type="bullet">
    /// <item><description>respeito aos limites de raio e categorias do plano;</description></item>
    /// <item><description>validacao de CEP/coordenadas para base de atendimento;</description></item>
    /// <item><description>persistencia de especialidades e status operacional.</description></item>
    /// </list>
    /// </remarks>
    /// <param name="request">Payload completo de configuracoes operacionais do prestador.</param>
    /// <response code="200">Perfil atualizado com sucesso.</response>
    /// <response code="400">Dados invalidos ou violacao de regra de validacao.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider.</response>
    /// <response code="404">Perfil de prestador nao encontrado.</response>
    /// <response code="409">Conflito com regras de plano/compliance.</response>
    [HttpPut("profile/settings")]
    [ProducesResponseType(typeof(MobileProviderProfileSettingsOperationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateProfileSettings([FromBody] MobileProviderUpdateProfileSettingsRequestDto request)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var result = await _mobileProviderService.UpdateProfileSettingsAsync(providerUserId, request);
        if (!result.Success)
        {
            return MapProfileSettingsFailure(result);
        }

        if (Enum.IsDefined(typeof(ProviderOperationalStatus), request.OperationalStatus))
        {
            var status = (ProviderOperationalStatus)request.OperationalStatus;
            await _chatHubContext.Clients.Group(ChatHub.BuildProviderStatusGroup(providerUserId)).SendAsync("ReceiveProviderStatus", new
            {
                providerId = providerUserId,
                status = status.ToString(),
                updatedAt = DateTime.UtcNow
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Atualiza somente o status operacional do prestador e publica o evento em tempo real.
    /// </summary>
    /// <param name="request">Novo status operacional.</param>
    /// <response code="200">Status operacional atualizado com sucesso.</response>
    /// <response code="400">Status invalido ou operacao rejeitada.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider.</response>
    [HttpPut("profile/operational-status")]
    [ProducesResponseType(typeof(MobileProviderProfileSettingsOperationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateProfileOperationalStatus([FromBody] MobileProviderUpdateProfileOperationalStatusRequestDto request)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var result = await _mobileProviderService.UpdateProfileOperationalStatusAsync(providerUserId, request);
        if (!result.Success)
        {
            return MapProfileSettingsFailure(result);
        }

        if (Enum.IsDefined(typeof(ProviderOperationalStatus), request.OperationalStatus))
        {
            var status = (ProviderOperationalStatus)request.OperationalStatus;
            await _chatHubContext.Clients.Group(ChatHub.BuildProviderStatusGroup(providerUserId)).SendAsync("ReceiveProviderStatus", new
            {
                providerId = providerUserId,
                status = status.ToString(),
                updatedAt = DateTime.UtcNow
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Resolve CEP para coordenadas e endereco base no contexto do app do prestador.
    /// </summary>
    /// <param name="zipCode">CEP com ou sem mascara (8 digitos significativos).</param>
    /// <response code="200">CEP resolvido com sucesso.</response>
    /// <response code="400">CEP invalido.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider.</response>
    /// <response code="404">CEP nao encontrado.</response>
    [HttpGet("profile/resolve-zip")]
    [ProducesResponseType(typeof(MobileProviderResolveZipResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolveProfileZip([FromQuery] string zipCode)
    {
        if (!TryGetProviderUserId(out _))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        if (string.IsNullOrWhiteSpace(zipCode))
        {
            return BadRequest(new
            {
                errorCode = "mobile_provider_profile_invalid_zip",
                message = "Informe um CEP valido com 8 digitos."
            });
        }

        var resolved = await _zipGeocodingService.ResolveCoordinatesAsync(zipCode);
        if (resolved == null)
        {
            return NotFound(new
            {
                errorCode = "mobile_provider_profile_zip_not_found",
                message = "Nao foi possivel localizar esse CEP."
            });
        }

        var response = new MobileProviderResolveZipResponseDto(
            resolved.Value.NormalizedZip,
            resolved.Value.Latitude,
            resolved.Value.Longitude,
            BuildResolvedZipAddress(resolved.Value.Street, resolved.Value.City));

        return Ok(response);
    }

    /// <summary>
    /// Retorna mapa de cobertura operacional do prestador autenticado para o app mobile.
    /// </summary>
    /// <remarks>
    /// Endpoint dedicado ao app do prestador para renderizacao de mapa (ex.: dashboard mobile),
    /// mantendo isolamento em relacao aos contratos dos portais web.
    /// 
    /// Regras de negocio aplicadas:
    /// <list type="bullet">
    /// <item><description>usa base geografica do prestador (CEP/base + latitude/longitude) cadastrada em perfil;</description></item>
    /// <item><description>expande o raio de busca para visao operacional do mapa sem alterar o raio comercial de matching;</description></item>
    /// <item><description>permite filtro por categoria e limite de distancia para reduzir ruido visual;</description></item>
    /// <item><description>pagina os pins para suportar volume alto sem degradar desempenho no app.</description></item>
    /// </list>
    /// 
    /// Quando o prestador nao possui base geografica cadastrada, a resposta retorna <c>hasBaseLocation=false</c>
    /// e lista de pins vazia, permitindo ao app exibir call-to-action para completar o perfil.
    /// </remarks>
    /// <param name="categoryFilter">Filtro opcional por categoria (normalizado internamente).</param>
    /// <param name="maxDistanceKm">Distancia maxima opcional em km para filtrar pins.</param>
    /// <param name="pinPage">Pagina de pins (1..N).</param>
    /// <param name="pinPageSize">Tamanho de pagina de pins (20..200).</param>
    /// <response code="200">Mapa de cobertura retornado com sucesso.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider.</response>
    [HttpGet("coverage-map")]
    [ProducesResponseType(typeof(MobileProviderCoverageMapDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCoverageMap(
        [FromQuery] string? categoryFilter = null,
        [FromQuery] double? maxDistanceKm = null,
        [FromQuery] int pinPage = 1,
        [FromQuery] int pinPageSize = 120)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var payload = await _mobileProviderService.GetCoverageMapAsync(
            providerUserId,
            categoryFilter,
            maxDistanceKm,
            pinPage,
            pinPageSize);

        return Ok(payload);
    }

    /// <summary>
    /// Lista pedidos proximos elegiveis para o prestador autenticado no app.
    /// </summary>
    /// <remarks>
    /// Este endpoint e dedicado ao app do prestador e aplica as regras atuais de matching por raio/categoria/perfil.
    /// </remarks>
    /// <param name="searchTerm">Filtro opcional de descricao/categoria conforme regra atual do matching.</param>
    /// <param name="take">Quantidade maxima de itens retornados.</param>
    /// <response code="200">Lista retornada com sucesso.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider.</response>
    [HttpGet("requests")]
    [ProducesResponseType(typeof(MobileProviderRequestsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetNearbyRequests(
        [FromQuery] string? searchTerm = null,
        [FromQuery] int take = 50)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var payload = await _mobileProviderService.GetNearbyRequestsAsync(providerUserId, searchTerm, take);
        return Ok(payload);
    }

    /// <summary>
    /// Retorna o detalhe de um pedido no contexto do app do prestador.
    /// </summary>
    /// <remarks>
    /// O detalhe inclui:
    /// <list type="bullet">
    /// <item><description>dados do pedido para tomada de decisao comercial;</description></item>
    /// <item><description>eventual proposta ja enviada pelo prestador autenticado;</description></item>
    /// <item><description>flag de elegibilidade para envio de nova proposta.</description></item>
    /// </list>
    /// </remarks>
    /// <param name="requestId">Identificador do pedido.</param>
    /// <response code="200">Detalhe retornado com sucesso.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider.</response>
    /// <response code="404">Pedido nao encontrado para o prestador autenticado.</response>
    [HttpGet("requests/{requestId:guid}")]
    [ProducesResponseType(typeof(MobileProviderRequestDetailsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRequestDetails([FromRoute] Guid requestId)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var payload = await _mobileProviderService.GetRequestDetailsAsync(providerUserId, requestId);
        if (payload == null)
        {
            return NotFound(new
            {
                errorCode = "mobile_provider_request_not_found",
                message = "Pedido nao encontrado para o prestador autenticado."
            });
        }

        return Ok(payload);
    }

    /// <summary>
    /// Lista propostas do prestador autenticado em contrato dedicado mobile.
    /// </summary>
    /// <param name="take">Quantidade maxima de propostas retornadas.</param>
    /// <response code="200">Propostas retornadas com sucesso.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider.</response>
    [HttpGet("proposals")]
    [ProducesResponseType(typeof(MobileProviderProposalsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyProposals([FromQuery] int take = 100)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var payload = await _mobileProviderService.GetMyProposalsAsync(providerUserId, take);
        return Ok(payload);
    }

    /// <summary>
    /// Cria um novo chamado de suporte para o prestador autenticado.
    /// </summary>
    /// <param name="request">Assunto, categoria, prioridade e mensagem inicial do chamado.</param>
    /// <response code="201">Chamado criado com sucesso.</response>
    /// <response code="400">Payload invalido.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider.</response>
    [HttpPost("support/tickets")]
    [ProducesResponseType(typeof(MobileProviderSupportTicketDetailsDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateSupportTicket([FromBody] MobileProviderCreateSupportTicketRequestDto request)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var result = await _mobileProviderService.CreateSupportTicketAsync(providerUserId, request);
        if (!result.Success || result.Ticket == null)
        {
            return MapSupportTicketFailure(result);
        }

        return CreatedAtAction(
            nameof(GetSupportTicketDetails),
            new { ticketId = result.Ticket.Ticket.Id },
            result.Ticket);
    }

    /// <summary>
    /// Lista chamados de suporte do prestador autenticado com filtros basicos e paginacao.
    /// </summary>
    /// <param name="status">Filtro opcional por status (nome ou valor numerico).</param>
    /// <param name="priority">Filtro opcional por prioridade (nome ou valor numerico).</param>
    /// <param name="search">Busca opcional por assunto/categoria.</param>
    /// <param name="page">Pagina atual (minimo 1).</param>
    /// <param name="pageSize">Itens por pagina (minimo 1, maximo 100).</param>
    /// <response code="200">Lista de chamados retornada com sucesso.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider.</response>
    [HttpGet("support/tickets")]
    [ProducesResponseType(typeof(MobileProviderSupportTicketListResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSupportTickets(
        [FromQuery] string? status = null,
        [FromQuery] string? priority = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var payload = await _mobileProviderService.GetSupportTicketsAsync(
            providerUserId,
            new MobileProviderSupportTicketListQueryDto(status, priority, search, page, pageSize));

        return Ok(payload);
    }

    /// <summary>
    /// Retorna o detalhe de um chamado do prestador autenticado com historico de mensagens.
    /// </summary>
    /// <param name="ticketId">Identificador do chamado.</param>
    /// <response code="200">Detalhe retornado com sucesso.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider.</response>
    /// <response code="404">Chamado nao encontrado.</response>
    [HttpGet("support/tickets/{ticketId:guid}")]
    [ProducesResponseType(typeof(MobileProviderSupportTicketDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSupportTicketDetails([FromRoute] Guid ticketId)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var result = await _mobileProviderService.GetSupportTicketDetailsAsync(providerUserId, ticketId);
        if (!result.Success || result.Ticket == null)
        {
            return MapSupportTicketFailure(result);
        }

        return Ok(result.Ticket);
    }

    /// <summary>
    /// Adiciona uma nova mensagem do prestador em um chamado aberto.
    /// </summary>
    /// <param name="ticketId">Identificador do chamado.</param>
    /// <param name="request">Mensagem a ser adicionada.</param>
    /// <response code="200">Mensagem registrada com sucesso.</response>
    /// <response code="400">Payload invalido.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider.</response>
    /// <response code="404">Chamado nao encontrado.</response>
    /// <response code="409">Chamado em status invalido para envio de mensagem.</response>
    [HttpPost("support/tickets/{ticketId:guid}/messages")]
    [ProducesResponseType(typeof(MobileProviderSupportTicketDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddSupportTicketMessage(
        [FromRoute] Guid ticketId,
        [FromBody] MobileProviderSupportTicketMessageRequestDto request)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var result = await _mobileProviderService.AddSupportTicketMessageAsync(providerUserId, ticketId, request);
        if (!result.Success || result.Ticket == null)
        {
            return MapSupportTicketFailure(result);
        }

        return Ok(result.Ticket);
    }

    /// <summary>
    /// Fecha um chamado em aberto no contexto do prestador autenticado.
    /// </summary>
    /// <param name="ticketId">Identificador do chamado.</param>
    /// <response code="200">Chamado fechado com sucesso.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider.</response>
    /// <response code="404">Chamado nao encontrado.</response>
    /// <response code="409">Chamado em status invalido para fechamento.</response>
    [HttpPost("support/tickets/{ticketId:guid}/close")]
    [ProducesResponseType(typeof(MobileProviderSupportTicketDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CloseSupportTicket([FromRoute] Guid ticketId)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var result = await _mobileProviderService.CloseSupportTicketAsync(providerUserId, ticketId);
        if (!result.Success || result.Ticket == null)
        {
            return MapSupportTicketFailure(result);
        }

        return Ok(result.Ticket);
    }

    /// <summary>
    /// Retorna agenda operacional do prestador para o app mobile, com pendencias e proximas visitas.
    /// </summary>
    /// <remarks>
    /// Regras principais:
    /// <list type="bullet">
    /// <item><description>Dados filtrados para o prestador autenticado;</description></item>
    /// <item><description>Separacao em <c>pendingItems</c> (acao do prestador) e <c>upcomingItems</c>;</description></item>
    /// <item><description><c>statusFilter</c> aceita: <c>all</c>, <c>pending</c>, <c>upcoming</c> ou status especifico.</description></item>
    /// </list>
    /// </remarks>
    /// <param name="fromUtc">Data/hora inicial opcional do recorte.</param>
    /// <param name="toUtc">Data/hora final opcional do recorte.</param>
    /// <param name="statusFilter">Filtro opcional por grupo/status.</param>
    /// <param name="take">Quantidade maxima por grupo retornado.</param>
    /// <response code="200">Agenda retornada com sucesso.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider.</response>
    [HttpGet("agenda")]
    [ProducesResponseType(typeof(MobileProviderAgendaResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAgenda(
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] string? statusFilter = null,
        [FromQuery] int take = 50)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var payload = await _mobileProviderService.GetAgendaAsync(providerUserId, fromUtc, toUtc, statusFilter, take);
        return Ok(payload);
    }

    /// <summary>
    /// Confirma agendamento pendente de confirmacao no app do prestador.
    /// </summary>
    /// <param name="appointmentId">Identificador do agendamento.</param>
    /// <response code="200">Agendamento confirmado com sucesso.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider ou sem permissao para o agendamento.</response>
    /// <response code="404">Agendamento nao encontrado.</response>
    /// <response code="409">Agendamento em estado invalido para confirmacao.</response>
    [HttpPost("agenda/{appointmentId:guid}/confirm")]
    [ProducesResponseType(typeof(MobileProviderAgendaOperationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmAgendaAppointment([FromRoute] Guid appointmentId)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var result = await _mobileProviderService.ConfirmAgendaAppointmentAsync(providerUserId, appointmentId);
        if (result.Success)
        {
            return Ok(result);
        }

        return MapAgendaFailure(result);
    }

    /// <summary>
    /// Recusa agendamento pendente no app do prestador.
    /// </summary>
    /// <param name="appointmentId">Identificador do agendamento.</param>
    /// <param name="request">Motivo da recusa.</param>
    /// <response code="200">Agendamento recusado com sucesso.</response>
    /// <response code="400">Motivo nao informado ou payload invalido.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider ou sem permissao para o agendamento.</response>
    /// <response code="404">Agendamento nao encontrado.</response>
    /// <response code="409">Agendamento em estado invalido para recusa.</response>
    [HttpPost("agenda/{appointmentId:guid}/reject")]
    [ProducesResponseType(typeof(MobileProviderAgendaOperationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RejectAgendaAppointment(
        [FromRoute] Guid appointmentId,
        [FromBody] MobileProviderRejectAgendaRequestDto request)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var result = await _mobileProviderService.RejectAgendaAppointmentAsync(providerUserId, appointmentId, request);
        if (result.Success)
        {
            return Ok(result);
        }

        return MapAgendaFailure(result);
    }

    /// <summary>
    /// Responde solicitacao de reagendamento iniciada pelo cliente.
    /// </summary>
    /// <remarks>
    /// Quando <c>accept=true</c>, o prestador confirma a nova janela proposta.
    /// Quando <c>accept=false</c>, a solicitacao e recusada e o motivo pode ser informado.
    /// </remarks>
    /// <param name="appointmentId">Identificador do agendamento.</param>
    /// <param name="request">Acao de aceite/recusa do reagendamento.</param>
    /// <response code="200">Resposta de reagendamento registrada com sucesso.</response>
    /// <response code="400">Payload invalido para a operacao.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider ou sem permissao para o agendamento.</response>
    /// <response code="404">Agendamento nao encontrado.</response>
    /// <response code="409">Agendamento em estado invalido para resposta de reagendamento.</response>
    [HttpPost("agenda/{appointmentId:guid}/reschedule/respond")]
    [ProducesResponseType(typeof(MobileProviderAgendaOperationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RespondAgendaReschedule(
        [FromRoute] Guid appointmentId,
        [FromBody] MobileProviderRespondRescheduleRequestDto request)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var result = await _mobileProviderService.RespondAgendaRescheduleAsync(providerUserId, appointmentId, request);
        if (result.Success)
        {
            return Ok(result);
        }

        return MapAgendaFailure(result);
    }

    /// <summary>
    /// Registra check-in de chegada do prestador para um agendamento no app mobile.
    /// </summary>
    /// <param name="appointmentId">Identificador do agendamento.</param>
    /// <param name="request">Dados de geolocalizacao e motivo manual opcional.</param>
    /// <response code="200">Chegada registrada com sucesso.</response>
    /// <response code="400">Dados invalidos para check-in.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario sem permissao para o agendamento.</response>
    /// <response code="404">Agendamento nao encontrado.</response>
    /// <response code="409">Agendamento em estado invalido para check-in.</response>
    [HttpPost("agenda/{appointmentId:guid}/arrive")]
    [ProducesResponseType(typeof(MobileProviderAgendaOperationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarkAgendaArrival(
        [FromRoute] Guid appointmentId,
        [FromBody] MobileProviderMarkArrivalRequestDto request)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var result = await _mobileProviderService.MarkAgendaArrivalAsync(providerUserId, appointmentId, request);
        if (result.Success)
        {
            return Ok(result);
        }

        return MapAgendaFailure(result);
    }

    /// <summary>
    /// Inicia a execucao do atendimento no agendamento informado.
    /// </summary>
    /// <param name="appointmentId">Identificador do agendamento.</param>
    /// <param name="request">Observacao opcional de inicio.</param>
    /// <response code="200">Atendimento iniciado com sucesso.</response>
    /// <response code="400">Dados invalidos para inicio.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario sem permissao para o agendamento.</response>
    /// <response code="404">Agendamento nao encontrado.</response>
    /// <response code="409">Agendamento em estado invalido para inicio.</response>
    [HttpPost("agenda/{appointmentId:guid}/start")]
    [ProducesResponseType(typeof(MobileProviderAgendaOperationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> StartAgendaExecution(
        [FromRoute] Guid appointmentId,
        [FromBody] MobileProviderStartExecutionRequestDto request)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var result = await _mobileProviderService.StartAgendaExecutionAsync(providerUserId, appointmentId, request);
        if (result.Success)
        {
            return Ok(result);
        }

        return MapAgendaFailure(result);
    }

    /// <summary>
    /// Atualiza o status operacional da visita no app do prestador.
    /// </summary>
    /// <param name="appointmentId">Identificador do agendamento.</param>
    /// <param name="request">Novo status operacional e motivo opcional.</param>
    /// <response code="200">Status operacional atualizado com sucesso.</response>
    /// <response code="400">Dados invalidos para atualizacao.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario sem permissao para o agendamento.</response>
    /// <response code="404">Agendamento nao encontrado.</response>
    /// <response code="409">Agendamento em estado invalido para atualizacao.</response>
    [HttpPost("agenda/{appointmentId:guid}/operational-status")]
    [ProducesResponseType(typeof(MobileProviderAgendaOperationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateAgendaOperationalStatus(
        [FromRoute] Guid appointmentId,
        [FromBody] MobileProviderUpdateOperationalStatusRequestDto request)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var result = await _mobileProviderService.UpdateAgendaOperationalStatusAsync(providerUserId, appointmentId, request);
        if (result.Success)
        {
            return Ok(result);
        }

        return MapAgendaFailure(result);
    }

    /// <summary>
    /// Retorna checklist tecnico do agendamento para operacao no app do prestador.
    /// </summary>
    /// <param name="appointmentId">Identificador do agendamento.</param>
    /// <response code="200">Checklist retornado com sucesso.</response>
    /// <response code="400">Checklist indisponivel para o contexto atual.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario sem permissao para acessar checklist.</response>
    /// <response code="404">Agendamento nao encontrado.</response>
    /// <response code="409">Checklist indisponivel para o estado atual.</response>
    [HttpGet("agenda/{appointmentId:guid}/checklist")]
    [ProducesResponseType(typeof(ServiceAppointmentChecklistDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetAgendaChecklist([FromRoute] Guid appointmentId)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var result = await _mobileProviderService.GetAppointmentChecklistAsync(providerUserId, appointmentId);
        if (result.Success && result.Checklist != null)
        {
            return Ok(result.Checklist);
        }

        return MapChecklistFailure(result);
    }

    /// <summary>
    /// Atualiza resposta de item de checklist no app do prestador.
    /// </summary>
    /// <param name="appointmentId">Identificador do agendamento.</param>
    /// <param name="request">Payload do item, marcacao, observacao e evidencia opcional.</param>
    /// <response code="200">Checklist atualizado com sucesso.</response>
    /// <response code="400">Item/payload invalido.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario sem permissao para editar checklist.</response>
    /// <response code="404">Agendamento nao encontrado.</response>
    /// <response code="409">Checklist indisponivel para o estado atual.</response>
    [HttpPost("agenda/{appointmentId:guid}/checklist/items")]
    [ProducesResponseType(typeof(ServiceAppointmentChecklistDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateAgendaChecklistItem(
        [FromRoute] Guid appointmentId,
        [FromBody] MobileProviderChecklistItemUpsertRequestDto request)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var result = await _mobileProviderService.UpdateAppointmentChecklistItemAsync(providerUserId, appointmentId, request);
        if (result.Success && result.Checklist != null)
        {
            return Ok(result.Checklist);
        }

        return MapChecklistFailure(result);
    }

    /// <summary>
    /// Realiza upload de evidencia (foto/video) para checklist no app do prestador.
    /// </summary>
    /// <remarks>
    /// Regras atuais:
    /// <list type="bullet">
    /// <item><description>tamanho maximo: 25MB;</description></item>
    /// <item><description>somente tipos/assinaturas de imagem e video suportados.</description></item>
    /// </list>
    /// </remarks>
    /// <param name="appointmentId">Identificador do agendamento.</param>
    /// <param name="file">Arquivo de evidencia para upload.</param>
    /// <response code="200">Upload concluido com sucesso.</response>
    /// <response code="400">Arquivo invalido para evidencia.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Prestador sem acesso ao checklist do agendamento.</response>
    /// <response code="404">Agendamento nao encontrado.</response>
    /// <response code="409">Checklist indisponivel para o estado atual.</response>
    [HttpPost("agenda/checklist-evidences/upload")]
    [RequestSizeLimit(120_000_000)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UploadAgendaChecklistEvidence([FromForm] Guid appointmentId, [FromForm] IFormFile? file)
    {
        if (appointmentId == Guid.Empty)
        {
            return BadRequest(new
            {
                errorCode = "invalid_request",
                message = "Agendamento invalido para upload de evidencia."
            });
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new
            {
                errorCode = "mobile_provider_checklist_evidence_required",
                message = "Selecione um arquivo de evidencia."
            });
        }

        if (file.Length > ChecklistEvidenceMaxFileSizeBytes)
        {
            return BadRequest(new
            {
                errorCode = "mobile_provider_checklist_evidence_too_large",
                message = "Arquivo de evidencia acima de 25MB."
            });
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedChecklistEvidenceExtensions.Contains(extension))
        {
            return BadRequest(new
            {
                errorCode = "mobile_provider_checklist_evidence_invalid_extension",
                message = "Extensao de evidencia nao permitida."
            });
        }

        if (string.IsNullOrWhiteSpace(file.ContentType) || !AllowedChecklistEvidenceContentTypes.Contains(file.ContentType))
        {
            return BadRequest(new
            {
                errorCode = "mobile_provider_checklist_evidence_invalid_content_type",
                message = "Tipo de conteudo da evidencia nao permitido."
            });
        }

        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var checklistAccess = await _mobileProviderService.GetAppointmentChecklistAsync(providerUserId, appointmentId);
        if (!checklistAccess.Success)
        {
            return MapChecklistFailure(checklistAccess);
        }

        await using var stream = file.OpenReadStream();
        if (!IsSupportedChecklistEvidenceSignature(stream, file.ContentType))
        {
            return BadRequest(new
            {
                errorCode = "mobile_provider_checklist_evidence_invalid_signature",
                message = "Arquivo de evidencia com assinatura invalida."
            });
        }

        stream.Position = 0;
        var relativeUrl = await _fileStorageService.SaveFileAsync(stream, file.FileName, "service-checklists");
        var absoluteUrl = $"{Request.Scheme}://{Request.Host}{relativeUrl}";

        return Ok(new
        {
            fileUrl = absoluteUrl,
            fileName = Path.GetFileName(file.FileName),
            contentType = file.ContentType,
            sizeBytes = file.Length
        });
    }

    /// <summary>
    /// Lista conversas ativas do prestador autenticado para o app mobile.
    /// </summary>
    /// <remarks>
    /// O retorno inclui dados de contraparte, ultima mensagem, quantidade de nao lidas
    /// e sinalizacao de presenca/status quando disponivel.
    /// </remarks>
    /// <response code="200">Conversas retornadas com sucesso.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider.</response>
    [HttpGet("chats")]
    [ProducesResponseType(typeof(MobileProviderChatConversationsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetChatConversations()
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var payload = await _mobileProviderService.GetChatConversationsAsync(providerUserId);
        return Ok(payload);
    }

    /// <summary>
    /// Retorna o historico de mensagens da conversa entre prestador autenticado e cliente no pedido informado.
    /// </summary>
    /// <param name="requestId">Identificador do pedido/conversa.</param>
    /// <response code="200">Historico retornado com sucesso.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider.</response>
    [HttpGet("chats/{requestId:guid}/messages")]
    [ProducesResponseType(typeof(MobileProviderChatMessagesResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetChatMessages([FromRoute] Guid requestId)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var payload = await _mobileProviderService.GetChatMessagesAsync(providerUserId, requestId);
        return Ok(payload);
    }

    /// <summary>
    /// Envia mensagem de chat do prestador no contexto do pedido informado.
    /// </summary>
    /// <param name="requestId">Identificador do pedido/conversa.</param>
    /// <param name="request">Payload com texto e anexos (opcionais).</param>
    /// <response code="200">Mensagem enviada com sucesso.</response>
    /// <response code="400">Payload invalido ou conversa indisponivel.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider.</response>
    [HttpPost("chats/{requestId:guid}/messages")]
    [ProducesResponseType(typeof(MobileProviderSendChatMessageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SendChatMessage(
        [FromRoute] Guid requestId,
        [FromBody] MobileProviderSendChatMessageRequestDto request)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var result = await _mobileProviderService.SendChatMessageAsync(providerUserId, requestId, request);
        if (result.Success)
        {
            if (result.Message != null)
            {
                await _chatHubContext.Clients.Group(BuildConversationGroup(requestId, providerUserId))
                    .SendAsync("ReceiveChatMessage", result.Message);

                var recipientId = await _chatService.ResolveRecipientIdAsync(requestId, providerUserId, providerUserId);
                if (recipientId.HasValue && recipientId.Value != providerUserId)
                {
                    await _chatHubContext.Clients.Group(BuildUserGroup(recipientId.Value))
                        .SendAsync("ReceiveChatMessage", result.Message);
                }
            }

            return Ok(result);
        }

        return BadRequest(new
        {
            errorCode = result.ErrorCode ?? "mobile_provider_chat_send_failed",
            message = result.ErrorMessage ?? "Nao foi possivel enviar a mensagem."
        });
    }

    /// <summary>
    /// Marca mensagens da conversa como entregues para o prestador autenticado.
    /// </summary>
    /// <param name="requestId">Identificador do pedido/conversa.</param>
    /// <response code="200">Recibos atualizados com sucesso.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider.</response>
    [HttpPost("chats/{requestId:guid}/delivered")]
    [ProducesResponseType(typeof(MobileProviderChatReceiptOperationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> MarkChatDelivered([FromRoute] Guid requestId)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var result = await _mobileProviderService.MarkChatConversationDeliveredAsync(providerUserId, requestId);
        foreach (var receipt in result.Receipts)
        {
            await _chatHubContext.Clients.Group(BuildConversationGroup(receipt.RequestId, receipt.ProviderId))
                .SendAsync("ReceiveMessageReceiptUpdated", receipt);
        }

        return Ok(result);
    }

    /// <summary>
    /// Marca mensagens da conversa como lidas para o prestador autenticado.
    /// </summary>
    /// <param name="requestId">Identificador do pedido/conversa.</param>
    /// <response code="200">Recibos atualizados com sucesso.</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider.</response>
    [HttpPost("chats/{requestId:guid}/read")]
    [ProducesResponseType(typeof(MobileProviderChatReceiptOperationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> MarkChatRead([FromRoute] Guid requestId)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var result = await _mobileProviderService.MarkChatConversationReadAsync(providerUserId, requestId);
        foreach (var receipt in result.Receipts)
        {
            await _chatHubContext.Clients.Group(BuildConversationGroup(receipt.RequestId, receipt.ProviderId))
                .SendAsync("ReceiveMessageReceiptUpdated", receipt);
        }

        return Ok(result);
    }

    /// <summary>
    /// Realiza upload de anexo para envio em conversa do app do prestador.
    /// </summary>
    /// <remarks>
    /// Tipos permitidos: imagens e videos suportados pelo chat.
    /// Limite de arquivo: 20MB.
    /// </remarks>
    /// <param name="requestId">Identificador do pedido/conversa.</param>
    /// <param name="file">Arquivo a ser enviado para armazenamento.</param>
    /// <response code="200">Upload concluido com sucesso.</response>
    /// <response code="400">Arquivo invalido (tipo/tamanho).</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Prestador sem acesso a conversa.</response>
    [HttpPost("chat-attachments/upload")]
    [RequestSizeLimit(50_000_000)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UploadChatAttachment([FromForm] Guid requestId, [FromForm] IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new
            {
                errorCode = "mobile_provider_chat_attachment_required",
                message = "Arquivo obrigatorio."
            });
        }

        if (!AllowedChatAttachmentExtensions.Contains(Path.GetExtension(file.FileName)))
        {
            return BadRequest(new
            {
                errorCode = "mobile_provider_chat_attachment_unsupported_type",
                message = "Tipo de arquivo nao suportado."
            });
        }

        if (file.Length > 20_000_000)
        {
            return BadRequest(new
            {
                errorCode = "mobile_provider_chat_attachment_too_large",
                message = "Arquivo excede o limite de 20MB."
            });
        }

        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var allowed = await _chatService.CanAccessConversationAsync(
            requestId,
            providerUserId,
            providerUserId,
            UserRole.Provider.ToString());
        if (!allowed)
        {
            return Forbid();
        }

        await using var stream = file.OpenReadStream();
        var relativeUrl = await _fileStorageService.SaveFileAsync(stream, file.FileName, "chat");
        var absoluteUrl = $"{Request.Scheme}://{Request.Host}{relativeUrl}";

        return Ok(new
        {
            fileUrl = absoluteUrl,
            fileName = file.FileName,
            contentType = file.ContentType,
            sizeBytes = file.Length
        });
    }

    /// <summary>
    /// Envia proposta comercial para um pedido no app do prestador.
    /// </summary>
    /// <remarks>
    /// Regras de negocio aplicadas:
    /// <list type="bullet">
    /// <item><description>pedido deve estar acessivel/elegivel para o prestador autenticado;</description></item>
    /// <item><description>nao pode existir proposta previa do mesmo prestador para o mesmo pedido;</description></item>
    /// <item><description>valor estimado, quando informado, nao pode ser negativo.</description></item>
    /// </list>
    /// </remarks>
    /// <param name="requestId">Identificador do pedido alvo.</param>
    /// <param name="request">Payload da proposta (valor estimado opcional e mensagem opcional).</param>
    /// <response code="200">Proposta criada com sucesso.</response>
    /// <response code="400">Payload invalido (ex.: valor negativo).</response>
    /// <response code="401">Token ausente/invalido ou claim de usuario indisponivel.</response>
    /// <response code="403">Usuario autenticado sem role Provider.</response>
    /// <response code="404">Pedido nao encontrado para o prestador autenticado.</response>
    /// <response code="409">Conflito de regra de negocio (pedido inelegivel/duplicidade de proposta).</response>
    [HttpPost("requests/{requestId:guid}/proposals")]
    [ProducesResponseType(typeof(MobileProviderCreateProposalResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateProposal(
        [FromRoute] Guid requestId,
        [FromBody] MobileProviderCreateProposalRequestDto request)
    {
        if (!TryGetProviderUserId(out var providerUserId))
        {
            return Unauthorized(new
            {
                errorCode = "mobile_provider_invalid_user_claim",
                message = "Nao foi possivel identificar o prestador autenticado."
            });
        }

        var result = await _mobileProviderService.CreateProposalAsync(providerUserId, requestId, request);
        if (result.Success && result.Payload != null)
        {
            return Ok(result.Payload);
        }

        return result.ErrorCode switch
        {
            "mobile_provider_request_not_found" => NotFound(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            "mobile_provider_proposal_already_exists" => Conflict(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            "mobile_provider_request_not_eligible_for_proposal" => Conflict(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            "mobile_provider_proposal_invalid_estimated_value" => BadRequest(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            _ => BadRequest(new
            {
                errorCode = result.ErrorCode ?? "mobile_provider_proposal_unknown_error",
                message = result.ErrorMessage ?? "Nao foi possivel enviar a proposta."
            })
        };
    }

    private static bool IsSupportedChecklistEvidenceSignature(Stream stream, string contentType)
    {
        if (!stream.CanRead || !stream.CanSeek)
        {
            return false;
        }

        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return IsSupportedImageSignature(stream);
        }

        if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        {
            return IsSupportedVideoSignature(stream);
        }

        return false;
    }

    private static bool IsSupportedImageSignature(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[12];
        var bytesRead = stream.Read(buffer);
        if (bytesRead < 12)
        {
            return false;
        }

        var isJpeg = buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF;
        var isPng = buffer[0] == 0x89 &&
                    buffer[1] == 0x50 &&
                    buffer[2] == 0x4E &&
                    buffer[3] == 0x47 &&
                    buffer[4] == 0x0D &&
                    buffer[5] == 0x0A &&
                    buffer[6] == 0x1A &&
                    buffer[7] == 0x0A;
        var isWebp = buffer[0] == 0x52 &&
                     buffer[1] == 0x49 &&
                     buffer[2] == 0x46 &&
                     buffer[3] == 0x46 &&
                     buffer[8] == 0x57 &&
                     buffer[9] == 0x45 &&
                     buffer[10] == 0x42 &&
                     buffer[11] == 0x50;

        return isJpeg || isPng || isWebp;
    }

    private static bool IsSupportedVideoSignature(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[16];
        var bytesRead = stream.Read(buffer);
        if (bytesRead < 12)
        {
            return false;
        }

        var isMp4OrMov = buffer[4] == 0x66 &&
                         buffer[5] == 0x74 &&
                         buffer[6] == 0x79 &&
                         buffer[7] == 0x70;

        var isWebm = buffer[0] == 0x1A &&
                     buffer[1] == 0x45 &&
                     buffer[2] == 0xDF &&
                     buffer[3] == 0xA3;

        return isMp4OrMov || isWebm;
    }

    private static string BuildConversationGroup(Guid requestId, Guid providerId)
    {
        return $"chat:{requestId:N}:{providerId:N}";
    }

    private static string BuildUserGroup(Guid userId)
    {
        return $"chat-user:{userId:N}";
    }

    private bool TryGetProviderUserId(out Guid providerUserId)
    {
        providerUserId = Guid.Empty;
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out providerUserId);
    }

    private static string BuildResolvedZipAddress(string? street, string? city)
    {
        var streetPart = string.IsNullOrWhiteSpace(street) ? null : street.Trim();
        var cityPart = string.IsNullOrWhiteSpace(city) ? null : city.Trim();

        if (streetPart is null && cityPart is null)
        {
            return "Localizacao encontrada com sucesso.";
        }

        if (streetPart is null)
        {
            return cityPart!;
        }

        if (cityPart is null)
        {
            return streetPart;
        }

        return $"{streetPart}, {cityPart}";
    }

    private IActionResult MapSupportTicketFailure(MobileProviderSupportTicketOperationResultDto result)
    {
        return result.ErrorCode switch
        {
            "mobile_provider_support_ticket_not_found" => NotFound(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            "mobile_provider_support_invalid_state" => Conflict(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            _ => BadRequest(new
            {
                errorCode = result.ErrorCode ?? "mobile_provider_support_unknown_error",
                message = result.ErrorMessage ?? "Nao foi possivel processar o chamado."
            })
        };
    }

    private IActionResult MapProfileSettingsFailure(MobileProviderProfileSettingsOperationResultDto result)
    {
        return result.ErrorCode switch
        {
            "mobile_provider_profile_not_found" => NotFound(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            "mobile_provider_profile_radius_exceeds_plan_limit" => Conflict(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            "mobile_provider_profile_categories_exceed_plan_limit" => Conflict(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            "mobile_provider_profile_category_not_allowed_by_plan" => Conflict(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            "mobile_provider_profile_update_rejected" => Conflict(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            _ => BadRequest(new
            {
                errorCode = result.ErrorCode ?? "mobile_provider_profile_unknown_error",
                message = result.ErrorMessage ?? "Nao foi possivel atualizar o perfil."
            })
        };
    }

    private IActionResult MapAgendaFailure(MobileProviderAgendaOperationResultDto result)
    {
        return result.ErrorCode switch
        {
            "mobile_provider_agenda_reject_reason_required" => BadRequest(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            "appointment_not_found" => NotFound(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            "forbidden" => StatusCode(StatusCodes.Status403Forbidden, new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            "invalid_state" => Conflict(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            "invalid_request" => BadRequest(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            _ => BadRequest(new
            {
                errorCode = result.ErrorCode ?? "mobile_provider_agenda_unknown_error",
                message = result.ErrorMessage ?? "Nao foi possivel processar a operacao de agenda."
            })
        };
    }

    private IActionResult MapChecklistFailure(MobileProviderChecklistResultDto result)
    {
        return result.ErrorCode switch
        {
            "appointment_not_found" => NotFound(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            "forbidden" => StatusCode(StatusCodes.Status403Forbidden, new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            "invalid_state" => Conflict(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            "checklist_not_configured" => Conflict(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            "evidence_required" => Conflict(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            "invalid_item" => BadRequest(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            "item_not_found" => BadRequest(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            "invalid_note" => BadRequest(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            "invalid_evidence" => BadRequest(new
            {
                errorCode = result.ErrorCode,
                message = result.ErrorMessage
            }),
            _ => BadRequest(new
            {
                errorCode = result.ErrorCode ?? "mobile_provider_checklist_unknown_error",
                message = result.ErrorMessage ?? "Nao foi possivel processar a operacao de checklist."
            })
        };
    }
}
