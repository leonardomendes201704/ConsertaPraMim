using System.Globalization;
using System.Security.Claims;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Web.Client.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Client.Controllers;

[Authorize(Roles = "Client")]
public class ServiceRequestsController : Controller
{
    private readonly IServiceRequestService _requestService;
    private readonly IServiceCategoryCatalogService _serviceCategoryCatalogService;
    private readonly IProposalService _proposalService;
    private readonly IProviderGalleryService _providerGalleryService;
    private readonly IZipGeocodingService _zipGeocodingService;
    private readonly IServiceAppointmentService _serviceAppointmentService;
    private readonly IServiceAppointmentChecklistService _serviceAppointmentChecklistService;

    public ServiceRequestsController(
        IServiceRequestService requestService,
        IServiceCategoryCatalogService serviceCategoryCatalogService,
        IProposalService proposalService,
        IProviderGalleryService providerGalleryService,
        IZipGeocodingService zipGeocodingService,
        IServiceAppointmentService serviceAppointmentService,
        IServiceAppointmentChecklistService serviceAppointmentChecklistService)
    {
        _requestService = requestService;
        _serviceCategoryCatalogService = serviceCategoryCatalogService;
        _proposalService = proposalService;
        _providerGalleryService = providerGalleryService;
        _zipGeocodingService = zipGeocodingService;
        _serviceAppointmentService = serviceAppointmentService;
        _serviceAppointmentChecklistService = serviceAppointmentChecklistService;
    }

