using System.Security.Claims;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConsertaPraMim.API.Controllers;

[Authorize]
[ApiController]
[Route("api/service-appointments")]
public class ServiceAppointmentsController : ControllerBase
{
    private static readonly HashSet<string> AllowedMediaAttachmentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".mp4", ".webm", ".mov"
    };

    private readonly IServiceAppointmentService _serviceAppointmentService;
    private readonly IServiceAppointmentChecklistService _serviceAppointmentChecklistService;
    private readonly IServiceFinancialPolicyCalculationService _serviceFinancialPolicyCalculationService;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<ServiceAppointmentsController> _logger;

    public ServiceAppointmentsController(
        IServiceAppointmentService serviceAppointmentService,
        IServiceAppointmentChecklistService serviceAppointmentChecklistService,
        IServiceFinancialPolicyCalculationService serviceFinancialPolicyCalculationService,
        IFileStorageService fileStorageService,
        ILogger<ServiceAppointmentsController>? logger = null)
    {
        _serviceAppointmentService = serviceAppointmentService;
        _serviceAppointmentChecklistService = serviceAppointmentChecklistService;
        _serviceFinancialPolicyCalculationService = serviceFinancialPolicyCalculationService;
        _fileStorageService = fileStorageService;
        _logger = logger ?? NullLogger<ServiceAppointmentsController>.Instance;
    }

    [HttpGet("slots")]
    public async Task<IActionResult> GetSlots([FromQuery] GetServiceAppointmentSlotsQueryDto query)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        LogAgendaOperationStart(
            operation: "GetSlots",
            actorUserId: actorUserId,
            actorRole: actorRole,
            appointmentId: null,
            serviceRequestId: null,
            providerId: query.ProviderId);

        var result = await _serviceAppointmentService.GetAvailableSlotsAsync(actorUserId, actorRole, query);
        if (result.Success)
        {
            LogAgendaOperationOutcome(
                operation: "GetSlots",
                actorUserId: actorUserId,
                actorRole: actorRole,
                isSuccess: true,
                appointmentId: null,
                serviceRequestId: null,
                providerId: query.ProviderId,
                slotsCount: result.Slots.Count);
            return Ok(result.Slots);
        }

        LogAgendaOperationOutcome(
            operation: "GetSlots",
            actorUserId: actorUserId,
            actorRole: actorRole,
            isSuccess: false,
            appointmentId: null,
            serviceRequestId: null,
            providerId: query.ProviderId,
            errorCode: result.ErrorCode);

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpGet("providers/{providerId:guid}/availability")]
    public async Task<IActionResult> GetProviderAvailability(Guid providerId)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.GetProviderAvailabilityOverviewAsync(actorUserId, actorRole, providerId);
        if (result.Success && result.Overview != null)
        {
            return Ok(result.Overview);
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("availability/rules")]
    public async Task<IActionResult> AddProviderAvailabilityRule([FromBody] CreateProviderAvailabilityRuleRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.AddProviderAvailabilityRuleAsync(actorUserId, actorRole, request);
        if (result.Success)
        {
            return Ok(new { success = true });
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpDelete("availability/rules/{ruleId:guid}")]
    public async Task<IActionResult> RemoveProviderAvailabilityRule(Guid ruleId)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.RemoveProviderAvailabilityRuleAsync(actorUserId, actorRole, ruleId);
        if (result.Success)
        {
            return Ok(new { success = true });
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("availability/blocks")]
    public async Task<IActionResult> AddProviderAvailabilityBlock([FromBody] CreateProviderAvailabilityExceptionRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.AddProviderAvailabilityExceptionAsync(actorUserId, actorRole, request);
        if (result.Success)
        {
            return Ok(new { success = true });
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpDelete("availability/blocks/{exceptionId:guid}")]
    public async Task<IActionResult> RemoveProviderAvailabilityBlock(Guid exceptionId)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.RemoveProviderAvailabilityExceptionAsync(actorUserId, actorRole, exceptionId);
        if (result.Success)
        {
            return Ok(new { success = true });
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateServiceAppointmentRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        LogAgendaOperationStart(
            operation: "CreateAppointment",
            actorUserId: actorUserId,
            actorRole: actorRole,
            appointmentId: null,
            serviceRequestId: request.ServiceRequestId,
            providerId: request.ProviderId);

        var result = await _serviceAppointmentService.CreateAsync(actorUserId, actorRole, request);
        if (result.Success && result.Appointment != null)
        {
            LogAgendaOperationOutcome(
                operation: "CreateAppointment",
                actorUserId: actorUserId,
                actorRole: actorRole,
                isSuccess: true,
                appointmentId: result.Appointment.Id,
                serviceRequestId: result.Appointment.ServiceRequestId,
                providerId: result.Appointment.ProviderId);
            return CreatedAtAction(nameof(GetById), new { id = result.Appointment.Id }, result.Appointment);
        }

        LogAgendaOperationOutcome(
            operation: "CreateAppointment",
            actorUserId: actorUserId,
            actorRole: actorRole,
            isSuccess: false,
            appointmentId: null,
            serviceRequestId: request.ServiceRequestId,
            providerId: request.ProviderId,
            errorCode: result.ErrorCode);

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        LogAgendaOperationStart("ConfirmAppointment", actorUserId, actorRole, id);
        var result = await _serviceAppointmentService.ConfirmAsync(actorUserId, actorRole, id);
        if (result.Success && result.Appointment != null)
        {
            LogAgendaOperationOutcome(
                operation: "ConfirmAppointment",
                actorUserId: actorUserId,
                actorRole: actorRole,
                isSuccess: true,
                appointmentId: result.Appointment.Id,
                serviceRequestId: result.Appointment.ServiceRequestId,
                providerId: result.Appointment.ProviderId);
            return Ok(result.Appointment);
        }

        LogAgendaOperationOutcome(
            operation: "ConfirmAppointment",
            actorUserId: actorUserId,
            actorRole: actorRole,
            isSuccess: false,
            appointmentId: id,
            errorCode: result.ErrorCode);

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectServiceAppointmentRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.RejectAsync(actorUserId, actorRole, id, request);
        if (result.Success && result.Appointment != null)
        {
            return Ok(result.Appointment);
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/reschedule")]
    public async Task<IActionResult> RequestReschedule(Guid id, [FromBody] RequestServiceAppointmentRescheduleDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        LogAgendaOperationStart("RequestReschedule", actorUserId, actorRole, id);
        var result = await _serviceAppointmentService.RequestRescheduleAsync(actorUserId, actorRole, id, request);
        if (result.Success && result.Appointment != null)
        {
            LogAgendaOperationOutcome(
                operation: "RequestReschedule",
                actorUserId: actorUserId,
                actorRole: actorRole,
                isSuccess: true,
                appointmentId: result.Appointment.Id,
                serviceRequestId: result.Appointment.ServiceRequestId,
                providerId: result.Appointment.ProviderId);
            return Ok(result.Appointment);
        }

        LogAgendaOperationOutcome(
            operation: "RequestReschedule",
            actorUserId: actorUserId,
            actorRole: actorRole,
            isSuccess: false,
            appointmentId: id,
            errorCode: result.ErrorCode);

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/reschedule/respond")]
    public async Task<IActionResult> RespondReschedule(Guid id, [FromBody] RespondServiceAppointmentRescheduleRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        LogAgendaOperationStart("RespondReschedule", actorUserId, actorRole, id);
        var result = await _serviceAppointmentService.RespondRescheduleAsync(actorUserId, actorRole, id, request);
        if (result.Success && result.Appointment != null)
        {
            LogAgendaOperationOutcome(
                operation: "RespondReschedule",
                actorUserId: actorUserId,
                actorRole: actorRole,
                isSuccess: true,
                appointmentId: result.Appointment.Id,
                serviceRequestId: result.Appointment.ServiceRequestId,
                providerId: result.Appointment.ProviderId);
            return Ok(result.Appointment);
        }

        LogAgendaOperationOutcome(
            operation: "RespondReschedule",
            actorUserId: actorUserId,
            actorRole: actorRole,
            isSuccess: false,
            appointmentId: id,
            errorCode: result.ErrorCode);

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelServiceAppointmentRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        LogAgendaOperationStart("CancelAppointment", actorUserId, actorRole, id);
        var result = await _serviceAppointmentService.CancelAsync(actorUserId, actorRole, id, request);
        if (result.Success && result.Appointment != null)
        {
            LogAgendaOperationOutcome(
                operation: "CancelAppointment",
                actorUserId: actorUserId,
                actorRole: actorRole,
                isSuccess: true,
                appointmentId: result.Appointment.Id,
                serviceRequestId: result.Appointment.ServiceRequestId,
                providerId: result.Appointment.ProviderId);
            return Ok(result.Appointment);
        }

        LogAgendaOperationOutcome(
            operation: "CancelAppointment",
            actorUserId: actorUserId,
            actorRole: actorRole,
            isSuccess: false,
            appointmentId: id,
            errorCode: result.ErrorCode);

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/arrive")]
    public async Task<IActionResult> MarkArrived(Guid id, [FromBody] MarkServiceAppointmentArrivalRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        LogAgendaOperationStart("MarkArrived", actorUserId, actorRole, id);
        var result = await _serviceAppointmentService.MarkArrivedAsync(actorUserId, actorRole, id, request);
        if (result.Success && result.Appointment != null)
        {
            LogAgendaOperationOutcome(
                operation: "MarkArrived",
                actorUserId: actorUserId,
                actorRole: actorRole,
                isSuccess: true,
                appointmentId: result.Appointment.Id,
                serviceRequestId: result.Appointment.ServiceRequestId,
                providerId: result.Appointment.ProviderId);
            return Ok(result.Appointment);
        }

        LogAgendaOperationOutcome(
            operation: "MarkArrived",
            actorUserId: actorUserId,
            actorRole: actorRole,
            isSuccess: false,
            appointmentId: id,
            errorCode: result.ErrorCode);

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> StartExecution(Guid id, [FromBody] StartServiceAppointmentExecutionRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        LogAgendaOperationStart("StartExecution", actorUserId, actorRole, id);
        var result = await _serviceAppointmentService.StartExecutionAsync(actorUserId, actorRole, id, request);
        if (result.Success && result.Appointment != null)
        {
            LogAgendaOperationOutcome(
                operation: "StartExecution",
                actorUserId: actorUserId,
                actorRole: actorRole,
                isSuccess: true,
                appointmentId: result.Appointment.Id,
                serviceRequestId: result.Appointment.ServiceRequestId,
                providerId: result.Appointment.ProviderId);
            return Ok(result.Appointment);
        }

        LogAgendaOperationOutcome(
            operation: "StartExecution",
            actorUserId: actorUserId,
            actorRole: actorRole,
            isSuccess: false,
            appointmentId: id,
            errorCode: result.ErrorCode);

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/presence/respond")]
    public async Task<IActionResult> RespondPresence(Guid id, [FromBody] RespondServiceAppointmentPresenceRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        LogAgendaOperationStart("RespondPresence", actorUserId, actorRole, id);
        var result = await _serviceAppointmentService.RespondPresenceAsync(actorUserId, actorRole, id, request);
        if (result.Success && result.Appointment != null)
        {
            LogAgendaOperationOutcome(
                operation: "RespondPresence",
                actorUserId: actorUserId,
                actorRole: actorRole,
                isSuccess: true,
                appointmentId: result.Appointment.Id,
                serviceRequestId: result.Appointment.ServiceRequestId,
                providerId: result.Appointment.ProviderId);
            return Ok(result.Appointment);
        }

        LogAgendaOperationOutcome(
            operation: "RespondPresence",
            actorUserId: actorUserId,
            actorRole: actorRole,
            isSuccess: false,
            appointmentId: id,
            errorCode: result.ErrorCode);

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/operational-status")]
    public async Task<IActionResult> UpdateOperationalStatus(Guid id, [FromBody] UpdateServiceAppointmentOperationalStatusRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        LogAgendaOperationStart("UpdateOperationalStatus", actorUserId, actorRole, id);
        var result = await _serviceAppointmentService.UpdateOperationalStatusAsync(actorUserId, actorRole, id, request);
        if (result.Success && result.Appointment != null)
        {
            LogAgendaOperationOutcome(
                operation: "UpdateOperationalStatus",
                actorUserId: actorUserId,
                actorRole: actorRole,
                isSuccess: true,
                appointmentId: result.Appointment.Id,
                serviceRequestId: result.Appointment.ServiceRequestId,
                providerId: result.Appointment.ProviderId);
            return Ok(result.Appointment);
        }

        LogAgendaOperationOutcome(
            operation: "UpdateOperationalStatus",
            actorUserId: actorUserId,
            actorRole: actorRole,
            isSuccess: false,
            appointmentId: id,
            errorCode: result.ErrorCode);

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/scope-changes")]
    public async Task<IActionResult> CreateScopeChangeRequest(
        Guid id,
        [FromBody] CreateServiceScopeChangeRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.CreateScopeChangeRequestAsync(actorUserId, actorRole, id, request);
        if (result.Success && result.ScopeChangeRequest != null)
        {
            return Ok(result.ScopeChangeRequest);
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    /// <summary>
    /// Abre uma solicitacao de garantia para um agendamento concluido.
    /// </summary>
    /// <remarks>
    /// Regras principais:
    /// - Perfil permitido: <c>Client</c> (dono do agendamento) ou <c>Admin</c>.
    /// - O agendamento deve estar concluido.
    /// - O pedido deve estar em estado elegivel para garantia.
    /// - Nao pode existir outra solicitacao de garantia ativa para o mesmo agendamento.
    /// - A solicitacao registra SLA de resposta do prestador.
    /// </remarks>
    /// <param name="id">Identificador do agendamento.</param>
    /// <param name="request">Payload com a descricao detalhada do problema.</param>
    /// <returns>Solicitacao de garantia criada.</returns>
    /// <response code="200">Solicitacao de garantia criada com sucesso.</response>
    /// <response code="400">Dados invalidos (ex.: descricao vazia/invalida).</response>
    /// <response code="401">Token ausente ou invalido.</response>
    /// <response code="403">Usuario sem permissao para abrir garantia nesse agendamento.</response>
    /// <response code="404">Agendamento ou pedido nao encontrado.</response>
    /// <response code="409">Conflito de estado (ex.: garantia expirada ou ja existe garantia ativa).</response>
    [HttpPost("{id:guid}/warranty-claims")]
    [ProducesResponseType(typeof(ServiceWarrantyClaimDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateWarrantyClaim(
        Guid id,
        [FromBody] CreateServiceWarrantyClaimRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.CreateWarrantyClaimAsync(actorUserId, actorRole, id, request);
        if (result.Success && result.WarrantyClaim != null)
        {
            return Ok(result.WarrantyClaim);
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    /// <summary>
    /// Abre uma disputa formal para o agendamento informado.
    /// </summary>
    /// <remarks>
    /// Regras principais:
    /// - Perfis permitidos: <c>Client</c> ou <c>Provider</c> diretamente envolvidos no agendamento.
    /// - O tipo e o motivo devem seguir a taxonomia oficial de disputas.
    /// - A disputa cria um caso com SLA e prioridade inicial para mediacao.
    /// - O fluxo operacional/comercial do pedido pode ficar congelado enquanto houver disputa aberta.
    /// - O caso gera trilha de auditoria e notificacoes para administracao e contraparte.
    /// </remarks>
    /// <param name="id">Identificador do agendamento.</param>
    /// <param name="request">Payload com tipo, motivo e descricao da disputa.</param>
    /// <returns>Case de disputa criado.</returns>
    /// <response code="200">Disputa aberta com sucesso.</response>
    /// <response code="400">Payload invalido (tipo/motivo/descricao).</response>
    /// <response code="401">Token ausente ou invalido.</response>
    /// <response code="403">Usuario sem permissao para abrir disputa neste agendamento.</response>
    /// <response code="404">Agendamento nao encontrado.</response>
    /// <response code="409">Conflito de estado (ex.: disputa ja aberta para o mesmo agendamento).</response>
    [HttpPost("{id:guid}/disputes")]
    [ProducesResponseType(typeof(ServiceDisputeCaseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateDisputeCase(
        Guid id,
        [FromBody] CreateServiceDisputeCaseRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.CreateDisputeCaseAsync(actorUserId, actorRole, id, request);
        if (result.Success && result.DisputeCase != null)
        {
            return Ok(result.DisputeCase);
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    /// <summary>
    /// Faz upload de evidencia para uma disputa existente do agendamento.
    /// </summary>
    /// <remarks>
    /// Regras principais:
    /// - Perfis permitidos: <c>Client</c>, <c>Provider</c> e <c>Admin</c> com acesso ao agendamento/disputa.
    /// - Tipos permitidos: JPG, JPEG, PNG, WEBP, MP4, WEBM, MOV.
    /// - Tamanho maximo por arquivo: 25MB.
    /// - A disputa deve estar aberta/em analise.
    /// - Cada disputa aceita ate 20 anexos.
    /// - O envio gera trilha de auditoria e notificacoes para contraparte e administracao.
    /// </remarks>
    /// <param name="id">Identificador do agendamento.</param>
    /// <param name="disputeCaseId">Identificador da disputa.</param>
    /// <param name="request">Arquivo e mensagem opcional vinculada ao anexo.</param>
    /// <returns>Anexo registrado na disputa.</returns>
    /// <response code="200">Evidencia anexada com sucesso.</response>
    /// <response code="400">Arquivo invalido ou metadados inconsistentes.</response>
    /// <response code="401">Token ausente ou invalido.</response>
    /// <response code="403">Usuario sem permissao para anexar evidencia nesta disputa.</response>
    /// <response code="404">Agendamento ou disputa nao encontrados.</response>
    /// <response code="409">Conflito de estado (disputa encerrada ou limite de anexos atingido).</response>
    [HttpPost("{id:guid}/disputes/{disputeCaseId:guid}/attachments/upload")]
    [RequestSizeLimit(50_000_000)]
    [ProducesResponseType(typeof(ServiceDisputeCaseAttachmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UploadDisputeAttachment(
        Guid id,
        Guid disputeCaseId,
        [FromForm] DisputeAttachmentUploadRequest request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        if (request.File == null || request.File.Length == 0)
        {
            return BadRequest(new { errorCode = "invalid_attachment", message = "Arquivo obrigatorio." });
        }

        var extension = Path.GetExtension(request.File.FileName);
        if (!AllowedMediaAttachmentExtensions.Contains(extension))
        {
            return BadRequest(new { errorCode = "invalid_attachment", message = "Tipo de arquivo nao suportado." });
        }

        if (request.File.Length > 25_000_000)
        {
            return BadRequest(new { errorCode = "invalid_attachment_size", message = "Arquivo excede o limite de 25MB." });
        }

        await using var stream = request.File.OpenReadStream();
        var relativeUrl = await _fileStorageService.SaveFileAsync(stream, request.File.FileName, "disputes");
        var result = await _serviceAppointmentService.AddDisputeCaseAttachmentAsync(
            actorUserId,
            actorRole,
            id,
            disputeCaseId,
            new RegisterServiceDisputeAttachmentDto(
                relativeUrl,
                request.File.FileName,
                request.File.ContentType,
                request.File.Length,
                request.MessageText));

        if (result.Success && result.Attachment != null)
        {
            return Ok(result.Attachment);
        }

        _fileStorageService.DeleteFile(relativeUrl);
        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    /// <summary>
    /// Registra uma nova mensagem textual em uma disputa existente do agendamento.
    /// </summary>
    /// <remarks>
    /// Regras principais:
    /// - Perfis permitidos: <c>Client</c>, <c>Provider</c> e <c>Admin</c> com acesso ao agendamento/disputa.
    /// - A mensagem e obrigatoria e aceita ate 3000 caracteres.
    /// - A disputa deve estar aberta/em analise.
    /// - A mensagem gera trilha de auditoria de disputa e auditoria administrativa.
    /// - O envio dispara notificacao para contraparte e administracao.
    /// </remarks>
    /// <param name="id">Identificador do agendamento.</param>
    /// <param name="disputeCaseId">Identificador da disputa.</param>
    /// <param name="request">Payload com o texto da mensagem.</param>
    /// <returns>Mensagem registrada na disputa.</returns>
    /// <response code="200">Mensagem registrada com sucesso.</response>
    /// <response code="400">Payload invalido (mensagem vazia ou acima do limite).</response>
    /// <response code="401">Token ausente ou invalido.</response>
    /// <response code="403">Usuario sem permissao para comentar nesta disputa.</response>
    /// <response code="404">Agendamento ou disputa nao encontrados.</response>
    /// <response code="409">Conflito de estado (disputa encerrada).</response>
    [HttpPost("{id:guid}/disputes/{disputeCaseId:guid}/messages")]
    [ProducesResponseType(typeof(ServiceDisputeCaseMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddDisputeMessage(
        Guid id,
        Guid disputeCaseId,
        [FromBody] DisputeMessageRequest request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.AddDisputeCaseMessageAsync(
            actorUserId,
            actorRole,
            id,
            disputeCaseId,
            new CreateServiceDisputeMessageRequestDto(request.MessageText ?? string.Empty));

        if (result.Success && result.Message != null)
        {
            return Ok(result.Message);
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    /// <summary>
    /// Registra o aceite ou rejeicao da solicitacao de garantia pelo prestador.
    /// </summary>
    /// <remarks>
    /// Regras principais:
    /// - Perfil permitido: <c>Provider</c> (dono do agendamento) ou <c>Admin</c>.
    /// - A garantia deve estar em estado pendente de analise do prestador.
    /// - Em rejeicao, o motivo e obrigatorio e o caso e escalado automaticamente para administracao.
    /// - Em aceite, a garantia segue para agendamento de revisita.
    /// </remarks>
    /// <param name="id">Identificador do agendamento original.</param>
    /// <param name="warrantyClaimId">Identificador da solicitacao de garantia.</param>
    /// <param name="request">Payload com decisao do prestador e motivo opcional/obrigatorio conforme a decisao.</param>
    /// <returns>Solicitacao de garantia atualizada.</returns>
    /// <response code="200">Resposta de garantia registrada com sucesso.</response>
    /// <response code="400">Payload invalido (ex.: motivo obrigatorio ausente na rejeicao).</response>
    /// <response code="401">Token ausente ou invalido.</response>
    /// <response code="403">Usuario sem permissao para responder a garantia.</response>
    /// <response code="404">Agendamento ou garantia nao encontrados.</response>
    /// <response code="409">Conflito de estado da garantia.</response>
    [HttpPost("{id:guid}/warranty-claims/{warrantyClaimId:guid}/respond")]
    [ProducesResponseType(typeof(ServiceWarrantyClaimDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RespondWarrantyClaim(
        Guid id,
        Guid warrantyClaimId,
        [FromBody] RespondServiceWarrantyClaimRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.RespondWarrantyClaimAsync(
            actorUserId,
            actorRole,
            id,
            warrantyClaimId,
            request);
        if (result.Success && result.WarrantyClaim != null)
        {
            return Ok(result.WarrantyClaim);
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    /// <summary>
    /// Agenda a revisita de uma solicitacao de garantia.
    /// </summary>
    /// <remarks>
    /// Regras principais:
    /// - Perfil permitido: <c>Provider</c> (dono do agendamento original) ou <c>Admin</c>.
    /// - A garantia deve estar em estado pendente/aceita para agendamento da revisita.
    /// - A janela deve respeitar disponibilidade do prestador, bloqueios e conflitos de agenda.
    /// - Ao confirmar a revisita, a garantia passa para <c>RevisitScheduled</c> e o novo agendamento fica vinculado.
    /// </remarks>
    /// <param name="id">Identificador do agendamento original que originou a garantia.</param>
    /// <param name="warrantyClaimId">Identificador da solicitacao de garantia.</param>
    /// <param name="request">Janela da revisita e motivo opcional.</param>
    /// <returns>Payload com a garantia atualizada e o agendamento de revisita criado.</returns>
    /// <response code="200">Revisita de garantia agendada com sucesso.</response>
    /// <response code="400">Janela invalida ou dados inconsistentes.</response>
    /// <response code="401">Token ausente ou invalido.</response>
    /// <response code="403">Usuario sem permissao para agendar revisita.</response>
    /// <response code="404">Agendamento original ou garantia nao encontrados.</response>
    /// <response code="409">Conflito de estado/disponibilidade (ex.: garantia expirada, janela indisponivel).</response>
    [HttpPost("{id:guid}/warranty-claims/{warrantyClaimId:guid}/revisit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ScheduleWarrantyRevisit(
        Guid id,
        Guid warrantyClaimId,
        [FromBody] ScheduleServiceWarrantyRevisitRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.ScheduleWarrantyRevisitAsync(
            actorUserId,
            actorRole,
            id,
            warrantyClaimId,
            request);
        if (result.Success && result.WarrantyClaim != null && result.RevisitAppointment != null)
        {
            return Ok(new
            {
                warrantyClaim = result.WarrantyClaim,
                revisitAppointment = result.RevisitAppointment
            });
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/scope-changes/{scopeChangeRequestId:guid}/attachments/upload")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> UploadScopeChangeAttachment(
        Guid id,
        Guid scopeChangeRequestId,
        [FromForm] ScopeChangeAttachmentUploadRequest request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        if (request.File == null || request.File.Length == 0)
        {
            return BadRequest(new { errorCode = "invalid_attachment", message = "Arquivo obrigatorio." });
        }

        var extension = Path.GetExtension(request.File.FileName);
        if (!AllowedMediaAttachmentExtensions.Contains(extension))
        {
            return BadRequest(new { errorCode = "invalid_attachment", message = "Tipo de arquivo nao suportado." });
        }

        if (request.File.Length > 25_000_000)
        {
            return BadRequest(new { errorCode = "invalid_attachment_size", message = "Arquivo excede o limite de 25MB." });
        }

        await using var stream = request.File.OpenReadStream();
        var relativeUrl = await _fileStorageService.SaveFileAsync(stream, request.File.FileName, "scope-changes");
        var result = await _serviceAppointmentService.AddScopeChangeAttachmentAsync(
            actorUserId,
            actorRole,
            id,
            scopeChangeRequestId,
            new RegisterServiceScopeChangeAttachmentDto(
                relativeUrl,
                request.File.FileName,
                request.File.ContentType,
                request.File.Length));

        if (result.Success && result.Attachment != null)
        {
            return Ok(result.Attachment);
        }

        _fileStorageService.DeleteFile(relativeUrl);
        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/scope-changes/{scopeChangeRequestId:guid}/approve")]
    public async Task<IActionResult> ApproveScopeChange(Guid id, Guid scopeChangeRequestId)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.ApproveScopeChangeRequestAsync(
            actorUserId,
            actorRole,
            id,
            scopeChangeRequestId);
        if (result.Success && result.ScopeChangeRequest != null)
        {
            return Ok(result.ScopeChangeRequest);
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/scope-changes/{scopeChangeRequestId:guid}/reject")]
    public async Task<IActionResult> RejectScopeChange(
        Guid id,
        Guid scopeChangeRequestId,
        [FromBody] RejectServiceScopeChangeRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.RejectScopeChangeRequestAsync(
            actorUserId,
            actorRole,
            id,
            scopeChangeRequestId,
            request);
        if (result.Success && result.ScopeChangeRequest != null)
        {
            return Ok(result.ScopeChangeRequest);
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/completion/pin/generate")]
    public async Task<IActionResult> GenerateCompletionPin(
        Guid id,
        [FromBody] GenerateServiceCompletionPinRequestDto? request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var payload = request ?? new GenerateServiceCompletionPinRequestDto();
        var result = await _serviceAppointmentService.GenerateCompletionPinAsync(actorUserId, actorRole, id, payload);
        if (result.Success && result.Term != null)
        {
            return Ok(new
            {
                term = result.Term,
                oneTimePin = result.OneTimePin
            });
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/completion/pin/validate")]
    public async Task<IActionResult> ValidateCompletionPin(
        Guid id,
        [FromBody] ValidateServiceCompletionPinRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.ValidateCompletionPinAsync(actorUserId, actorRole, id, request);
        if (result.Success && result.Term != null)
        {
            return Ok(new
            {
                term = result.Term
            });
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpGet("{id:guid}/completion")]
    public async Task<IActionResult> GetCompletionTerm(Guid id)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.GetCompletionTermAsync(actorUserId, actorRole, id);
        if (result.Success && result.Term != null)
        {
            return Ok(new
            {
                term = result.Term
            });
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpGet("service-requests/{serviceRequestId:guid}/scope-changes")]
    public async Task<IActionResult> GetScopeChangesByServiceRequest(Guid serviceRequestId)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.GetScopeChangeRequestsByServiceRequestAsync(
            actorUserId,
            actorRole,
            serviceRequestId);
        return Ok(result);
    }

    [HttpGet("service-requests/{serviceRequestId:guid}/warranty-claims")]
    public async Task<IActionResult> GetWarrantyClaimsByServiceRequest(Guid serviceRequestId)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.GetWarrantyClaimsByServiceRequestAsync(
            actorUserId,
            actorRole,
            serviceRequestId);
        return Ok(result);
    }

    [HttpGet("service-requests/{serviceRequestId:guid}/disputes")]
    public async Task<IActionResult> GetDisputesByServiceRequest(Guid serviceRequestId)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.GetDisputeCasesByServiceRequestAsync(
            actorUserId,
            actorRole,
            serviceRequestId);
        return Ok(result);
    }

    [HttpPost("{id:guid}/completion/confirm")]
    public async Task<IActionResult> ConfirmCompletion(
        Guid id,
        [FromBody] ConfirmServiceCompletionRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.ConfirmCompletionAsync(actorUserId, actorRole, id, request);
        if (result.Success && result.Term != null)
        {
            return Ok(new
            {
                term = result.Term
            });
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/completion/contest")]
    public async Task<IActionResult> ContestCompletion(
        Guid id,
        [FromBody] ContestServiceCompletionRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.ContestCompletionAsync(actorUserId, actorRole, id, request);
        if (result.Success && result.Term != null)
        {
            return Ok(new
            {
                term = result.Term
            });
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        LogAgendaOperationStart("GetMineAppointments", actorUserId, actorRole);
        var appointments = await _serviceAppointmentService.GetMyAppointmentsAsync(actorUserId, actorRole, fromUtc, toUtc);
        LogAgendaOperationOutcome(
            operation: "GetMineAppointments",
            actorUserId: actorUserId,
            actorRole: actorRole,
            isSuccess: true,
            slotsCount: appointments.Count);
        return Ok(appointments);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        LogAgendaOperationStart("GetAppointmentById", actorUserId, actorRole, id);
        var result = await _serviceAppointmentService.GetByIdAsync(actorUserId, actorRole, id);
        if (result.Success && result.Appointment != null)
        {
            LogAgendaOperationOutcome(
                operation: "GetAppointmentById",
                actorUserId: actorUserId,
                actorRole: actorRole,
                isSuccess: true,
                appointmentId: result.Appointment.Id,
                serviceRequestId: result.Appointment.ServiceRequestId,
                providerId: result.Appointment.ProviderId);
            return Ok(result.Appointment);
        }

        LogAgendaOperationOutcome(
            operation: "GetAppointmentById",
            actorUserId: actorUserId,
            actorRole: actorRole,
            isSuccess: false,
            appointmentId: id,
            errorCode: result.ErrorCode);

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpGet("{id:guid}/checklist")]
    public async Task<IActionResult> GetChecklist(Guid id)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentChecklistService.GetChecklistAsync(actorUserId, actorRole, id);
        if (result.Success && result.Checklist != null)
        {
            return Ok(result.Checklist);
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{id:guid}/checklist/items/{itemId:guid}")]
    public async Task<IActionResult> UpsertChecklistItem(
        Guid id,
        Guid itemId,
        [FromBody] UpsertServiceChecklistItemResponseRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var payload = new UpsertServiceChecklistItemResponseRequestDto(
            itemId,
            request.IsChecked,
            request.Note,
            request.EvidenceUrl,
            request.EvidenceFileName,
            request.EvidenceContentType,
            request.EvidenceSizeBytes,
            request.ClearEvidence);

        var result = await _serviceAppointmentChecklistService.UpsertItemResponseAsync(actorUserId, actorRole, id, payload);
        if (result.Success && result.Checklist != null)
        {
            return Ok(result.Checklist);
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("financial-policy/simulate")]
    public async Task<IActionResult> SimulateFinancialPolicy([FromBody] ServiceFinancialCalculationRequestDto request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out _, out var actorRole))
        {
            return Unauthorized();
        }

        if (!CanSimulateEventType(actorRole, request.EventType))
        {
            return Forbid();
        }

        var result = await _serviceFinancialPolicyCalculationService.CalculateAsync(request, cancellationToken);
        if (result.Success && result.Breakdown != null)
        {
            return Ok(result.Breakdown);
        }

        return result.ErrorCode switch
        {
            "policy_rule_not_found" => NotFound(new { errorCode = result.ErrorCode, message = result.ErrorMessage }),
            "invalid_service_value" => BadRequest(new { errorCode = result.ErrorCode, message = result.ErrorMessage }),
            _ => BadRequest(new { errorCode = result.ErrorCode, message = result.ErrorMessage })
        };
    }

    [HttpPost("{id:guid}/financial-policy/override")]
    public async Task<IActionResult> OverrideFinancialPolicy(Guid id, [FromBody] ServiceFinancialPolicyOverrideRequestDto request)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _serviceAppointmentService.OverrideFinancialPolicyAsync(actorUserId, actorRole, id, request);
        if (result.Success && result.Appointment != null)
        {
            return Ok(result.Appointment);
        }

        return MapFailure(result.ErrorCode, result.ErrorMessage);
    }

    private void LogAgendaOperationStart(
        string operation,
        Guid actorUserId,
        string actorRole,
        Guid? appointmentId = null,
        Guid? serviceRequestId = null,
        Guid? providerId = null)
    {
        _logger.LogInformation(
            "Agenda operation started. Operation={Operation}, ActorUserId={ActorUserId}, ActorRole={ActorRole}, AppointmentId={AppointmentId}, ServiceRequestId={ServiceRequestId}, ProviderId={ProviderId}, CorrelationId={CorrelationId}",
            operation,
            actorUserId,
            actorRole,
            appointmentId,
            serviceRequestId,
            providerId,
            HttpContext.TraceIdentifier);
    }

    private void LogAgendaOperationOutcome(
        string operation,
        Guid actorUserId,
        string actorRole,
        bool isSuccess,
        Guid? appointmentId = null,
        Guid? serviceRequestId = null,
        Guid? providerId = null,
        string? errorCode = null,
        int? slotsCount = null)
    {
        if (isSuccess)
        {
            _logger.LogInformation(
                "Agenda operation succeeded. Operation={Operation}, ActorUserId={ActorUserId}, ActorRole={ActorRole}, AppointmentId={AppointmentId}, ServiceRequestId={ServiceRequestId}, ProviderId={ProviderId}, SlotsCount={SlotsCount}, CorrelationId={CorrelationId}",
                operation,
                actorUserId,
                actorRole,
                appointmentId,
                serviceRequestId,
                providerId,
                slotsCount,
                HttpContext.TraceIdentifier);

            return;
        }

        _logger.LogWarning(
            "Agenda operation failed. Operation={Operation}, ActorUserId={ActorUserId}, ActorRole={ActorRole}, AppointmentId={AppointmentId}, ServiceRequestId={ServiceRequestId}, ProviderId={ProviderId}, ErrorCode={ErrorCode}, CorrelationId={CorrelationId}",
            operation,
            actorUserId,
            actorRole,
            appointmentId,
            serviceRequestId,
            providerId,
            errorCode,
            HttpContext.TraceIdentifier);
    }

    private bool TryGetActor(out Guid actorUserId, out string actorRole)
    {
        actorUserId = Guid.Empty;
        actorRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

        var actorRaw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return !string.IsNullOrWhiteSpace(actorRaw) && Guid.TryParse(actorRaw, out actorUserId);
    }

    private IActionResult MapFailure(string? errorCode, string? message)
    {
        return errorCode switch
        {
            "forbidden" => Forbid(),
            "provider_not_found" => NotFound(new { errorCode, message }),
            "request_not_found" => NotFound(new { errorCode, message }),
            "appointment_not_found" => NotFound(new { errorCode, message }),
            "rule_not_found" => NotFound(new { errorCode, message }),
            "block_not_found" => NotFound(new { errorCode, message }),
            "appointment_already_exists" => Conflict(new { errorCode, message }),
            "request_window_conflict" => Conflict(new { errorCode, message }),
            "slot_unavailable" => Conflict(new { errorCode, message }),
            "invalid_state" => Conflict(new { errorCode, message }),
            "policy_violation" => Conflict(new { errorCode, message }),
            "duplicate_checkin" => Conflict(new { errorCode, message }),
            "duplicate_start" => Conflict(new { errorCode, message }),
            "invalid_operational_transition" => Conflict(new { errorCode, message }),
            "required_checklist_pending" => Conflict(new { errorCode, message }),
            "checklist_not_configured" => Conflict(new { errorCode, message }),
            "evidence_required" => Conflict(new { errorCode, message }),
            "rule_overlap" => Conflict(new { errorCode, message }),
            "block_overlap" => Conflict(new { errorCode, message }),
            "block_conflict_appointment" => Conflict(new { errorCode, message }),
            "scope_change_pending" => Conflict(new { errorCode, message }),
            "scope_change_expired" => Conflict(new { errorCode, message }),
            "scope_change_not_found" => NotFound(new { errorCode, message }),
            "attachment_limit_exceeded" => Conflict(new { errorCode, message }),
            "warranty_not_eligible" => Conflict(new { errorCode, message }),
            "warranty_expired" => Conflict(new { errorCode, message }),
            "warranty_claim_already_open" => Conflict(new { errorCode, message }),
            "invalid_warranty_issue" => BadRequest(new { errorCode, message }),
            "invalid_warranty_response_reason" => BadRequest(new { errorCode, message }),
            "warranty_claim_not_found" => NotFound(new { errorCode, message }),
            "warranty_claim_invalid_state" => Conflict(new { errorCode, message }),
            "warranty_revisit_already_scheduled" => Conflict(new { errorCode, message }),
            "warranty_response_window_expired" => Conflict(new { errorCode, message }),
            "invalid_warranty_revisit_window" => BadRequest(new { errorCode, message }),
            "warranty_revisit_slot_unavailable" => Conflict(new { errorCode, message }),
            "dispute_already_open" => Conflict(new { errorCode, message }),
            "dispute_not_eligible" => Conflict(new { errorCode, message }),
            "dispute_open_freeze" => Conflict(new { errorCode, message }),
            "invalid_dispute_type" => BadRequest(new { errorCode, message }),
            "invalid_dispute_reason" => BadRequest(new { errorCode, message }),
            "invalid_dispute_description" => BadRequest(new { errorCode, message }),
            "invalid_dispute_message" => BadRequest(new { errorCode, message }),
            "invalid_dispute" => BadRequest(new { errorCode, message }),
            "dispute_not_found" => NotFound(new { errorCode, message }),
            "invalid_pin" => Conflict(new { errorCode, message }),
            "pin_expired" => Conflict(new { errorCode, message }),
            "pin_locked" => Conflict(new { errorCode, message }),
            "invalid_pin_format" => BadRequest(new { errorCode, message }),
            "invalid_scope_change_reason" => BadRequest(new { errorCode, message }),
            "invalid_scope_change_description" => BadRequest(new { errorCode, message }),
            "invalid_scope_change_value" => BadRequest(new { errorCode, message }),
            "invalid_scope_change" => BadRequest(new { errorCode, message }),
            "invalid_attachment" => BadRequest(new { errorCode, message }),
            "invalid_attachment_size" => BadRequest(new { errorCode, message }),
            "invalid_justification" => BadRequest(new { errorCode, message }),
            "invalid_acceptance_method" => BadRequest(new { errorCode, message }),
            "signature_required" => BadRequest(new { errorCode, message }),
            "contest_reason_required" => BadRequest(new { errorCode, message }),
            "financial_policy_unavailable" => StatusCode(StatusCodes.Status503ServiceUnavailable, new { errorCode, message }),
            "item_not_found" => NotFound(new { errorCode, message }),
            "completion_term_not_found" => NotFound(new { errorCode, message }),
            _ => BadRequest(new { errorCode, message })
        };
    }

    private static bool CanSimulateEventType(string actorRole, ServiceFinancialPolicyEventType eventType)
    {
        if (actorRole.Equals(UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (actorRole.Equals(UserRole.Client.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return eventType is ServiceFinancialPolicyEventType.ClientCancellation or ServiceFinancialPolicyEventType.ClientNoShow;
        }

        if (actorRole.Equals(UserRole.Provider.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return eventType is ServiceFinancialPolicyEventType.ProviderCancellation or ServiceFinancialPolicyEventType.ProviderNoShow;
        }

        return false;
    }

    public class ScopeChangeAttachmentUploadRequest
    {
        public IFormFile? File { get; set; }
    }

    public class DisputeAttachmentUploadRequest
    {
        public IFormFile? File { get; set; }
        public string? MessageText { get; set; }
    }

    public class DisputeMessageRequest
    {
        public string? MessageText { get; set; }
    }
}