    public async Task<IActionResult> Index()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var requests = await _requestService.GetAllAsync(userId, UserRole.Client.ToString());
        return View(requests);
    }

    [HttpGet]
    public async Task<IActionResult> ListData()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var requests = (await _requestService.GetAllAsync(userId, UserRole.Client.ToString()))
            .Select(r => new
            {
                id = r.Id,
                status = r.Status,
                category = r.Category,
                description = r.Description,
                createdAt = r.CreatedAt.ToString("dd/MM/yyyy"),
                street = r.Street,
                city = r.City
            });

        return Json(new { requests });
    }

    [HttpGet]
    public async Task<IActionResult> Appointments()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var items = await BuildAppointmentListAsync(userId);
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> AppointmentsData()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var items = await BuildAppointmentListAsync(userId);
        return Json(new
        {
            appointments = items.Select(MapAppointmentListItemPayload)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadActiveCategoriesAsync();
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> ResolveZip(string zipCode)
    {
        var resolved = await _zipGeocodingService.ResolveCoordinatesAsync(zipCode);
        if (!resolved.HasValue)
        {
            return NotFound(new { message = "Nao foi possivel localizar esse CEP." });
        }

        return Json(new
        {
            zipCode = resolved.Value.NormalizedZip,
            street = resolved.Value.Street,
            city = resolved.Value.City
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateServiceRequestDto dto)
    {
        await LoadActiveCategoriesAsync();

        if (!ModelState.IsValid)
        {
            return View(dto);
        }

        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        Guid requestId;
        try
        {
            requestId = await _requestService.CreateAsync(userId, dto);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(dto.CategoryId), ex.Message);
            return View(dto);
        }

        TempData["Success"] = "Pedido criado com sucesso! Aguarde propostas profissionais.";
        return RedirectToAction(nameof(Details), new { id = requestId });
    }

    public async Task<IActionResult> Details(Guid id)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var request = await _requestService.GetByIdAsync(id, userId, UserRole.Client.ToString());
        if (request == null)
        {
            return NotFound();
        }

        var proposals = await _proposalService.GetByRequestAsync(id, userId, UserRole.Client.ToString());
        var providerNames = BuildProviderNameMap(proposals);
        var appointments = await GetAppointmentsByRequestAsync(userId, id);
        var appointment = appointments.FirstOrDefault();
        var checklistByAppointmentId = await BuildChecklistPayloadMapAsync(userId, appointments);
        var completionTermByAppointmentId = await BuildCompletionTermPayloadMapAsync(userId, appointments);
        var evidences = await _providerGalleryService.GetEvidenceTimelineByServiceRequestAsync(
            id,
            userId,
            UserRole.Client.ToString());
        var appointmentPayloads = appointments
            .Select(a => MapAppointmentPayload(a, providerNames, checklistByAppointmentId, completionTermByAppointmentId))
            .ToList();
        var evidencePayloads = evidences
            .Select(e => MapEvidencePayload(e, providerNames))
            .ToList();

        ViewBag.Proposals = proposals;
        ViewBag.AcceptedProviders = proposals
            .Where(p => p.Accepted)
            .GroupBy(p => p.ProviderId)
            .Select(g => new AcceptedProviderOptionDto(g.Key, g.First().ProviderName))
            .ToList();
        ViewBag.Appointment = appointment;
        ViewBag.AppointmentPayload = appointmentPayloads.FirstOrDefault();
        ViewBag.Appointments = appointmentPayloads;
        ViewBag.Evidences = evidencePayloads;

        return View(request);
    }

    [HttpGet]
    public async Task<IActionResult> DetailsData(Guid id)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var request = await _requestService.GetByIdAsync(id, userId, UserRole.Client.ToString());
        if (request == null)
        {
            return NotFound();
        }

        var proposals = await _proposalService.GetByRequestAsync(id, userId, UserRole.Client.ToString());
        var providerNames = BuildProviderNameMap(proposals);
        var appointments = await GetAppointmentsByRequestAsync(userId, id);
        var appointment = appointments.FirstOrDefault();
        var checklistByAppointmentId = await BuildChecklistPayloadMapAsync(userId, appointments);
        var completionTermByAppointmentId = await BuildCompletionTermPayloadMapAsync(userId, appointments);
        var evidences = await _providerGalleryService.GetEvidenceTimelineByServiceRequestAsync(
            id,
            userId,
            UserRole.Client.ToString());

        return Json(new
        {
            requestStatus = request.Status,
            proposals,
            acceptedProviders = proposals
                .Where(p => p.Accepted)
                .GroupBy(p => p.ProviderId)
                .Select(g => new { providerId = g.Key, providerName = g.First().ProviderName }),
            appointment = MapAppointmentPayload(appointment, providerNames, checklistByAppointmentId, completionTermByAppointmentId),
            appointments = appointments.Select(a => MapAppointmentPayload(a, providerNames, checklistByAppointmentId, completionTermByAppointmentId)),
            evidences = evidences.Select(e => MapEvidencePayload(e, providerNames))
        });
    }

    [HttpGet]
    public async Task<IActionResult> AppointmentData(Guid id)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var request = await _requestService.GetByIdAsync(id, userId, UserRole.Client.ToString());
        if (request == null)
        {
            return NotFound();
        }

        var proposals = await _proposalService.GetByRequestAsync(id, userId, UserRole.Client.ToString());
        var providerNames = BuildProviderNameMap(proposals);
        var appointments = await GetAppointmentsByRequestAsync(userId, id);
        var appointment = appointments.FirstOrDefault();
        var checklistByAppointmentId = await BuildChecklistPayloadMapAsync(userId, appointments);
        var completionTermByAppointmentId = await BuildCompletionTermPayloadMapAsync(userId, appointments);
        var evidences = await _providerGalleryService.GetEvidenceTimelineByServiceRequestAsync(
            id,
            userId,
            UserRole.Client.ToString());

        return Json(new
        {
            requestId = id,
            requestStatus = request.Status,
            acceptedProviders = proposals
                .Where(p => p.Accepted)
                .GroupBy(p => p.ProviderId)
                .Select(g => new { providerId = g.Key, providerName = g.First().ProviderName }),
            appointment = MapAppointmentPayload(appointment, providerNames, checklistByAppointmentId, completionTermByAppointmentId),
            appointments = appointments.Select(a => MapAppointmentPayload(a, providerNames, checklistByAppointmentId, completionTermByAppointmentId)),
            evidences = evidences.Select(e => MapEvidencePayload(e, providerNames))
        });
    }

    [HttpGet]
    public async Task<IActionResult> Slots(Guid requestId, Guid providerId, string date)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var request = await _requestService.GetByIdAsync(requestId, userId, UserRole.Client.ToString());
        if (request == null)
        {
            return NotFound(new { errorCode = "request_not_found", message = "Pedido nao encontrado." });
        }

        var proposals = await _proposalService.GetByRequestAsync(requestId, userId, UserRole.Client.ToString());
        var providerAccepted = proposals.Any(p => p.ProviderId == providerId && p.Accepted);
        if (!providerAccepted)
        {
            return Conflict(new { errorCode = "provider_not_assigned", message = "Prestador sem proposta aceita para este pedido." });
        }

        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            return BadRequest(new { errorCode = "invalid_date", message = "Data invalida para consulta de slots." });
        }

        var dayStartLocal = DateTime.SpecifyKind(parsedDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local);
        var fromUtc = dayStartLocal.ToUniversalTime();
        var toUtc = fromUtc.AddDays(1);

        var result = await _serviceAppointmentService.GetAvailableSlotsAsync(
            userId,
            UserRole.Client.ToString(),
            new GetServiceAppointmentSlotsQueryDto(providerId, fromUtc, toUtc));

        if (!result.Success)
        {
            return MapAppointmentFailure(result.ErrorCode, result.ErrorMessage);
        }

        return Json(new
        {
            slots = result.Slots.Select(s => new
            {
                windowStartUtc = s.WindowStartUtc,
                windowEndUtc = s.WindowEndUtc
            })
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateAppointment([FromBody] CreateAppointmentInput input)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        if (input.ServiceRequestId == Guid.Empty || input.ProviderId == Guid.Empty)
        {
            return BadRequest(new { errorCode = "invalid_input", message = "Pedido e prestador sao obrigatorios." });
        }

        var result = await _serviceAppointmentService.CreateAsync(
            userId,
            UserRole.Client.ToString(),
            new CreateServiceAppointmentRequestDto(
                input.ServiceRequestId,
                input.ProviderId,
                input.WindowStartUtc,
                input.WindowEndUtc,
                input.Reason));

        if (!result.Success || result.Appointment == null)
        {
            return MapAppointmentFailure(result.ErrorCode, result.ErrorMessage);
        }

        return Ok(new { success = true, appointment = MapAppointmentPayload(result.Appointment) });
    }

    [HttpPost]
    public async Task<IActionResult> RequestAppointmentReschedule([FromBody] RequestRescheduleInput input)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        if (input.AppointmentId == Guid.Empty)
        {
            return BadRequest(new { errorCode = "invalid_input", message = "Agendamento invalido." });
        }

        var result = await _serviceAppointmentService.RequestRescheduleAsync(
            userId,
            UserRole.Client.ToString(),
            input.AppointmentId,
            new RequestServiceAppointmentRescheduleDto(
                input.ProposedWindowStartUtc,
                input.ProposedWindowEndUtc,
                input.Reason));

        if (!result.Success || result.Appointment == null)
        {
            return MapAppointmentFailure(result.ErrorCode, result.ErrorMessage);
        }

        return Ok(new { success = true, appointment = MapAppointmentPayload(result.Appointment) });
    }

    [HttpPost]
    public async Task<IActionResult> RespondAppointmentReschedule([FromBody] RespondRescheduleInput input)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        if (input.AppointmentId == Guid.Empty)
        {
            return BadRequest(new { errorCode = "invalid_input", message = "Agendamento invalido." });
        }

        var result = await _serviceAppointmentService.RespondRescheduleAsync(
            userId,
            UserRole.Client.ToString(),
            input.AppointmentId,
            new RespondServiceAppointmentRescheduleRequestDto(input.Accept, input.Reason));

        if (!result.Success || result.Appointment == null)
        {
            return MapAppointmentFailure(result.ErrorCode, result.ErrorMessage);
        }

        return Ok(new { success = true, appointment = MapAppointmentPayload(result.Appointment) });
    }

    [HttpPost]
    public async Task<IActionResult> CancelAppointment([FromBody] CancelAppointmentInput input)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        if (input.AppointmentId == Guid.Empty)
        {
            return BadRequest(new { errorCode = "invalid_input", message = "Agendamento invalido." });
        }

        var result = await _serviceAppointmentService.CancelAsync(
            userId,
            UserRole.Client.ToString(),
            input.AppointmentId,
            new CancelServiceAppointmentRequestDto(input.Reason));

        if (!result.Success || result.Appointment == null)
        {
            return MapAppointmentFailure(result.ErrorCode, result.ErrorMessage);
        }

        return Ok(new { success = true, appointment = MapAppointmentPayload(result.Appointment) });
    }

    [HttpPost]
    public async Task<IActionResult> ConfirmAppointmentCompletion([FromBody] ConfirmAppointmentCompletionInput input)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        if (input.AppointmentId == Guid.Empty)
        {
            return BadRequest(new { errorCode = "invalid_input", message = "Agendamento invalido." });
        }

        var method = string.IsNullOrWhiteSpace(input.Method) ? "Pin" : input.Method.Trim();
        var result = await _serviceAppointmentService.ConfirmCompletionAsync(
            userId,
            UserRole.Client.ToString(),
            input.AppointmentId,
            new ConfirmServiceCompletionRequestDto(
                method,
                input.Pin?.Trim(),
                input.SignatureName?.Trim()));

        if (!result.Success || result.Term == null)
        {
            return MapAppointmentFailure(result.ErrorCode, result.ErrorMessage);
        }

        return Ok(new { success = true, term = result.Term });
    }

    [HttpPost]
    public async Task<IActionResult> ContestAppointmentCompletion([FromBody] ContestAppointmentCompletionInput input)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        if (input.AppointmentId == Guid.Empty)
        {
            return BadRequest(new { errorCode = "invalid_input", message = "Agendamento invalido." });
        }

        var result = await _serviceAppointmentService.ContestCompletionAsync(
            userId,
            UserRole.Client.ToString(),
            input.AppointmentId,
            new ContestServiceCompletionRequestDto(input.Reason ?? string.Empty));

        if (!result.Success || result.Term == null)
        {
            return MapAppointmentFailure(result.ErrorCode, result.ErrorMessage);
        }

        return Ok(new { success = true, term = result.Term });
    }

    private async Task<IReadOnlyList<ClientAppointmentListItemViewModel>> BuildAppointmentListAsync(Guid userId)
    {
        var appointments = await _serviceAppointmentService.GetMyAppointmentsAsync(userId, UserRole.Client.ToString());
        var requests = (await _requestService.GetAllAsync(userId, UserRole.Client.ToString()))
            .ToDictionary(r => r.Id, r => r);

        var proposalsCache = new Dictionary<Guid, IReadOnlyList<ProposalDto>>();
        var items = new List<ClientAppointmentListItemViewModel>();

        foreach (var appointment in appointments.OrderByDescending(a => a.WindowStartUtc))
        {
            requests.TryGetValue(appointment.ServiceRequestId, out var request);

            if (!proposalsCache.TryGetValue(appointment.ServiceRequestId, out var proposals))
            {
                proposals = (await _proposalService.GetByRequestAsync(
                        appointment.ServiceRequestId,
                        userId,
                        UserRole.Client.ToString()))
                    .ToList();
                proposalsCache[appointment.ServiceRequestId] = proposals;
            }

            var providerName = proposals
                .FirstOrDefault(p => p.ProviderId == appointment.ProviderId)
                ?.ProviderName ?? "Prestador";

            items.Add(new ClientAppointmentListItemViewModel(
                appointment.Id,
                appointment.ServiceRequestId,
                appointment.ProviderId,
                providerName,
                request?.Category ?? "Categoria",
                request?.Description ?? "Pedido",
                request?.Street ?? "",
                request?.City ?? "",
                appointment.Status,
                appointment.WindowStartUtc,
                appointment.WindowEndUtc,
                appointment.CreatedAt,
                appointment.UpdatedAt));
        }

        return items;
    }

    private async Task<IReadOnlyList<ServiceAppointmentDto>> GetAppointmentsByRequestAsync(Guid userId, Guid requestId)
    {
        var appointments = await _serviceAppointmentService.GetMyAppointmentsAsync(userId, UserRole.Client.ToString());
        return appointments
            .Where(a => a.ServiceRequestId == requestId)
            .OrderByDescending(a => a.UpdatedAt ?? a.CreatedAt)
            .ToList();
    }

    private IActionResult MapAppointmentFailure(string? errorCode, string? errorMessage)
    {
        var payload = new { errorCode, message = errorMessage };

        return errorCode switch
        {
            "forbidden" => Forbid(),
            "provider_not_found" => NotFound(payload),
            "request_not_found" => NotFound(payload),
            "appointment_not_found" => NotFound(payload),
            "appointment_already_exists" => Conflict(payload),
            "request_window_conflict" => Conflict(payload),
            "slot_unavailable" => Conflict(payload),
            "invalid_state" => Conflict(payload),
            "policy_violation" => Conflict(payload),
            "invalid_pin" => Conflict(payload),
            "pin_expired" => Conflict(payload),
            "pin_locked" => Conflict(payload),
            "invalid_pin_format" => BadRequest(payload),
            "invalid_acceptance_method" => BadRequest(payload),
            "signature_required" => BadRequest(payload),
            "contest_reason_required" => BadRequest(payload),
            "completion_term_not_found" => NotFound(payload),
            _ => BadRequest(payload)
        };
    }

    private static object MapAppointmentListItemPayload(ClientAppointmentListItemViewModel item)
    {
        return new
        {
            appointmentId = item.AppointmentId,
            serviceRequestId = item.ServiceRequestId,
            providerId = item.ProviderId,
            providerName = item.ProviderName,
            category = item.Category,
            description = item.Description,
            street = item.Street,
            city = item.City,
            status = item.Status,
            windowStartUtc = item.WindowStartUtc,
            windowEndUtc = item.WindowEndUtc,
            createdAtUtc = item.CreatedAtUtc,
            updatedAtUtc = item.UpdatedAtUtc
        };
    }

    private static object? MapAppointmentPayload(
        ServiceAppointmentDto? appointment,
        IReadOnlyDictionary<Guid, string>? providerNames = null,
        IReadOnlyDictionary<Guid, object?>? checklistByAppointmentId = null,
        IReadOnlyDictionary<Guid, object?>? completionTermByAppointmentId = null)
    {
        if (appointment == null)
        {
            return null;
        }

        var providerName = "Prestador";
        if (providerNames != null &&
            providerNames.TryGetValue(appointment.ProviderId, out var mappedProviderName) &&
            !string.IsNullOrWhiteSpace(mappedProviderName))
        {
            providerName = mappedProviderName;
        }

        object? checklistPayload = null;
        checklistByAppointmentId?.TryGetValue(appointment.Id, out checklistPayload);
        object? completionTermPayload = null;
        completionTermByAppointmentId?.TryGetValue(appointment.Id, out completionTermPayload);

        return new
        {
            id = appointment.Id,
            serviceRequestId = appointment.ServiceRequestId,
            clientId = appointment.ClientId,
            providerId = appointment.ProviderId,
            providerName,
            status = appointment.Status,
            windowStartUtc = appointment.WindowStartUtc,
            windowEndUtc = appointment.WindowEndUtc,
            expiresAtUtc = appointment.ExpiresAtUtc,
            reason = appointment.Reason,
            proposedWindowStartUtc = appointment.ProposedWindowStartUtc,
            proposedWindowEndUtc = appointment.ProposedWindowEndUtc,
            rescheduleRequestedAtUtc = appointment.RescheduleRequestedAtUtc,
            rescheduleRequestedByRole = appointment.RescheduleRequestedByRole,
            rescheduleRequestReason = appointment.RescheduleRequestReason,
            arrivedAtUtc = appointment.ArrivedAtUtc,
            arrivedLatitude = appointment.ArrivedLatitude,
            arrivedLongitude = appointment.ArrivedLongitude,
            arrivedAccuracyMeters = appointment.ArrivedAccuracyMeters,
            arrivedManualReason = appointment.ArrivedManualReason,
            startedAtUtc = appointment.StartedAtUtc,
            operationalStatus = appointment.OperationalStatus,
            operationalStatusUpdatedAtUtc = appointment.OperationalStatusUpdatedAtUtc,
            operationalStatusReason = appointment.OperationalStatusReason,
            createdAt = appointment.CreatedAt,
            updatedAt = appointment.UpdatedAt,
            history = appointment.History
                .OrderBy(h => h.OccurredAtUtc)
                .Select(h => new
                {
                    id = h.Id,
                    previousStatus = h.PreviousStatus,
                    newStatus = h.NewStatus,
                    actorUserId = h.ActorUserId,
                    actorRole = h.ActorRole,
                    reason = h.Reason,
                    previousOperationalStatus = h.PreviousOperationalStatus,
                    newOperationalStatus = h.NewOperationalStatus,
                    metadata = h.Metadata,
                    occurredAtUtc = h.OccurredAtUtc
                }),
            checklist = checklistPayload,
            completionTerm = completionTermPayload
        };
    }

    private static object MapEvidencePayload(
        ServiceRequestEvidenceTimelineItemDto evidence,
        IReadOnlyDictionary<Guid, string>? providerNames = null)
    {
        var providerName = evidence.ProviderName;
        if (providerNames != null &&
            providerNames.TryGetValue(evidence.ProviderId, out var mappedProviderName) &&
            !string.IsNullOrWhiteSpace(mappedProviderName))
        {
            providerName = mappedProviderName;
        }

        return new
        {
            id = evidence.Id,
            serviceRequestId = evidence.ServiceRequestId,
            providerId = evidence.ProviderId,
            providerName,
            serviceAppointmentId = evidence.ServiceAppointmentId,
            evidencePhase = evidence.EvidencePhase,
            fileUrl = evidence.FileUrl,
            thumbnailUrl = evidence.ThumbnailUrl,
            previewUrl = evidence.PreviewUrl,
            fileName = evidence.FileName,
            contentType = evidence.ContentType,
            mediaKind = evidence.MediaKind,
            category = evidence.Category,
            caption = evidence.Caption,
            createdAt = evidence.CreatedAt
        };
    }

    private async Task<IReadOnlyDictionary<Guid, object?>> BuildChecklistPayloadMapAsync(
        Guid actorUserId,
        IReadOnlyList<ServiceAppointmentDto> appointments)
    {
        var result = new Dictionary<Guid, object?>();

        foreach (var appointment in appointments)
        {
            var checklistResult = await _serviceAppointmentChecklistService.GetChecklistAsync(
                actorUserId,
                UserRole.Client.ToString(),
                appointment.Id);

            if (!checklistResult.Success || checklistResult.Checklist == null || !checklistResult.Checklist.IsRequiredChecklist)
            {
                result[appointment.Id] = null;
                continue;
            }

            var checklist = checklistResult.Checklist;
            result[appointment.Id] = new
            {
                templateId = checklist.TemplateId,
                templateName = checklist.TemplateName,
                categoryName = checklist.CategoryName,
                isRequiredChecklist = checklist.IsRequiredChecklist,
                requiredItemsCount = checklist.RequiredItemsCount,
                requiredCompletedCount = checklist.RequiredCompletedCount,
                items = checklist.Items
                    .OrderBy(i => i.SortOrder)
                    .Select(i => new
                    {
                        templateItemId = i.TemplateItemId,
                        title = i.Title,
                        helpText = i.HelpText,
                        isRequired = i.IsRequired,
                        requiresEvidence = i.RequiresEvidence,
                        allowNote = i.AllowNote,
                        sortOrder = i.SortOrder,
                        isChecked = i.IsChecked,
                        note = i.Note,
                        evidenceUrl = i.EvidenceUrl,
                        evidenceFileName = i.EvidenceFileName,
                        evidenceContentType = i.EvidenceContentType,
                        evidenceSizeBytes = i.EvidenceSizeBytes,
                        checkedByUserId = i.CheckedByUserId,
                        checkedAtUtc = i.CheckedAtUtc
                    }),
                history = checklist.History.Select(h => new
                {
                    id = h.Id,
                    templateItemId = h.TemplateItemId,
                    itemTitle = h.ItemTitle,
                    previousIsChecked = h.PreviousIsChecked,
                    newIsChecked = h.NewIsChecked,
                    previousNote = h.PreviousNote,
                    newNote = h.NewNote,
                    previousEvidenceUrl = h.PreviousEvidenceUrl,
                    newEvidenceUrl = h.NewEvidenceUrl,
                    actorUserId = h.ActorUserId,
                    actorRole = h.ActorRole,
                    occurredAtUtc = h.OccurredAtUtc
                })
            };
        }

        return result;
    }

    private async Task<IReadOnlyDictionary<Guid, object?>> BuildCompletionTermPayloadMapAsync(
        Guid actorUserId,
        IReadOnlyList<ServiceAppointmentDto> appointments)
    {
        var result = new Dictionary<Guid, object?>();

        foreach (var appointment in appointments)
        {
            var completionResult = await _serviceAppointmentService.GetCompletionTermAsync(
                actorUserId,
                UserRole.Client.ToString(),
                appointment.Id);

            if (!completionResult.Success || completionResult.Term == null)
            {
                result[appointment.Id] = null;
                continue;
            }

            var term = completionResult.Term;
            result[appointment.Id] = new
            {
                id = term.Id,
                serviceRequestId = term.ServiceRequestId,
                serviceAppointmentId = term.ServiceAppointmentId,
                providerId = term.ProviderId,
                clientId = term.ClientId,
                status = term.Status,
                acceptedWithMethod = term.AcceptedWithMethod,
                pinExpiresAtUtc = term.PinExpiresAtUtc,
                pinFailedAttempts = term.PinFailedAttempts,
                acceptedAtUtc = term.AcceptedAtUtc,
                contestedAtUtc = term.ContestedAtUtc,
                escalatedAtUtc = term.EscalatedAtUtc,
                createdAt = term.CreatedAt,
                updatedAt = term.UpdatedAt,
                summary = term.Summary,
                acceptedSignatureName = term.AcceptedSignatureName,
                contestReason = term.ContestReason
            };
        }

        return result;
    }

    private static IReadOnlyDictionary<Guid, string> BuildProviderNameMap(IEnumerable<ProposalDto> proposals)
    {
        return proposals
            .GroupBy(p => p.ProviderId)
            .ToDictionary(g => g.Key, g => g.First().ProviderName);
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var userIdRaw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdRaw, out userId);
    }

    private async Task LoadActiveCategoriesAsync()
    {
        var categories = await _serviceCategoryCatalogService.GetActiveAsync();
        ViewBag.ActiveServiceCategories = categories;
    }

    public sealed record AcceptedProviderOptionDto(Guid ProviderId, string ProviderName);
    public sealed record CreateAppointmentInput(Guid ServiceRequestId, Guid ProviderId, DateTime WindowStartUtc, DateTime WindowEndUtc, string? Reason);
    public sealed record RequestRescheduleInput(Guid AppointmentId, DateTime ProposedWindowStartUtc, DateTime ProposedWindowEndUtc, string Reason);
    public sealed record RespondRescheduleInput(Guid AppointmentId, bool Accept, string? Reason);
    public sealed record CancelAppointmentInput(Guid AppointmentId, string Reason);
    public sealed record ConfirmAppointmentCompletionInput(Guid AppointmentId, string Method, string? Pin, string? SignatureName);
    public sealed record ContestAppointmentCompletionInput(Guid AppointmentId, string Reason);
}
